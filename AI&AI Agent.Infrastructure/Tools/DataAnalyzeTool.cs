using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using ClosedXML.Excel;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class DataAnalyzeTool : ITool
{
    public string Name => "DataAnalyze";
    public string Description => "Analyzes CSV or Excel data to compute stats, trends, forecasts, anomalies, and insights.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Path to local CSV or XLSX file under workspace." },
            sheet = new { type = "string", description = "Optional sheet for XLSX." },
            maxRows = new { type = "number", description = "Optional row cap (default 5000)." },
            generateChart = new { type = "boolean", description = "If true, create a quick chart for the first numeric column." }
        },
        required = new[] { "path" }
    };

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.GetProperty("path").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "Error: file path not found.";

        int maxRows = args.TryGetProperty("maxRows", out var mr) && mr.ValueKind == JsonValueKind.Number ? mr.GetInt32() : 5000;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        List<Dictionary<string, string>> rows;
        if (ext == ".csv") rows = ReadCsv(path, maxRows);
        else if (ext == ".xlsx") rows = ReadXlsx(path, args.TryGetProperty("sheet", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null, maxRows);
        else return "Error: only .csv or .xlsx supported.";

        if (rows.Count == 0) return new { message = "No data." };

        var columns = rows.SelectMany(r => r.Keys).Distinct().ToList();
        // Numeric detection
        var numericCols = new List<string>();
        var colValues = new Dictionary<string, List<double>>();
        foreach (var c in columns)
        {
            var vals = new List<double>();
            foreach (var r in rows)
            {
                if (r.TryGetValue(c, out var s) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    vals.Add(d);
            }
            if (vals.Count >= Math.Max(5, rows.Count / 10))
            {
                numericCols.Add(c);
                colValues[c] = vals;
            }
        }

        var stats = new Dictionary<string, object>();
        foreach (var c in numericCols)
        {
            var values = colValues[c];
            values.Sort();
            double mean = values.Average();
            double median = values[values.Count / 2];
            double min = values.First();
            double max = values.Last();
            double std = Math.Sqrt(values.Select(x => (x - mean) * (x - mean)).Average());
            double slope = TrendSlope(values);
            var anomalies = FindAnomalies(values, mean, std).ToList();
            var (q1, q3) = Quartiles(values);
            var forecast = Forecast(values, 3);
            stats[c] = new
            {
                count = values.Count,
                min,
                max,
                mean,
                median,
                stddev = std,
                q1,
                q3,
                iqr = q3 - q1,
                trendSlope = slope,
                forecast,
                anomalies
            };
        }

        var correlations = BuildCorrelationMatrix(numericCols, colValues);

        var insights = new List<string>();
        foreach (var c in numericCols)
        {
            var stat = (dynamic)stats[c];
            if (Math.Abs((double)stat.trendSlope) > 0.01)
            {
                insights.Add($"Column '{c}' shows a {((double)stat.trendSlope > 0 ? "rising" : "falling")} trend.");
            }
            var anomalyCount = ((IEnumerable<double>)stat.anomalies).Count();
            if (anomalyCount > 0)
            {
                insights.Add($"Column '{c}' has {anomalyCount} anomaly point(s) outside 3σ interval.");
            }
        }

        foreach (var (pair, corr) in correlations)
        {
            if (Math.Abs(corr) >= 0.75)
            {
                insights.Add($"Columns '{pair.Item1}' and '{pair.Item2}' are highly {(corr > 0 ? "positively" : "negatively")} correlated ({corr:F2}).");
            }
        }

        object? chartInfo = null;
        var shouldGenerateChart = args.TryGetProperty("generateChart", out var gcProp) && gcProp.ValueKind == JsonValueKind.True;
        if (shouldGenerateChart && numericCols.Count > 0)
        {
            var chartTool = new ChartCreateTool();
            var labels = Enumerable.Range(1, colValues[numericCols[0]].Count).Select(i => $"{i}").ToList();
            var payload = JsonSerializer.Serialize(new
            {
                title = $"{numericCols[0]} Trend",
                kind = "line",
                labels,
                values = colValues[numericCols[0]]
            });
            using var doc = JsonDocument.Parse(payload);
            var chartResult = await chartTool.InvokeAsync(doc.RootElement, ct);
            chartInfo = chartResult;
        }

        return new
        {
            message = "Data analyzed",
            rowCount = rows.Count,
            columns,
            numericColumns = numericCols,
            stats,
            correlations = correlations.ToDictionary(k => $"{k.Key.Item1}::{k.Key.Item2}", v => Math.Round(v.Value, 4)),
            insights,
            chart = chartInfo
        };
    }

    private static double TrendSlope(List<double> v)
    {
        int n = v.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++) { sumX += i; sumY += v[i]; sumXY += i * v[i]; sumXX += i * i; }
        double denom = n * sumXX - sumX * sumX;
        if (Math.Abs(denom) < 1e-9) return 0;
        return (n * sumXY - sumX * sumY) / denom;
    }

    private static IEnumerable<double> FindAnomalies(List<double> v, double mean, double std)
    {
        if (std < 1e-9) yield break;
        foreach (var x in v)
        {
            var z = Math.Abs((x - mean) / std);
            if (z >= 3.0) yield return x;
        }
    }

    private static (double q1, double q3) Quartiles(List<double> sorted)
    {
        if (sorted.Count == 0) return (double.NaN, double.NaN);
        double Q(double percentile)
        {
            var pos = percentile * (sorted.Count + 1);
            var index = Math.Clamp((int)Math.Floor(pos) - 1, 0, sorted.Count - 1);
            var frac = pos - Math.Floor(pos);
            if (index + 1 >= sorted.Count) return sorted[index];
            return sorted[index] + frac * (sorted[index + 1] - sorted[index]);
        }
        return (Q(0.25), Q(0.75));
    }

    private static double[] Forecast(List<double> values, int horizon)
    {
        int n = values.Count;
        if (n < 3) return Array.Empty<double>();
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumXX += i * i;
        }
        double denom = n * sumXX - sumX * sumX;
        if (Math.Abs(denom) < 1e-9) return Array.Empty<double>();
        double slope = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;
        var forecasts = new double[horizon];
        for (int h = 0; h < horizon; h++)
        {
            double x = n + h;
            forecasts[h] = intercept + slope * x;
        }
        return forecasts;
    }

    private static Dictionary<(string, string), double> BuildCorrelationMatrix(List<string> columns, Dictionary<string, List<double>> values)
    {
        var matrix = new Dictionary<(string, string), double>();
        for (int i = 0; i < columns.Count; i++)
        {
            for (int j = i + 1; j < columns.Count; j++)
            {
                var a = values[columns[i]];
                var b = values[columns[j]];
                var n = Math.Min(a.Count, b.Count);
                if (n < 3) continue;
                double meanA = a.Take(n).Average();
                double meanB = b.Take(n).Average();
                double num = 0, denA = 0, denB = 0;
                for (int k = 0; k < n; k++)
                {
                    var da = a[k] - meanA;
                    var db = b[k] - meanB;
                    num += da * db;
                    denA += da * da;
                    denB += db * db;
                }
                if (denA < 1e-9 || denB < 1e-9) continue;
                var corr = num / Math.Sqrt(denA * denB);
                matrix[(columns[i], columns[j])] = corr;
            }
        }
        return matrix;
    }

    private static List<Dictionary<string, string>> ReadCsv(string path, int maxRows)
    {
        var rows = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header is null) return rows;
        var cols = header.Split(',');
        string? line;
        int count = 0;
        while ((line = reader.ReadLine()) is not null && count < maxRows)
        {
            var parts = line.Split(',');
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < cols.Length && i < parts.Length; i++)
                dict[cols[i]] = parts[i];
            rows.Add(dict);
            count++;
        }
        return rows;
    }

    private static List<Dictionary<string, string>> ReadXlsx(string path, string? sheet, int maxRows)
    {
        var rows = new List<Dictionary<string, string>>();
        using var wb = new XLWorkbook(path);
        var ws = string.IsNullOrWhiteSpace(sheet) ? wb.Worksheets.First() : wb.Worksheet(sheet);
    var firstRow = ws.FirstRowUsed();
    if (firstRow is null) return rows;
    var colCells = firstRow.CellsUsed().ToList();
        var colNames = colCells.Select(c => c.GetString()).ToList();
        int count = 0;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            if (count >= maxRows) break;
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < colNames.Count; i++)
            {
                dict[colNames[i]] = row.Cell(i + 1).GetString();
            }
            rows.Add(dict);
            count++;
        }
        return rows;
    }
}
