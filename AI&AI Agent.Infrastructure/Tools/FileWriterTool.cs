using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class FileWriterTool : ITool
{
    public string Name => "FileWriter";
    public string Description => "Creates TXT or MD report files with structured content and proper formatting.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            fileName = new { type = "string", description = "File name (must end with .txt or .md)" },
            content = new { type = "string", description = "Content to write to the file" },
            format = new { type = "string", description = "Optional format: 'txt' or 'md' (auto-detected from fileName)" }
        },
        required = new[] { "fileName", "content" }
    };

    private readonly string _workspacePath;
    private readonly ILogger<FileWriterTool> _logger;

    public FileWriterTool(ILogger<FileWriterTool> logger)
    {
        _logger = logger;
        _workspacePath = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspacePath);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("fileName", out var fileNameProp) || fileNameProp.ValueKind != JsonValueKind.String)
            return new { error = "fileName is required", success = false };
        
        if (!args.TryGetProperty("content", out var contentProp) || contentProp.ValueKind != JsonValueKind.String)
            return new { error = "content is required", success = false };

        var fileName = fileNameProp.GetString() ?? "";
        var content = contentProp.GetString() ?? "";
        var format = args.TryGetProperty("format", out var formatProp) ? formatProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(fileName))
            return new { error = "fileName cannot be empty", success = false };

        if (string.IsNullOrWhiteSpace(content))
            return new { error = "content cannot be empty", success = false };

        // Validate file extension
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".txt" && extension != ".md")
        {
            return new { error = "fileName must end with .txt or .md", success = false };
        }

        // Auto-detect format from extension if not provided
        if (string.IsNullOrEmpty(format))
        {
            format = extension == ".md" ? "md" : "txt";
        }

        try
        {
            // Ensure safe filename
            var safeFileName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(_workspacePath, safeFileName);

            // Format content based on type
            var formattedContent = FormatContent(content, format);

            // Write file
            await File.WriteAllTextAsync(fullPath, formattedContent, ct);

            var fileInfo = new FileInfo(fullPath);
            var downloadUrl = $"/api/files/{safeFileName}";

            _logger.LogInformation("File created: {FileName} ({Size} bytes)", safeFileName, fileInfo.Length);

            return new
            {
                success = true,
                fileName = safeFileName,
                filePath = fullPath,
                downloadUrl,
                sizeBytes = fileInfo.Length,
                format,
                contentLength = formattedContent.Length,
                message = $"File '{safeFileName}' created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file: {FileName}", fileName);
            return new { error = ex.Message, success = false };
        }
    }

    private string FormatContent(string content, string format)
    {
        if (format.ToLowerInvariant() == "md")
        {
            return FormatAsMarkdown(content);
        }
        
        return FormatAsText(content);
    }

    private string FormatAsText(string content)
    {
        // Basic text formatting: ensure proper line breaks and structure
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var formatted = new System.Text.StringBuilder();

        formatted.AppendLine($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        formatted.AppendLine(new string('=', 50));
        formatted.AppendLine();

        foreach (var line in lines)
        {
            formatted.AppendLine(line);
        }

        formatted.AppendLine();
        formatted.AppendLine(new string('-', 50));
        formatted.AppendLine("End of Report");

        return formatted.ToString();
    }

    private string FormatAsMarkdown(string content)
    {
        // Format as proper Markdown with metadata and structure
        var formatted = new System.Text.StringBuilder();

        formatted.AppendLine("---");
        formatted.AppendLine($"title: Report");
        formatted.AppendLine($"generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        formatted.AppendLine($"format: markdown");
        formatted.AppendLine("---");
        formatted.AppendLine();
        formatted.AppendLine("# Report");
        formatted.AppendLine();

        // Process content to ensure proper Markdown structure
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        bool inCodeBlock = false;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Handle code blocks
            if (trimmedLine.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                formatted.AppendLine(line);
                continue;
            }
            
            if (inCodeBlock)
            {
                formatted.AppendLine(line);
                continue;
            }
            
            // Auto-format headers if not already formatted
            if (!string.IsNullOrEmpty(trimmedLine) && 
                !trimmedLine.StartsWith("#") && 
                !trimmedLine.StartsWith("-") && 
                !trimmedLine.StartsWith("*") && 
                !trimmedLine.Contains(":") &&
                char.IsUpper(trimmedLine[0]) &&
                trimmedLine.Length < 80 &&
                !trimmedLine.EndsWith("."))
            {
                // Likely a section header
                formatted.AppendLine($"## {trimmedLine}");
                formatted.AppendLine();
            }
            else
            {
                formatted.AppendLine(line);
            }
        }

        formatted.AppendLine();
        formatted.AppendLine("---");
        formatted.AppendLine($"*Generated by AI Agent on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

        return formatted.ToString();
    }
}