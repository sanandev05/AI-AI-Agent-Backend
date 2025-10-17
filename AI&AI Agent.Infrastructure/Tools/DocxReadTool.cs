using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI_AI_Agent.Infrastructure.Tools;

public class DocxReadTool : ITool
{
    public string Name => "DocxRead";
    public string Description => "Reads a DOCX (local path or URL) and returns extracted text.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local file path or HTTP/HTTPS URL to a .docx file." }
        },
        required = new[] { "path" }
    };

    private readonly string _workspace;
    public DocxReadTool()
    {
        _workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (!args.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            return "Error: 'path' is required";
        var input = p.GetString()!;
        var localPath = input;
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            var fileName = "docx_" + Guid.NewGuid().ToString("N") + ".docx";
            localPath = Path.Combine(_workspace, fileName);
            using var http = new System.Net.Http.HttpClient();
            using var resp = await http.GetAsync(input, cancellationToken);
            resp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs, cancellationToken);
        }
        if (!File.Exists(localPath)) return $"File not found: {localPath}";

        var sb = new StringBuilder();
        using (var doc = WordprocessingDocument.Open(localPath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var para in body.Descendants<Paragraph>())
                {
                    var text = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }
            }
        }
        return new { message = "DOCX text extracted", path = localPath, length = sb.Length, text = sb.ToString() };
    }
}
