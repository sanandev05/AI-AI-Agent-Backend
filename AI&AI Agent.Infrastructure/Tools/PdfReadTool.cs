using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace AI_AI_Agent.Infrastructure.Tools;

public class PdfReadTool : ITool
{
    public string Name => "PdfReader";
    public string Description => "Reads a PDF from local path or URL. If text extraction fails (< 200 chars), falls back to screenshot of first 2 pages.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local file path or HTTP/HTTPS URL to a PDF." },
            maxPages = new { type = "number", description = "Optional max pages to read (default: all)." }
        },
        required = new[] { "path" }
    };

    private readonly string _workspace;
    private readonly IBrowser _browser;
    private readonly ILogger<PdfReadTool> _logger;

    public PdfReadTool(IBrowser browser, ILogger<PdfReadTool> logger)
    {
        _browser = browser;
        _logger = logger;
        _workspace = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspace);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (!args.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            return new { error = "Path is required", success = false };
        
        var input = p.GetString()!;
        int maxPages = args.TryGetProperty("maxPages", out var mp) && mp.ValueKind == JsonValueKind.Number ? mp.GetInt32() : 10;

        var localPath = input;

        try
        {
            // Download PDF if it's a URL
            if (input.StartsWith("http://") || input.StartsWith("https://"))
            {
                var fileName = "pdf_" + Guid.NewGuid().ToString("N") + ".pdf";
                localPath = Path.Combine(_workspace, fileName);
                using var http = new System.Net.Http.HttpClient();
                using var resp = await http.GetAsync(input, cancellationToken);
                resp.EnsureSuccessStatusCode();
                await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await resp.Content.CopyToAsync(fs, cancellationToken);
            }

            if (!File.Exists(localPath)) 
                return new { error = $"File not found: {localPath}", success = false };

            // Try text extraction first
            var extractedText = await ExtractTextAsync(localPath, maxPages);
            
            if (extractedText.Length >= 200)
            {
                return new { 
                    success = true,
                    method = "text_extraction",
                    path = localPath,
                    textLength = extractedText.Length,
                    text = extractedText,
                    message = "PDF text extracted successfully"
                };
            }

            // Text extraction failed or insufficient - use screenshot fallback
            _logger.LogWarning("PDF text extraction insufficient ({Length} chars), falling back to screenshots", extractedText.Length);
            
            var screenshots = await CreatePdfScreenshotsAsync(localPath, Math.Min(maxPages, 2));
            
            return new { 
                success = true,
                method = "screenshot_fallback",
                path = localPath,
                textLength = extractedText.Length,
                text = extractedText,
                screenshots = screenshots,
                message = "PDF text insufficient, created page screenshots for visual analysis",
                issue = "Handling file reading issue"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process PDF: {Path}", input);
            return new { error = ex.Message, success = false, path = localPath };
        }
    }

    private Task<string> ExtractTextAsync(string pdfPath, int maxPages)
    {
        return Task.Run(() =>
        {
            try
            {
                using var pdf = new PdfDocument(new PdfReader(pdfPath));
                var text = new System.Text.StringBuilder();
                int total = pdf.GetNumberOfPages();
                int limit = Math.Min(maxPages, total);
                
                for (int i = 1; i <= limit; i++)
                {
                    var strategy = new SimpleTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), strategy);
                    text.AppendLine(pageText);
                }
                
                return text.ToString().Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Text extraction failed for PDF: {Path}", pdfPath);
                return "";
            }
        });
    }

    private async Task<List<object>> CreatePdfScreenshotsAsync(string pdfPath, int maxPages)
    {
        var screenshots = new List<object>();
        
        try
        {
            var context = await _browser.NewContextAsync();
            var page = await context.NewPageAsync();
            
            for (int pageNum = 1; pageNum <= maxPages; pageNum++)
            {
                try
                {
                    // Convert PDF page to data URL and take screenshot
                    var fileName = $"pdf_page_{Guid.NewGuid():N}_{pageNum}.png";
                    var screenshotPath = Path.Combine(_workspace, fileName);
                    
                    // Use PDF.js viewer with the file
                    var pdfUrl = "file:///" + pdfPath.Replace('\\', '/');
                    await page.GotoAsync($"https://mozilla.github.io/pdf.js/web/viewer.html?file={Uri.EscapeDataString(pdfUrl)}#page={pageNum}", 
                        new PageGotoOptions { Timeout = 30000 });
                    
                    // Wait for PDF to load
                    await page.WaitForSelectorAsync("#viewer .page", new PageWaitForSelectorOptions { Timeout = 15000 });
                    await page.WaitForTimeoutAsync(2000); // Allow render time
                    
                    await page.ScreenshotAsync(new PageScreenshotOptions 
                    { 
                        Path = screenshotPath,
                        FullPage = false,
                        Clip = new Clip { X = 0, Y = 0, Width = 800, Height = 1000 }
                    });
                    
                    screenshots.Add(new { 
                        pageNumber = pageNum,
                        fileName = fileName,
                        filePath = screenshotPath,
                        downloadUrl = $"/api/files/{fileName}",
                        sizeBytes = new FileInfo(screenshotPath).Length
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to screenshot PDF page {Page}", pageNum);
                }
            }
            
            await page.CloseAsync();
            await context.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PDF screenshots");
        }
        
        return screenshots;
    }
}
