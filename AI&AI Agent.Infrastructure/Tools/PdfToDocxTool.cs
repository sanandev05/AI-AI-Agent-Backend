using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Converts a PDF document to a DOCX file by extracting text and writing it into a Word document.
/// </summary>
public sealed class PdfToDocxTool : ITool
{
    public string Name => "PdfToDocx";
    public string Description => "Convert a PDF file to DOCX (text only).";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local path or HTTP/HTTPS URL to the PDF" },
            fileName = new { type = "string", description = "Optional output file name (default pdf_to_docx_TIMESTAMP.docx)" },
            maxPages = new { type = "number", description = "Maximum pages to extract (default 20)" }
        },
        required = new[] { "path" }
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public PdfToDocxTool(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
        {
            return new { success = false, error = "path is required" };
        }

        var input = pathProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return new { success = false, error = "path cannot be empty" };
        }

        var fileName = args.TryGetProperty("fileName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String
            ? (fnProp.GetString() ?? string.Empty)
            : string.Empty;
        int maxPages = args.TryGetProperty("maxPages", out var mpProp) && mpProp.ValueKind == JsonValueKind.Number ? mpProp.GetInt32() : 20;

        try
        {
            var pdfPath = await EnsureLocalPdfAsync(input, ct);
            if (pdfPath is null || !File.Exists(pdfPath))
            {
                return new { success = false, error = "Unable to access PDF" };
            }

            var text = ExtractText(pdfPath, maxPages);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new { success = false, error = "No text extracted from PDF" };
            }

            var docxFileName = string.IsNullOrWhiteSpace(fileName)
                ? $"pdf_to_docx_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.docx"
                : (fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".docx");
            var outputPath = Path.Combine(AppContext.BaseDirectory, "workspace", docxFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            WriteDocx(outputPath, text, Path.GetFileNameWithoutExtension(pdfPath));
            var info = new FileInfo(outputPath);

            return new
            {
                success = true,
                fileName = docxFileName,
                filePath = outputPath,
                mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                sizeBytes = info.Length,
                downloadUrl = $"/api/files/{docxFileName}",
                extractedCharacters = text.Length
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<string?> EnsureLocalPdfAsync(string input, CancellationToken ct)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(input, ct);
            response.EnsureSuccessStatusCode();
            var tempFile = Path.Combine(Path.GetTempPath(), $"pdf_{Guid.NewGuid():N}.pdf");
            await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, ct);
            return tempFile;
        }

        return File.Exists(input) ? input : null;
    }

    private static string ExtractText(string pdfPath, int maxPages)
    {
        var sb = new StringBuilder();
        using var pdf = new PdfDocument(new PdfReader(pdfPath));
        int total = pdf.GetNumberOfPages();
        int limit = Math.Min(Math.Max(1, maxPages), total);
        for (int i = 1; i <= limit; i++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), strategy);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText.Trim());
            }
        }
        return sb.ToString().Trim();
    }

    private static void WriteDocx(string outputPath, string text, string originalBaseName)
    {
        using var doc = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        // Title paragraph (original PDF base name)
        var titleRunProps = new RunProperties(new Bold(), new FontSize { Val = "32" }); // 16pt
        var titlePara = new Paragraph(new Run(titleRunProps, new Text(originalBaseName)));
        body.AppendChild(titlePara);

        // Split into blocks by double newline to form paragraphs/sections
        var normalized = text.Replace("\r\n", "\n");
        var blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.None);

        Paragraph CreateHeading(string heading, int level)
        {
            string size = level switch
            {
                1 => "28",
                2 => "26",
                3 => "24",
                4 => "22",
                5 => "20",
                _ => "20"
            };
            var rp = new RunProperties(new Bold(), new FontSize { Val = size });
            return new Paragraph(new Run(rp, new Text(heading)));
        }

        Paragraph CreateParagraph(string line)
        {
            var textEl = new Text(line) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve };
            return new Paragraph(new Run(textEl));
        }

        Paragraph CreateBullet(string item)
        {
            var bulletRun = new Run(new Text("• "));
            var textRun = new Run(new Text(item));
            return new Paragraph(bulletRun, textRun);
        }

        foreach (var block in blocks)
        {
            var trimmed = block.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Heading detection (# style)
            if (trimmed.StartsWith('#'))
            {
                int level = trimmed.TakeWhile(c => c == '#').Count();
                level = Math.Clamp(level, 1, 6);
                var headingText = trimmed.Substring(level).TrimStart();
                body.AppendChild(CreateHeading(headingText, level));
                continue;
            }

            var lines = trimmed.Split('\n');
            bool isList = lines.All(l => l.TrimStart().StartsWith("- ") || l.TrimStart().StartsWith("* "));
            if (isList)
            {
                foreach (var raw in lines)
                {
                    var l = raw.TrimStart();
                    var itemText = l.StartsWith("- ") || l.StartsWith("* ") ? l.Substring(2) : l;
                    body.AppendChild(CreateBullet(itemText));
                }
                continue;
            }

            if (lines.Length == 1)
            {
                body.AppendChild(CreateParagraph(lines[0]));
            }
            else
            {
                foreach (var l in lines)
                {
                    var t = l.TrimEnd();
                    if (t.Length == 0)
                    {
                        body.AppendChild(new Paragraph());
                        continue;
                    }
                    body.AppendChild(CreateParagraph(t));
                }
            }
        }

        mainPart.Document.AppendChild(body);
        mainPart.Document.Save();
    }
}
