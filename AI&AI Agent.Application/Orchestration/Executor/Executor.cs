using System.IO;
using AI_AI_Agent.Domain;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Executor;

public sealed class Executor : IExecutor
{
    private readonly IToolRouter _router;
    private readonly IEventBus _bus;
    private readonly IArtifactStore _artifacts;
    private readonly IRunStore _runs;
    private readonly IBudgetManager _budget;
    private readonly ICritic _critic;
    private readonly IApprovalGate _approval;
    public Executor(IToolRouter router, IEventBus bus, IArtifactStore artifacts, IRunStore runs, IBudgetManager budget, ICritic critic, IApprovalGate approval)
    { _router = router; _bus = bus; _artifacts = artifacts; _runs = runs; _budget = budget; _critic = critic; _approval = approval; }

    public async Task ExecuteAsync(Guid runId, Plan plan, CancellationToken ct)
    {
        var (started, _) = await _runs.MarkRunStartAsync(runId);
        await _bus.PublishAsync(new RunStarted(runId, plan.Goal), ct);
        await _bus.PublishAsync(new PlanCreated(runId, plan.Goal, plan.Steps), ct);
        var ctx = new Dictionary<string, object?>();
        foreach (var step in plan.Steps)
        {
            await _runs.MarkStepAsync(runId, step.Id, StepState.Running);
            var stepInputObj = TryDeserialize(step.Input);
            await _bus.PublishAsync(new StepStarted(runId, step.Id, step.Tool, stepInputObj), ct);
            // Approval gate for risky tools (policy defined by IApprovalService)
            if (_approval.RequiresApproval(step.Tool))
            {
                await _bus.PublishAsync(new PermissionRequested(runId, step.Id, step.Tool, stepInputObj), ct);
                var ok = await _approval.WaitForApprovalAsync(runId, step.Id, step.Tool, stepInputObj, ct);
                if (!ok)
                {
                    await _runs.MarkStepAsync(runId, step.Id, StepState.Skipped);
                    await _bus.PublishAsync(new PermissionDenied(runId, step.Id, "Denied by policy/user"), ct);
                    continue;
                }
                await _bus.PublishAsync(new PermissionGranted(runId, step.Id), ct);
            }
            var attempt = 0; Exception? lastEx = null; bool success = false;
            var inputForExecution = step.Input; // allows repair to modify input between attempts
            var triedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Increase attempts for Extract, we may rotate through multiple URLs; allow override via step input
            int maxAttempts;
            try { using var d = System.Text.Json.JsonDocument.Parse(step.Input.GetRawText()); maxAttempts = d.RootElement.TryGetProperty("maxAttempts", out var ma) && ma.ValueKind == System.Text.Json.JsonValueKind.Number ? Math.Clamp(ma.GetInt32(), 1, 10) : (step.Tool.Equals("Browser.Extract", StringComparison.OrdinalIgnoreCase) ? 6 : 2); }
            catch { maxAttempts = step.Tool.Equals("Browser.Extract", StringComparison.OrdinalIgnoreCase) ? 6 : 2; }
            while (attempt < maxAttempts && !success)
            {
                attempt++;
                try
                {
                    using var _ = _budget.Step(runId, step.Id, TimeSpan.FromSeconds(90));
                    // Enforce per-step timeout of 90s
                    using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    stepCts.CancelAfter(TimeSpan.FromSeconds(90));
                    var (payload, artifacts, summary) = await _router.ExecuteAsync(step.Tool, inputForExecution, ctx, stepCts.Token);
                    await _bus.PublishAsync(new ToolOutput(runId, step.Id, summary), ct);
                    // Also stream the payload if it's plainly textual so clients can display the actual answer
                    if (payload is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        // Limit to ~10k chars to avoid flooding
                        var text = s.Length > 10_000 ? s.Substring(0, 10_000) + "…" : s;
                        await _bus.PublishAsync(new ToolOutput(runId, step.Id, text), ct);
                    }
                    else if (payload is not null)
                    {
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(payload);
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                var preview = json.Length > 10_000 ? json.Substring(0, 10_000) + "…" : json;
                                await _bus.PublishAsync(new ToolOutput(runId, step.Id, preview), ct);
                            }
                        }
                        catch { /* ignore serialization issues */ }
                    }
                    // Rough token estimate: ~4 chars per token
                    var estTokens = Math.Max(1, summary?.Length / 4 ?? 1);
                    if (!_budget.SpendTokens(estTokens))
                    {
                        await _bus.PublishAsync(new BudgetExceeded(runId, "tokens", $"Budget exceeded after step {step.Id}"), ct);
                        await _runs.MarkRunEndAsync(runId, DateTime.UtcNow);
                        await _bus.PublishAsync(new RunFailed(runId, "Token budget exceeded"), ct);
                        return;
                    }
                    var savedArtifacts = new List<Artifact>();
                    foreach (var a in artifacts)
                    {
                        var saved = System.IO.File.Exists(a.Path) ? await _artifacts.SaveAsync(runId, step.Id, a.Path, a.FileName, a.MimeType) : a;
                        savedArtifacts.Add(saved);
                        await _bus.PublishAsync(new ArtifactCreated(runId, step.Id, saved), ct);
                        if (!string.IsNullOrWhiteSpace(saved.Path)
                            && saved.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                            && saved.Size <= 2_000_000)
                        {
                            try
                            {
                                var bytes = await File.ReadAllBytesAsync(saved.Path, ct);
                                var base64 = Convert.ToBase64String(bytes);
                                var dataUrl = $"data:{saved.MimeType};base64,{base64}";
                                await _bus.PublishAsync(new ToolOutput(runId, step.Id, dataUrl), ct);
                            }
                            catch
                            {
                                // Ignore streaming errors, artifact event still delivered
                            }
                        }
                    }
                    ctx[$"step:{step.Id}:payload"] = payload;
                    ctx[$"step:{step.Id}:artifacts"] = savedArtifacts;
                    var pass = await _critic.PassAsync(step, payload, ct);
                    if (!pass) throw new InvalidOperationException("Critic rejected result");
                    await _runs.MarkStepAsync(runId, step.Id, StepState.Succeeded);
                    await _bus.PublishAsync(new StepSucceeded(runId, step.Id), ct);
                    success = true;
                }
                catch (Exception ex)
                {
                    lastEx = ex; await _runs.MarkStepAsync(runId, step.Id, StepState.Failed);
                    await _bus.PublishAsync(new StepFailed(runId, step.Id, ex.Message, attempt), ct);

                    // Repair-on-fail: if this was an Extract, try alternate URL from prior search results
                    if (attempt < maxAttempts && step.Tool.Equals("Browser.Extract", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            if (ctx.TryGetValue("search:results", out var sr) && sr is IEnumerable<object> list)
                            {
                                // Determine current URL from the latest input we executed (not the original step input)
                                string? currentUrl = null;
                                try { using var d = System.Text.Json.JsonDocument.Parse(inputForExecution.GetRawText()); if (d.RootElement.TryGetProperty("url", out var u)) currentUrl = u.GetString(); } catch { }
                                if (!string.IsNullOrWhiteSpace(currentUrl)) triedUrls.Add(currentUrl);
                                // Skip problematic domains known for CAPTCHAs
                                var skipDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reddit.com", "www.reddit.com" };
                                var next = list
                                    .Select(obj => System.Text.Json.JsonSerializer.Serialize(obj))
                                    .Select(json => System.Text.Json.JsonDocument.Parse(json).RootElement)
                                    .Select(el => new {
                                        Url = el.TryGetProperty("url", out var u) ? u.GetString() : null,
                                        Domain = el.TryGetProperty("domain", out var d) ? d.GetString() : null
                                    })
                                    .Where(x => !string.IsNullOrWhiteSpace(x.Url)
                                                && !triedUrls.Contains(x.Url!)
                                                && !string.Equals(x.Url, currentUrl, StringComparison.OrdinalIgnoreCase)
                                                && (x.Domain == null || !skipDomains.Contains(x.Domain)))
                                    .Select(x => x.Url)
                                    .FirstOrDefault();
                                if (!string.IsNullOrWhiteSpace(next))
                                {
                                    var patched = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { url = next, selector = "main, article, #content, body", timeoutSec = 30 }));
                                    // overwrite input for retry
                                    inputForExecution = patched.RootElement.Clone();
                                    triedUrls.Add(next);
                                    await _bus.PublishAsync(new ToolOutput(runId, step.Id, $"Repair: switching extract URL to {next}"), ct);
                                    // small backoff to avoid hammering
                                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                                }
                                else
                                {
                                    await _bus.PublishAsync(new ToolOutput(runId, step.Id, "Repair: no alternative URL candidates left"), ct);
                                }
                            }
                        }
                        catch { }
                    }

                    if (attempt >= maxAttempts) break; await Task.Delay(TimeSpan.FromSeconds(Math.Min(4, Math.Pow(2, attempt))), ct);
                }
            }
            if (!success)
            {
                await _runs.MarkRunEndAsync(runId, DateTime.UtcNow);
                await _bus.PublishAsync(new RunFailed(runId, lastEx?.Message ?? "Unknown error"), ct); return;
            }
        }
        await _runs.MarkRunEndAsync(runId, DateTime.UtcNow);
        await _bus.PublishAsync(new RunSucceeded(runId, DateTime.UtcNow - started), ct);
    }

    private static object? TryDeserialize(System.Text.Json.JsonElement el)
    { try { return System.Text.Json.JsonSerializer.Deserialize<object>(el.GetRawText()); } catch { return null; } }
}
