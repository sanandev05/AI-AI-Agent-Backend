using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class CalendarCreateTool : ITool
{
    public string Name => "CalendarCreate";
    public string Description => "Create a basic calendar event (ICS file) in the workspace.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            description = new { type = "string" },
            startUtc = new { type = "string", description = "ISO 8601 UTC start" },
            endUtc = new { type = "string", description = "ISO 8601 UTC end" },
            location = new { type = "string" }
        },
        required = new[] { "title", "startUtc", "endUtc" }
    };

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var title = args.GetProperty("title").GetString() ?? string.Empty;
        var description = args.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
        var startStr = args.GetProperty("startUtc").GetString() ?? string.Empty;
        var endStr = args.GetProperty("endUtc").GetString() ?? string.Empty;
        var location = args.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? string.Empty : string.Empty;
        if (!DateTimeOffset.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start) ||
            !DateTimeOffset.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end))
        {
            return Task.FromResult<object>("Error: invalid startUtc/endUtc.");
        }
        var ics = BuildIcs(title, description, start, end, location);
        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        var fileName = $"event_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.ics";
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, ics, Encoding.UTF8);
        return Task.FromResult<object>(new { message = "Event created", fileName, path, downloadUrl = $"/api/files/{fileName}" });
    }

    private static string BuildIcs(string title, string description, DateTimeOffset start, DateTimeOffset end, string location)
    {
        string dt(DateTimeOffset t) => t.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//AI&AI Agent//EN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{Guid.NewGuid():N}@ai-agent");
        sb.AppendLine($"DTSTAMP:{dt(DateTimeOffset.UtcNow)}");
        sb.AppendLine($"DTSTART:{dt(start)}");
        sb.AppendLine($"DTEND:{dt(end)}");
        sb.AppendLine($"SUMMARY:{title}");
        if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine($"DESCRIPTION:{description}");
        if (!string.IsNullOrWhiteSpace(location)) sb.AppendLine($"LOCATION:{location}");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }
}

public sealed class CalendarListTool : ITool
{
    public string Name => "CalendarList";
    public string Description => "List existing ICS files in workspace.";
    public object JsonSchema => new { type = "object", properties = new { }, required = Array.Empty<string>() };

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        var files = Directory.GetFiles(dir, "*.ics").Select(f => new { name = Path.GetFileName(f), path = f, downloadUrl = $"/api/files/{Path.GetFileName(f)}" }).ToList();
        return Task.FromResult<object>(new { files });
    }
}
