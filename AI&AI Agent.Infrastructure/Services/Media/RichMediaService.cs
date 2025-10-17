using AI_AI_Agent.Domain.Streaming;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace AI_AI_Agent.Infrastructure.Services.Media
{
    /// <summary>
    /// Rich media service for file uploads, attachments, and content processing
    /// </summary>
    public class RichMediaService
    {
        private readonly ILogger<RichMediaService> _logger;
        private readonly string _storagePath;
        private readonly HashSet<string> _allowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"
        };
        private readonly HashSet<string> _allowedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".txt", ".md", ".csv", ".xlsx", ".pptx"
        };
        private readonly long _maxFileSize = 50 * 1024 * 1024; // 50MB

        public RichMediaService(ILogger<RichMediaService> logger, string storagePath = "uploads")
        {
            _logger = logger;
            _storagePath = storagePath;
            EnsureStorageDirectoryExists();
        }

        private void EnsureStorageDirectoryExists()
        {
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                _logger.LogInformation("Created storage directory at {Path}", _storagePath);
            }
        }

        #region File Upload

        /// <summary>
        /// Upload and process image file
        /// </summary>
        public async Task<MediaAttachment> UploadImageAsync(
            string fileName,
            byte[] fileData,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(fileName);
            if (!_allowedImageTypes.Contains(extension))
            {
                throw new InvalidOperationException($"File type {extension} not allowed for images");
            }

            if (fileData.Length > _maxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds maximum of {_maxFileSize / 1024 / 1024}MB");
            }

            var attachment = new MediaAttachment
            {
                FileName = fileName,
                ContentType = GetContentType(extension),
                Size = fileData.Length
            };

            // Save file
            var filePath = Path.Combine(_storagePath, $"{attachment.Id}{extension}");
            await File.WriteAllBytesAsync(filePath, fileData, cancellationToken);
            attachment.StoragePath = filePath;

            // Generate thumbnail
            attachment.ThumbnailPath = await GenerateThumbnailAsync(filePath, attachment.Id, cancellationToken);

            // Extract metadata
            attachment.Metadata["width"] = 0; // Would extract from image
            attachment.Metadata["height"] = 0;
            attachment.Metadata["format"] = extension;

            _logger.LogInformation("Uploaded image {FileName} ({Size} bytes)", fileName, fileData.Length);
            return attachment;
        }

        /// <summary>
        /// Upload and process document file
        /// </summary>
        public async Task<MediaAttachment> UploadDocumentAsync(
            string fileName,
            byte[] fileData,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(fileName);
            if (!_allowedDocumentTypes.Contains(extension))
            {
                throw new InvalidOperationException($"File type {extension} not allowed for documents");
            }

            if (fileData.Length > _maxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds maximum of {_maxFileSize / 1024 / 1024}MB");
            }

            var attachment = new MediaAttachment
            {
                FileName = fileName,
                ContentType = GetContentType(extension),
                Size = fileData.Length
            };

            // Save file
            var filePath = Path.Combine(_storagePath, $"{attachment.Id}{extension}");
            await File.WriteAllBytesAsync(filePath, fileData, cancellationToken);
            attachment.StoragePath = filePath;

            // Extract metadata
            attachment.Metadata["pages"] = 0; // Would extract from document
            attachment.Metadata["format"] = extension;

            _logger.LogInformation("Uploaded document {FileName} ({Size} bytes)", fileName, fileData.Length);
            return attachment;
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".csv" => "text/csv",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/octet-stream"
            };
        }

        #endregion

        #region Image Processing

        /// <summary>
        /// Generate thumbnail for image
        /// </summary>
        private async Task<string> GenerateThumbnailAsync(
            string imagePath,
            string attachmentId,
            CancellationToken cancellationToken = default)
        {
            // In real implementation, would use image processing library
            // For now, just return placeholder path
            var thumbnailPath = Path.Combine(_storagePath, $"{attachmentId}_thumb.jpg");
            _logger.LogDebug("Generated thumbnail for {ImagePath}", imagePath);
            await Task.CompletedTask;
            return thumbnailPath;
        }

        /// <summary>
        /// Process image for AI analysis
        /// </summary>
        public async Task<Dictionary<string, object>> AnalyzeImageAsync(
            string attachmentId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Analyzing image {AttachmentId}", attachmentId);

            // In real implementation, would use vision AI
            var analysis = new Dictionary<string, object>
            {
                ["objects"] = new List<string> { "placeholder" },
                ["text"] = "Extracted text would appear here",
                ["colors"] = new List<string> { "#000000" },
                ["confidence"] = 0.95
            };

            await Task.CompletedTask;
            return analysis;
        }

        #endregion

        #region Document Processing

        /// <summary>
        /// Extract text from document
        /// </summary>
        public async Task<string> ExtractDocumentTextAsync(
            string attachmentId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Extracting text from document {AttachmentId}", attachmentId);

            // In real implementation, would use document parsing library
            var extractedText = "Document text would be extracted here";
            
            await Task.CompletedTask;
            return extractedText;
        }

        #endregion

        #region Artifact Management

        /// <summary>
        /// Create artifact preview
        /// </summary>
        public async Task<string> CreateArtifactPreviewAsync(
            MediaAttachment attachment,
            CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(attachment.FileName);

            if (_allowedImageTypes.Contains(extension))
            {
                return await CreateImagePreviewAsync(attachment, cancellationToken);
            }
            else if (_allowedDocumentTypes.Contains(extension))
            {
                return await CreateDocumentPreviewAsync(attachment, cancellationToken);
            }

            return "No preview available";
        }

        private async Task<string> CreateImagePreviewAsync(
            MediaAttachment attachment,
            CancellationToken cancellationToken)
        {
            // Return HTML preview
            var html = $@"
                <div class='image-preview'>
                    <img src='{attachment.StoragePath}' alt='{attachment.FileName}' />
                    <p>Size: {FormatFileSize(attachment.Size)}</p>
                </div>";
            
            await Task.CompletedTask;
            return html;
        }

        private async Task<string> CreateDocumentPreviewAsync(
            MediaAttachment attachment,
            CancellationToken cancellationToken)
        {
            // Return HTML preview
            var html = $@"
                <div class='document-preview'>
                    <h3>{attachment.FileName}</h3>
                    <p>Type: {attachment.ContentType}</p>
                    <p>Size: {FormatFileSize(attachment.Size)}</p>
                    <button onclick='downloadArtifact(""{attachment.Id}"")'>Download</button>
                </div>";
            
            await Task.CompletedTask;
            return html;
        }

        /// <summary>
        /// Get artifact download URL
        /// </summary>
        public string GetDownloadUrl(string attachmentId)
        {
            return $"/api/media/download/{attachmentId}";
        }

        #endregion

        #region Code Syntax Highlighting

        /// <summary>
        /// Apply syntax highlighting to code
        /// </summary>
        public string HighlightCode(string code, string language)
        {
            _logger.LogDebug("Applying syntax highlighting for {Language}", language);

            // In real implementation, would use syntax highlighting library
            // For now, wrap in pre/code tags with language class
            var highlighted = $@"
                <pre class='code-block'>
                    <code class='language-{language.ToLower()}'>{EscapeHtml(code)}</code>
                </pre>";

            return highlighted;
        }

        /// <summary>
        /// Detect programming language from code
        /// </summary>
        public string DetectLanguage(string code)
        {
            // Simple heuristic-based detection
            if (code.Contains("def ") || code.Contains("import ")) return "python";
            if (code.Contains("function ") || code.Contains("const ")) return "javascript";
            if (code.Contains("class ") && code.Contains("public")) return "csharp";
            if (code.Contains("<html") || code.Contains("</div>")) return "html";
            if (code.Contains("SELECT ") || code.Contains("FROM ")) return "sql";
            
            return "text";
        }

        #endregion

        #region Utilities

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string EscapeHtml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        /// <summary>
        /// Delete attachment
        /// </summary>
        public async Task DeleteAttachmentAsync(string attachmentId)
        {
            var pattern = Path.Combine(_storagePath, $"{attachmentId}*");
            var files = Directory.GetFiles(_storagePath, $"{attachmentId}*");
            
            foreach (var file in files)
            {
                File.Delete(file);
            }

            _logger.LogInformation("Deleted attachment {AttachmentId}", attachmentId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var files = Directory.GetFiles(_storagePath);
            var totalSize = files.Sum(f => new FileInfo(f).Length);

            return new Dictionary<string, object>
            {
                ["totalFiles"] = files.Length,
                ["totalSize"] = FormatFileSize(totalSize),
                ["storagePath"] = _storagePath,
                ["maxFileSize"] = FormatFileSize(_maxFileSize),
                ["allowedImageTypes"] = _allowedImageTypes.Count,
                ["allowedDocumentTypes"] = _allowedDocumentTypes.Count
            };
        }

        #endregion
    }
}
