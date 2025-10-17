using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using CsvHelper;

namespace AI_AI_Agent.Infrastructure.Tools;

public class CsvAnalyzeTool : ITool
{
    public string Name => "CsvAnalyze";
    public string Description => "Loads a CSV file (local path or URL), computes basic stats and optional column summaries.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local path or HTTP/HTTPS URL to a CSV." },
            delimiter = new { type = "string", description = "CSV delimiter (default ',')." },
            sampleRows = new { type = "number", description = "Include up to N sample rows (default 5)." }
        },
        required = new[] { "path" }
    };

    private readonly string _workspace;
    public CsvAnalyzeTool()
    {
        _workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (!args.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            return "Error: 'path' is required";
        var input = p.GetString()!;
        var delimiter = args.TryGetProperty("delimiter", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()![0] : ',';
        var sampleRows = args.TryGetProperty("sampleRows", out var sr) && sr.ValueKind == JsonValueKind.Number ? sr.GetInt32() : 5;

        var localPath = input;
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            var fileName = "csv_" + Guid.NewGuid().ToString("N") + ".csv";
            localPath = Path.Combine(_workspace, fileName);
            using var http = new System.Net.Http.HttpClient();
            using var resp = await http.GetAsync(input, cancellationToken);
            resp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs, cancellationToken);
        }

        if (!File.Exists(localPath)) return $"File not found: {localPath}";

        using var reader = new StreamReader(localPath);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            DetectColumnCountChanges = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.ToList() ?? new System.Collections.Generic.List<string>();

        var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string?>>();
        int count = 0;
        while (await csv.ReadAsync())
        {
            var row = headers.ToDictionary(h => h, h => csv.GetField(h));
            rows.Add(row);
            count++;
        }

        var sample = rows.Take(Math.Max(0, Math.Min(sampleRows, rows.Count))).ToList();
        return new { message = "CSV analyzed", path = localPath, rows = count, columns = headers, sample };
    }
}
