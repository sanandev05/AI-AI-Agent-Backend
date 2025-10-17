using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class TasksTool : ITool
{
    public string Name => "Tasks";
    public string Description => "Manage simple tasks/to-dos: add, list, complete, delete.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            action = new { type = "string", description = "add | list | complete | delete" },
            title = new { type = "string" },
            id = new { type = "string" }
        },
        required = new[] { "action" }
    };

    private readonly string _path;
    public TasksTool()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "tasks.json");
        if (!File.Exists(_path)) File.WriteAllText(_path, "[]");
    }

    public Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var action = args.GetProperty("action").GetString()?.ToLowerInvariant();
        var list = Load();
        switch (action)
        {
            case "add":
                var title = args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) return Task.FromResult<object>("Error: title required.");
                var item = new TaskItem(Guid.NewGuid().ToString("N"), title!, false, DateTimeOffset.UtcNow);
                list.Add(item);
                Save(list);
                return Task.FromResult<object>(new { message = "Task added", item });
            case "complete":
                {
                    var id = args.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.String ? idp.GetString() : null;
                    var found = list.FirstOrDefault(x => x.Id == id);
                    if (found is null) return Task.FromResult<object>("Error: task not found.");
                    found.Done = true; Save(list);
                    return Task.FromResult<object>(new { message = "Task completed", item = found });
                }
            case "delete":
                {
                    var id = args.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.String ? idp.GetString() : null;
                    var before = list.Count;
                    list = list.Where(x => x.Id != id).ToList();
                    Save(list);
                    return Task.FromResult<object>(new { message = before != list.Count ? "Task deleted" : "No change" });
                }
            case "list":
            default:
                return Task.FromResult<object>(new { tasks = list.OrderBy(x => x.Done).ThenBy(x => x.CreatedAt).ToList() });
        }
    }

    private List<TaskItem> Load()
    {
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new List<TaskItem>();
    }

    private void Save(List<TaskItem> items)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(items));
    }

    public sealed class TaskItem
    {
        public TaskItem(string id, string title, bool done, DateTimeOffset createdAt)
        { Id = id; Title = title; Done = done; CreatedAt = createdAt; }
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Done { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
