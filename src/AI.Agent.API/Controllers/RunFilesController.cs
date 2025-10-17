using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace AI.Agent.API.Controllers;

[ApiController]
[Route("api/runs/{runId:guid}/files")]
public sealed class RunFilesController : ControllerBase
{
    private readonly string _root;
    public RunFilesController()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "storage");
        Directory.CreateDirectory(_root);
    }

    public record RunFile(string FileName, long Size, DateTime CreatedUtc, string DownloadUrl);

    [HttpGet]
    public ActionResult<IEnumerable<RunFile>> List(Guid runId)
    {
        var dir = Path.Combine(_root, runId.ToString());
        if (!Directory.Exists(dir)) return Ok(Array.Empty<RunFile>());
        var files = Directory.GetFiles(dir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(fi => new RunFile(
                fi.Name, fi.Length, fi.CreationTimeUtc,
                Url.ActionLink(nameof(Download), values: new { runId, fileName = fi.Name }) ?? string.Empty))
            .ToList();
        return Ok(files);
    }

    [HttpGet("{fileName}")]
    public IActionResult Download(Guid runId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest("Invalid file name.");
        var dir = Path.Combine(_root, runId.ToString());
        var path = Path.Combine(dir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var contentType = GetContentType(Path.GetExtension(fileName));
        return PhysicalFile(path, contentType, fileName);
    }

    [HttpGet("download/zip")]
    public IActionResult DownloadZip(Guid runId)
    {
        var dir = Path.Combine(_root, runId.ToString());
        if (!Directory.Exists(dir)) return NotFound();
        var tmpZip = Path.Combine(Path.GetTempPath(), $"{runId}.zip");
        if (System.IO.File.Exists(tmpZip)) System.IO.File.Delete(tmpZip);
        ZipFile.CreateFromDirectory(dir, tmpZip, CompressionLevel.Fastest, includeBaseDirectory: false);
        var bytes = System.IO.File.ReadAllBytes(tmpZip);
        return File(bytes, "application/zip", $"{runId}.zip");
    }

    [HttpGet("provenance.json")]
    public IActionResult Provenance(Guid runId)
    {
        var dir = Path.Combine(_root, runId.ToString());
        if (!Directory.Exists(dir)) return NotFound();
        var files = Directory.GetFiles(dir).Select(f => new FileInfo(f)).Select(fi => new
        {
            fi.Name,
            fi.Length,
            fi.CreationTimeUtc,
            Sha256 = HashOf(fi.FullName)
        });
        var doc = new
        {
            runId,
            generatedUtc = DateTime.UtcNow,
            artifacts = files
        };
        var json = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "provenance.json");
    }

    private static string GetContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pdf" => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };

    private static string HashOf(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = System.IO.File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
