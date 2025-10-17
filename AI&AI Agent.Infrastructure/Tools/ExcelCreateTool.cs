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

/// <summary>
/// Creates an Excel workbook from structured JSON input.
/// </summary>
public sealed class ExcelCreateTool : ITool
{
    public string Name => "ExcelCreate";
    public string Description => "Create an Excel workbook (.xlsx) from structured sheet definitions.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            fileName = new { type = "string", description = "Output file name (must end with .xlsx)" },
            sheets = new
            {
                type = "array",
                description = "Sheet definitions",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Sheet name" },
                        columns = new { type = "array", items = new { type = "string" }, description = "Column headers" },
                        rows = new { type = "array", items = new { type = "array", items = new { type = "string" } }, description = "Row data" }
                    },
                    required = new[] { "name", "columns", "rows" }
                }
            }
        },
        required = new[] { "fileName", "sheets" }
    };

    private readonly string _workspacePath;

    public ExcelCreateTool()
    {
        _workspacePath = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspacePath);
    }

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var fileName = args.GetProperty("fileName").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<object>(new { success = false, error = "fileName must end with .xlsx" });
        }

        if (!args.TryGetProperty("sheets", out var sheetsProp) || sheetsProp.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult<object>(new { success = false, error = "sheets array is required" });
        }

        var workbookPath = Path.Combine(_workspacePath, Path.GetFileName(fileName));

        using var workbook = new XLWorkbook();
        foreach (var sheetElement in sheetsProp.EnumerateArray())
        {
            var sheetName = sheetElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? (nameProp.GetString() ?? "Sheet1")
                : "Sheet1";
            var columns = sheetElement.TryGetProperty("columns", out var columnProp) && columnProp.ValueKind == JsonValueKind.Array
                ? columnProp.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                : new List<string>();
            var rows = sheetElement.TryGetProperty("rows", out var rowProp) && rowProp.ValueKind == JsonValueKind.Array
                ? rowProp.EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()).ToList()
                : new List<List<string>>();

            if (columns.Count == 0)
            {
                return Task.FromResult<object>(new { success = false, error = $"Sheet '{sheetName}' requires at least one column" });
            }

            var worksheet = workbook.Worksheets.Add(sheetName);
            for (int col = 0; col < columns.Count; col++)
            {
                worksheet.Cell(1, col + 1).Value = columns[col];
                worksheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            for (int row = 0; row < rows.Count; row++)
            {
                var dataRow = rows[row];
                for (int col = 0; col < columns.Count; col++)
                {
                    var value = col < dataRow.Count ? dataRow[col] : string.Empty;
                    worksheet.Cell(row + 2, col + 1).Value = value;
                }
            }

            worksheet.Columns().AdjustToContents();
        }

        workbook.SaveAs(workbookPath);
        var info = new FileInfo(workbookPath);
        return Task.FromResult<object>(new
        {
            success = true,
            fileName = Path.GetFileName(workbookPath),
            filePath = workbookPath,
            mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            sizeBytes = info.Length,
            downloadUrl = $"/api/files/{Path.GetFileName(workbookPath)}"
        });
    }
}
