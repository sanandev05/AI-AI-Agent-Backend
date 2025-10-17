using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using ClosedXML.Excel;

namespace AI_AI_Agent.Infrastructure.Tools;

public class ExcelReadTool : ITool
{
    public string Name => "ExcelRead";
    public string Description => "Reads an Excel .xlsx file (local or URL) and returns sheets, columns, row counts, and sample rows.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local file path or HTTP/HTTPS URL to an .xlsx file." },
            sampleRows = new { type = "number", description = "Number of sample rows to include per sheet (default 5)." }
        },
        required = new[] { "path" }
    };

    private readonly string _workspace;
    public ExcelReadTool()
    {
        _workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (!args.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            return "Error: 'path' is required";
        var input = p.GetString()!;
        var sampleRows = args.TryGetProperty("sampleRows", out var sr) && sr.ValueKind == JsonValueKind.Number ? sr.GetInt32() : 5;

        var localPath = input;
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            var fileName = "excel_" + Guid.NewGuid().ToString("N") + ".xlsx";
            localPath = Path.Combine(_workspace, fileName);
            using var http = new System.Net.Http.HttpClient();
            using var resp = await http.GetAsync(input, cancellationToken);
            resp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs, cancellationToken);
        }

        if (!File.Exists(localPath)) return $"File not found: {localPath}";

        using var wb = new XLWorkbook(localPath);
        var sheets = new List<object>();
        foreach (var ws in wb.Worksheets)
        {
            var used = ws.RangeUsed();
            if (used is null)
            {
                sheets.Add(new { name = ws.Name, rows = 0, columns = Array.Empty<string>(), sample = Array.Empty<object>() });
                continue;
            }

            var firstRow = used.FirstRowUsed();
            var headers = firstRow.Cells().Select(c => c.GetString()).ToList();
            int rowCount = used.RowCount() - 1; // exclude header row

            var sample = new List<Dictionary<string, string?>>();
            var dataRows = used.RowsUsed().Skip(1).Take(sampleRows);
            foreach (var row in dataRows)
            {
                var dict = new Dictionary<string, string?>();
                int idx = 0;
                foreach (var cell in row.Cells(1, headers.Count))
                {
                    var key = idx < headers.Count && !string.IsNullOrWhiteSpace(headers[idx]) ? headers[idx] : $"col_{idx+1}";
                    dict[key] = cell.GetFormattedString();
                    idx++;
                }
                sample.Add(dict);
            }

            sheets.Add(new { name = ws.Name, rows = Math.Max(0, rowCount), columns = headers, sample });
        }

        return new { message = "Excel analyzed", path = localPath, sheets };
    }
}
