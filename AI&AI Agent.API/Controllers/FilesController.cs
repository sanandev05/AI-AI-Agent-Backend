using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AI_AI_Agent.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private static readonly string[] AllowedExtensions =
        {
            ".docx", ".png", ".pdf", ".pptx", ".eml", ".ics", ".mp3", ".wav", ".xlsx", ".csv"
        };

        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".png"] = "image/png",
            [".pdf"] = "application/pdf",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".eml"] = "message/rfc822",
            [".ics"] = "text/calendar",
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".csv"] = "text/csv"
        };

        private readonly string _workspacePath;
        public FilesController()
        {
            _workspacePath = Path.Combine(AppContext.BaseDirectory, "workspace");
            if (!Directory.Exists(_workspacePath))
            {
                Directory.CreateDirectory(_workspacePath);
            }
        }

        public class FileItem
        {
            public string Name { get; set; } = string.Empty;
            public long Size { get; set; }
            public DateTime CreatedUtc { get; set; }
            public string DownloadUrl { get; set; } = string.Empty;
        }

        [HttpGet]
        public ActionResult<IEnumerable<FileItem>> List()
        {
            var files = AllowedExtensions
                .SelectMany(ext => Directory.GetFiles(_workspacePath, "*" + ext, SearchOption.TopDirectoryOnly))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Select(CreateFileItem)
                .ToList();
            return Ok(files);
        }

        [HttpGet("{fileName}")]
        public IActionResult Download(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return BadRequest("Invalid file name.");
            }
            var ext = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("File type not allowed.");
            }
            var fullPath = Path.Combine(_workspacePath, fileName);
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }
            var bytes = System.IO.File.ReadAllBytes(fullPath);
            var contentType = MimeTypes.TryGetValue(ext, out var type) ? type : "application/octet-stream";
            return File(bytes, contentType, fileName);
        }

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public ActionResult<FileItem> Upload(IFormFile file)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest("File is required.");
            }

            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return BadRequest("Invalid file name.");
            }

            var ext = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("File type not allowed.");
            }

            var safeName = BuildSafeFileName(fileName);
            var targetPath = Path.Combine(_workspacePath, safeName);

            using (var stream = System.IO.File.Create(targetPath))
            {
                file.CopyTo(stream);
            }

            var info = new FileInfo(targetPath);
            var item = CreateFileItem(info);
            return CreatedAtAction(nameof(Download), new { fileName = item.Name }, item);
        }

        [HttpDelete("{fileName}")]
        public IActionResult Delete(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return BadRequest("Invalid file name.");
            }
            var ext = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("File type not allowed.");
            }
            var fullPath = Path.Combine(_workspacePath, fileName);
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }
            System.IO.File.Delete(fullPath);
            return NoContent();
        }

        private FileItem CreateFileItem(FileInfo info)
        {
            return new FileItem
            {
                Name = info.Name,
                Size = info.Length,
                CreatedUtc = info.CreationTimeUtc,
                DownloadUrl = Url.ActionLink(nameof(Download), values: new { fileName = info.Name }) ?? string.Empty
            };
        }

        private static string BuildSafeFileName(string original)
        {
            var ext = Path.GetExtension(original);
            var baseName = Path.GetFileNameWithoutExtension(original);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return string.IsNullOrWhiteSpace(baseName)
                ? $"upload_{stamp}{ext}"
                : $"{baseName}_{stamp}{ext}";
        }
    }
}
