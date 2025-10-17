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

/// <summary>
/// Converts a CSV file into an XLSX workbook.
/// </summary>
public sealed class CsvToXlsxTool : ITool
{
    public string Name => "CsvToXlsx";
    public string Description => "Convert a CSV file to XLSX (Excel).";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Path to the CSV file" },
            fileName = new { type = "string", description = "Optional output file name (must end with .xlsx)" },
            delimiter = new { type = "string", description = "Optional delimiter (default ',')" }
        },
        required = new[] { "path" }
    };

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.GetProperty("path").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Task.FromResult<object>(new { success = false, error = "CSV file not found" });
        }

        var delimiter = args.TryGetProperty("delimiter", out var delimProp) && delimProp.ValueKind == JsonValueKind.String
            ? (delimProp.GetString() ?? ",")
            : ",";
        var fileName = args.TryGetProperty("fileName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String
            ? fnProp.GetString() ?? string.Empty
            : string.Empty;

        var outputFile = string.IsNullOrWhiteSpace(fileName)
            ? $"csv_to_xlsx_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.xlsx"
            : (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".xlsx");

        var workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(workspace);
        var outputPath = Path.Combine(workspace, Path.GetFileName(outputFile));

        var rows = ReadCsv(path, delimiter);
        if (rows.Count == 0)
        {
            return Task.FromResult<object>(new { success = false, error = "CSV appears to be empty" });
        }

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            // headers
            var headers = rows[0];
            for (int col = 0; col < headers.Count; col++)
            {
                worksheet.Cell(1, col + 1).Value = headers[col];
                worksheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                for (int col = 0; col < headers.Count; col++)
                {
                    worksheet.Cell(i + 1, col + 1).Value = col < row.Count ? row[col] : string.Empty;
                }
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(outputPath);
        }

        var info = new FileInfo(outputPath);
        return Task.FromResult<object>(new
        {
            success = true,
            fileName = Path.GetFileName(outputPath),
            filePath = outputPath,
            mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            sizeBytes = info.Length,
            rows = rows.Count - 1,
            downloadUrl = $"/api/files/{Path.GetFileName(outputPath)}"
        });
    }

    private static List<List<string>> ReadCsv(string path, string delimiter)
    {
        var rows = new List<List<string>>();
        using var reader = new StreamReader(path);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var values = line.Split(delimiter);
            rows.Add(values.ToList());
        }
        return rows;
    }
}
