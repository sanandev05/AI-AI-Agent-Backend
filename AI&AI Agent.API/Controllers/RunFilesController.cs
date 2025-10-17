using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AI_AI_Agent.Infrastructure.Orchestration.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AI_AI_Agent.API.Controllers;

[ApiController]
[Route("api/runs/{runId}/files")]
public sealed class RunFilesController : ControllerBase
{
    private readonly FileArtifactStore _store;
    public RunFilesController(FileArtifactStore store) { _store = store; }

    private string GetRunDir(Guid runId) => Path.Combine(AppContext.BaseDirectory, "storage", runId.ToString());

    public sealed record FileItem(string Name, long Size, string MimeType, DateTime CreatedUtc, string DownloadUrl);

    [HttpGet]
    public IActionResult List([FromRoute] Guid runId)
    {
        var dir = GetRunDir(runId);
        if (!Directory.Exists(dir)) return Ok(Array.Empty<FileItem>());
        var items = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .Where(fi => !string.Equals(fi.Extension, ".json", StringComparison.OrdinalIgnoreCase) || !fi.Name.Equals("provenance.json", StringComparison.OrdinalIgnoreCase))
            .Select(fi => new FileItem(
                fi.Name,
                fi.Length,
                MimeFromExt(fi.Extension),
                fi.CreationTimeUtc,
                Url.ActionLink(nameof(Download), values: new { runId, fileName = fi.Name }) ?? string.Empty
            ))
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();
        return Ok(items);
    }

    [HttpGet("{fileName}")]
    public IActionResult Download([FromRoute] Guid runId, [FromRoute] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return BadRequest("invalid file name");
        var dir = GetRunDir(runId);
        var path = Path.Combine(dir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, MimeFromExt(Path.GetExtension(path)), fileName);
    }

    [HttpGet("zip")] // GET /api/runs/{runId}/files/zip
    public IActionResult Zip([FromRoute] Guid runId)
    {
        var dir = GetRunDir(runId);
        if (!Directory.Exists(dir)) return NotFound();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);
                using var es = entry.Open();
                using var fs = System.IO.File.OpenRead(file);
                fs.CopyTo(es);
            }
        }
        ms.Position = 0;
        return File(ms.ToArray(), "application/zip", $"run_{runId}.zip");
    }

    [HttpGet("provenance")] // GET /api/runs/{runId}/files/provenance
    public IActionResult Provenance([FromRoute] Guid runId)
    {
        var dir = GetRunDir(runId);
        if (!Directory.Exists(dir)) return NotFound();
        var items = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Sha256 = Sha256Of(path), Size = new FileInfo(path).Length })
            .ToArray();
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        return Content(json, "application/json");
    }

    private static string MimeFromExt(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
    }

    private static string Sha256Of(string path)
    {
        using var sha = SHA256.Create();
        using var fs = System.IO.File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
