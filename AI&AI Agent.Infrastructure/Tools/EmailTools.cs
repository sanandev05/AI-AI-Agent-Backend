using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using AI_AI_Agent.Application.Services;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class EmailDraftTool : ITool
{
    public string Name => "EmailDraft";
    public string Description => "Create an email draft (to, subject, body) and a pending approval request.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            to = new { type = "string" },
            subject = new { type = "string" },
            body = new { type = "string" }
        },
        required = new[] { "to", "subject", "body" }
    };

    private readonly IApprovalService _approvals;
    public EmailDraftTool(IApprovalService approvals) { _approvals = approvals; }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var to = args.GetProperty("to").GetString() ?? string.Empty;
        var subject = args.GetProperty("subject").GetString() ?? string.Empty;
        var body = args.GetProperty("body").GetString() ?? string.Empty;
        var payload = JsonSerializer.Serialize(new { to, subject, body });
        var req = await _approvals.CreateAsync("email.send", $"Send email to {to} with subject '{subject}'", payload);
        return new { message = "Draft created; pending approval required before send.", approvalId = req.Id, status = req.Status.ToString() };
    }
}

public sealed class EmailSendTool : ITool
{
    public string Name => "EmailSend";
    public string Description => "Send a previously drafted email after it has been approved. Sandbox writes .eml to workspace.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            approvalId = new { type = "string" }
        },
        required = new[] { "approvalId" }
    };

    private readonly IApprovalService _approvals;
    public EmailSendTool(IApprovalService approvals) { _approvals = approvals; }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var id = args.GetProperty("approvalId").GetString() ?? string.Empty;
        var req = await _approvals.GetAsync(id);
        if (req is null) return "Error: approvalId not found.";
        if (req.Status != ApprovalStatus.Approved) return $"Error: approval status is {req.Status}, must be Approved.";

        var json = await File.ReadAllTextAsync(req.PayloadPath, ct);
        using var doc = JsonDocument.Parse(json);
        var to = doc.RootElement.GetProperty("to").GetString() ?? string.Empty;
        var subject = doc.RootElement.GetProperty("subject").GetString() ?? string.Empty;
        var body = doc.RootElement.GetProperty("body").GetString() ?? string.Empty;

        var dir = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(dir);
        var fileName = $"email_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.eml";
        var path = Path.Combine(dir, fileName);
        var content = $"To: {to}\r\nSubject: {subject}\r\n\r\n{body}";
        await File.WriteAllTextAsync(path, content, ct);
        return new { message = "Email sent (sandbox)", fileName, path, downloadUrl = $"/api/files/{fileName}" };
    }
}
