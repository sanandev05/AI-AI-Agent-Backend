using AI_AI_Agent.Application.Agent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.IO;

namespace AI_AI_Agent.Infrastructure.Tools;

public class DocxCreateTool : ITool
{
    public string Name => "DocxCreate";
    private readonly string _workspacePath;

    public DocxCreateTool()
    {
        _workspacePath = Path.Combine(AppContext.BaseDirectory, "workspace");
        if (!Directory.Exists(_workspacePath))
        {
            Directory.CreateDirectory(_workspacePath);
        }
    }

    [KernelFunction, Description("Creates a DOCX file with a title and content in the agent's workspace.")]
    public (string filePath, long size) CreateDocument(
        [Description("The file name for the document, e.g., 'report.docx'.")] string fileName,
        [Description("The main content/body of the document.")] string content,
        [Description("The title of the document.")] string title = "Report")
    {
        var filePath = Path.Combine(_workspacePath, fileName);

        using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Helper local functions
            static Paragraph CreateTitleParagraph(string text)
            {
                var runProps = new RunProperties(
                    new Bold(),
                    new FontSize { Val = "32" } // 16pt
                );
                var run = new Run(runProps, new Text(text));
                var paraProps = new ParagraphProperties();
                return new Paragraph(paraProps, run);
            }

            static Paragraph CreateHeadingParagraph(string text, int level)
            {
                // Map level to font size (h1..h6 style-ish)
                string size = level switch
                {
                    1 => "28", // 14pt
                    2 => "26",
                    3 => "24",
                    4 => "22",
                    5 => "20",
                    _ => "20"
                };
                var runProps = new RunProperties(new Bold(), new FontSize { Val = size });
                var run = new Run(runProps, new Text(text));
                return new Paragraph(run);
            }

            static Paragraph CreateParagraph(string text)
            {
                return new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            static Paragraph CreateBulletParagraph(string text)
            {
                // Simple bullet using a leading bullet character
                var runProps = new RunProperties(new FontSize { Val = "20" });
                var bulletRun = new Run(runProps, new Text("• "));
                var textRun = new Run(new Text(text));
                return new Paragraph(bulletRun, textRun);
            }

            // Title
            body.AppendChild(CreateTitleParagraph(title));

            if (!string.IsNullOrWhiteSpace(content))
            {
                // Normalize newlines and split into logical blocks (double newline = new paragraph block / section)
                var normalized = content.Replace("\r\n", "\n");
                var blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.None);
                foreach (var block in blocks)
                {
                    var trimmedBlock = block.TrimEnd();
                    if (string.IsNullOrWhiteSpace(trimmedBlock)) continue;

                    // Support simple markdown-style headings (#, ##, ###)
                    if (trimmedBlock.StartsWith("#"))
                    {
                        int level = trimmedBlock.TakeWhile(c => c == '#').Count();
                        level = Math.Clamp(level, 1, 6);
                        var headingText = trimmedBlock.Substring(level).TrimStart();
                        body.AppendChild(CreateHeadingParagraph(headingText, level));
                        continue;
                    }

                    // Support simple bullet lists: lines all beginning with - or *
                    var lines = trimmedBlock.Split('\n');
                    bool isList = lines.All(l => l.TrimStart().StartsWith("- ") || l.TrimStart().StartsWith("* "));
                    if (isList)
                    {
                        foreach (var rawLine in lines)
                        {
                            var line = rawLine.TrimStart();
                            var itemText = line.StartsWith("- ") || line.StartsWith("* ") ? line.Substring(2) : line;
                            body.AppendChild(CreateBulletParagraph(itemText));
                        }
                        continue;
                    }

                    // Regular paragraph (preserve internal single newlines by splitting)
                    if (lines.Length == 1)
                    {
                        body.AppendChild(CreateParagraph(lines[0]));
                    }
                    else
                    {
                        foreach (var l in lines)
                        {
                            var t = l.TrimEnd();
                            if (t.Length == 0) { body.AppendChild(new Paragraph()); continue; }
                            body.AppendChild(CreateParagraph(t));
                        }
                    }
                }
            }
        }
        var fi = new FileInfo(filePath);
        return (filePath, fi.Exists ? fi.Length : 0);
    }

    // Unified ITool contract: allow function-call style execution
    public Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        string fileName = args.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String
            ? f.GetString()!
            : $"doc_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.docx";

        string content = args.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()!
            : string.Empty;

        string title = args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()!
            : "Report";

        var (filePath, size) = CreateDocument(fileName, content, title);
        var payload = new
        {
            fileName = fileName,
            title = title,
            sizeBytes = size,
            path = filePath,
            downloadUrl = $"/api/files/{fileName}",
            message = "DOCX created"
        };
        return Task.FromResult<object>(payload);
    }
}
