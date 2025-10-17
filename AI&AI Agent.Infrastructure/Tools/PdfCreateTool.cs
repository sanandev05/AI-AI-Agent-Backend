using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class PdfCreateTool : ITool
{
    public string Name => "PdfCreate";
    public string Description => "Creates a simple PDF from provided title and content text.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            content = new { type = "string" },
            fileName = new { type = "string" }
        },
        required = new[] { "content" }
    };

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var title = args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var content = args.GetProperty("content").GetString() ?? string.Empty;
        var fileName = args.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString()! : $"report_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.pdf";
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName += ".pdf";
        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        using var doc = new Document(pdf);
        if (!string.IsNullOrWhiteSpace(title)) doc.Add(new Paragraph(title).SetBold().SetFontSize(16));
        doc.Add(new Paragraph(content));

        return Task.FromResult<object>(new { message = "PDF created", fileName, path, downloadUrl = $"/api/files/{fileName}", sizeBytes = new FileInfo(path).Length });
    }
}
