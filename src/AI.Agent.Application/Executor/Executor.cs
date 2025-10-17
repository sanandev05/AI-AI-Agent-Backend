using System.Text.Json;
using AI.Agent.Domain;
using AI.Agent.Domain.Events;

namespace AI.Agent.Application.Executor;

public sealed class Executor : IExecutor
{
    private readonly IToolRouter _router;
    private readonly IEventBus _bus;
    private readonly IArtifactStore _artifacts;
    private readonly IRunStore _runs;
    private readonly IBudgetManager _budget;
    private readonly ICritic _critic;
    private readonly IApprovalGate _approval;

    private static readonly HashSet<string> RiskyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Browser.Click","Browser.Type","Browser.Submit","Browser.Goto","Docx.Create","Email.Send","Calendar.Create"
    };

    public Executor(IToolRouter router, IEventBus bus, IArtifactStore artifacts, IRunStore runs, IBudgetManager budget, ICritic critic, IApprovalGate approval)
    {
        _router = router; _bus = bus; _artifacts = artifacts; _runs = runs; _budget = budget; _critic = critic; _approval = approval;
    }

    // Backward compatibility for callers/tests not providing approval gate
    public Executor(IToolRouter router, IEventBus bus, IArtifactStore artifacts, IRunStore runs, IBudgetManager budget, ICritic critic)
        : this(router, bus, artifacts, runs, budget, critic, new InMemoryApprovalGate())
    { }

    public async Task ExecuteAsync(Guid runId, Plan plan, CancellationToken ct)
    {
        var (started, _) = await _runs.MarkRunStartAsync(runId);
        await _bus.PublishAsync(new RunStarted(runId, plan.Goal), ct);

        // Send the plan to Agent Hub before execution begins
        await NarratePlanAsync(runId, plan, ct);

        var ctx = new Dictionary<string, object?>();
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            await _runs.MarkStepAsync(runId, step.Id, StepState.Running);
            
            // Narrate what we're about to do
            await NarrateStepStartAsync(runId, step, i + 1, plan.Steps.Count, ct);
            await _bus.PublishAsync(new StepStarted(runId, step.Id, step.Tool, TryDeserialize(step.Input)), ct);

            // Approval for risky tools
            if (RiskyTools.Contains(step.Tool))
            {
                await _bus.PublishAsync(new PermissionRequested(runId, step.Id, step.Tool, TryDeserialize(step.Input)), ct);
                var approved = await _approval.WaitAsync(runId, step.Id, ct);
                if (!approved)
                {
                    await _runs.MarkStepAsync(runId, step.Id, StepState.Skipped);
                    await _bus.PublishAsync(new PermissionDenied(runId, step.Id, "Denied by user"), ct);
                    continue;
                }
                await _bus.PublishAsync(new PermissionGranted(runId, step.Id), ct);
            }

            var attempt = 0;
            var success = false;
            Exception? lastEx = null;
            while (attempt < 3 && !success)
            {
                attempt++;
                try
                {
                    using var _ = _budget.Step(runId, step.Id, TimeSpan.FromSeconds(120)); // Longer timeout for screenshots

                    // Narrate the execution attempt
                    if (attempt > 1)
                    {
                        await NarrateRetryAsync(runId, step, attempt, lastEx?.Message, ct);
                    }

                    var start = DateTime.UtcNow;
                    var (payload, artifacts, summary) = await _router.ExecuteAsync(step.Tool, step.Input, ctx, ct);
                    var duration = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                    
                    // Enhanced narration from tool output
                    await NarrateToolOutputAsync(runId, step, payload, summary, ct);
                    await _bus.PublishAsync(new ToolOutput(runId, step.Id, summary), ct);

                    // Stream payload text or JSON preview for UI visibility
                    try
                    {
                        if (payload is string s && !string.IsNullOrWhiteSpace(s))
                        {
                            await _bus.PublishAsync(new ToolOutput(runId, step.Id, s.Length > 2000 ? s.Substring(0, 2000) + "…" : s), ct);
                        }
                        else if (payload is not null)
                        {
                            var json = JsonSerializer.Serialize(payload);
                            var preview = json.Length > 2000 ? json.Substring(0, 2000) + "…" : json;
                            await _bus.PublishAsync(new ToolOutput(runId, step.Id, preview), ct);
                        }
                    }
                    catch { /* non-fatal */ }

                    var savedArtifacts = new List<Artifact>();
                    foreach (var a in artifacts)
                    {
                        // If tool produced temp files, persist to artifact store under /storage/{runId}
                        if (File.Exists(a.Path))
                        {
                            var saved = await _artifacts.SaveAsync(runId, step.Id, a.Path, a.FileName, a.MimeType);
                            await _bus.PublishAsync(new ArtifactCreated(runId, step.Id, saved), ct);
                            savedArtifacts.Add(saved);
                            
                            // Narrate artifact creation
                            await NarrateArtifactAsync(runId, step, saved, ct);
                        }
                        else
                        {
                            await _bus.PublishAsync(new ArtifactCreated(runId, step.Id, a), ct);
                            savedArtifacts.Add(a);
                            await NarrateArtifactAsync(runId, step, a, ct);
                        }
                    }

                    // Store step results in context
                    ctx[$"step:{step.Id}:payload"] = payload;
                    if (savedArtifacts.Count > 0)
                    {
                        ctx[$"step:{step.Id}:artifacts"] = savedArtifacts;
                    }

                    // Metrics
                    ctx[$"step:{step.Id}:metrics"] = new { durationMs = duration };

                    // Critic evaluation with narration
                    await NarrateCriticEvaluationAsync(runId, step, ct);
                    var pass = await _critic.PassAsync(step, payload, ct);
                    if (!pass)
                    {
                        await NarrateCriticRejectionAsync(runId, step, ct);
                        throw new InvalidOperationException("Critic rejected result");
                    }

                    await NarrateStepSuccessAsync(runId, step, savedArtifacts.Count, ct);
                    await _runs.MarkStepAsync(runId, step.Id, StepState.Succeeded);
                    await _bus.PublishAsync(new StepSucceeded(runId, step.Id), ct);
                    success = true;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    await _runs.MarkStepAsync(runId, step.Id, StepState.Failed);
                    await NarrateStepFailureAsync(runId, step, ex, attempt, ct);
                    await _bus.PublishAsync(new StepFailed(runId, step.Id, ex.Message, attempt), ct);
                    if (attempt >= 3) break;
                    
                    // Narrate retry delay
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await NarrateRetryDelayAsync(runId, step, delay, ct);
                    await Task.Delay(delay, ct);
                }
            }

            if (!success)
            {
                await _runs.MarkRunEndAsync(runId, DateTime.UtcNow);
                await NarrateRunFailureAsync(runId, step, lastEx, ct);
                await _bus.PublishAsync(new RunFailed(runId, lastEx?.Message ?? "Unknown error"), ct);
                return;
            }
        }

        var elapsed = DateTime.UtcNow - started;
        await _runs.MarkRunEndAsync(runId, DateTime.UtcNow);
        await NarrateRunSuccessAsync(runId, plan, elapsed, ctx, ct);
        await _bus.PublishAsync(new RunSucceeded(runId, elapsed), ct);
    }

    private async Task NarratePlanAsync(Guid runId, Plan plan, CancellationToken ct)
    {
        var planNarration = new List<string>
        {
            $"🎯 **EXECUTION PLAN FOR:** {plan.Goal}",
            $"📋 **TOTAL STEPS:** {plan.Steps.Count}",
            "",
            "**PLANNED APPROACH:**"
        };

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            planNarration.Add($"**Step {i + 1}:** {step.Tool} - {step.Success}");
        }

        planNarration.Add("");
        planNarration.Add("🚀 **Starting execution...**");

        var message = string.Join("\n", planNarration);
        await _bus.PublishAsync(new AgentNarration(runId, "PLAN", message), ct);
    }

    private async Task NarrateStepStartAsync(Guid runId, Step step, int current, int total, CancellationToken ct)
    {
    var message = $"🔄 **STEP {current}/{total}: {step.Tool}**\n" +
             $"🎯 Goal: {step.Success}\n" +
                     $"⚙️ Starting execution...";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateRetryAsync(Guid runId, Step step, int attempt, string? previousError, CancellationToken ct)
    {
        var message = $"🔁 **RETRY ATTEMPT {attempt}/3 for {step.Tool}**\n" +
                     $"❗ Previous attempt failed: {previousError ?? "Unknown error"}\n" +
                     $"🔄 Retrying with exponential backoff...";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateToolOutputAsync(Guid runId, Step step, object? payload, string summary, CancellationToken ct)
    {
        var messages = new List<string> { $"✅ **{step.Tool} COMPLETED**" };

        // Extract narration from payload if available
        if (payload != null)
        {
            try
            {
                var payloadJson = JsonSerializer.Serialize(payload);
                using var doc = JsonDocument.Parse(payloadJson);
                if (doc.RootElement.TryGetProperty("narration", out var narrationProp) && 
                    narrationProp.ValueKind == JsonValueKind.Array)
                {
                    messages.Add("📝 **Process Details:**");
                    foreach (var item in narrationProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            messages.Add(item.GetString()!);
                        }
                    }
                }
            }
            catch { /* ignore parsing errors */ }
        }

        messages.Add($"📊 **Summary:** {summary}");
        
        var message = string.Join("\n", messages);
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateArtifactAsync(Guid runId, Step step, Artifact artifact, CancellationToken ct)
    {
        var message = $"📁 **ARTIFACT CREATED**\n" +
                     $"📄 Name: {artifact.FileName}\n" +
                     $"📊 Size: {artifact.Size:N0} bytes\n" +
                     $"🏷️ Type: {artifact.MimeType}\n" +
                     $"💾 Saved to artifact store";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateCriticEvaluationAsync(Guid runId, Step step, CancellationToken ct)
    {
    var message = $"🔍 **QUALITY CHECK**\n" +
             $"⚖️ Evaluating step output against success criteria...\n" +
             $"🎯 Target: {step.Success}";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateCriticRejectionAsync(Guid runId, Step step, CancellationToken ct)
    {
    var message = $"❌ **QUALITY CHECK FAILED**\n" +
             $"🚫 Output did not meet success criteria: {step.Success}\n" +
                     $"🔄 Will retry if attempts remaining...";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateStepSuccessAsync(Guid runId, Step step, int artifactCount, CancellationToken ct)
    {
    var message = $"🎉 **STEP COMPLETED SUCCESSFULLY**\n" +
             $"✅ Success criteria met: {step.Success}\n" +
                     $"📁 Artifacts created: {artifactCount}\n" +
                     $"➡️ Proceeding to next step...";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateStepFailureAsync(Guid runId, Step step, Exception ex, int attempt, CancellationToken ct)
    {
        var message = $"💥 **STEP FAILED (Attempt {attempt}/3)**\n" +
                     $"❌ Error: {ex.Message}\n" +
                     $"🔍 Tool: {step.Tool}\n" +
                     (attempt < 3 ? "🔄 Will retry after delay..." : "🛑 Maximum attempts reached");
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateRetryDelayAsync(Guid runId, Step step, TimeSpan delay, CancellationToken ct)
    {
        var message = $"⏳ **RETRY DELAY**\n" +
                     $"🕒 Waiting {delay.TotalSeconds:F1} seconds before next attempt...\n" +
                     $"🔄 Using exponential backoff strategy";
        
        await _bus.PublishAsync(new AgentNarration(runId, step.Id, message), ct);
    }

    private async Task NarrateRunFailureAsync(Guid runId, Step failedStep, Exception? ex, CancellationToken ct)
    {
        var message = $"💔 **RUN FAILED**\n" +
                     $"🛑 Failed at step: {failedStep.Id} ({failedStep.Tool})\n" +
                     $"❌ Final error: {ex?.Message ?? "Unknown error"}\n" +
                     $"📊 Check artifacts for partial results";
        
        await _bus.PublishAsync(new AgentNarration(runId, "FINAL", message), ct);
    }

    private async Task NarrateRunSuccessAsync(Guid runId, Plan plan, TimeSpan elapsed, Dictionary<string, object?> ctx, CancellationToken ct)
    {
        var artifactCount = ctx.Keys.Count(k => k.Contains(":artifacts"));
        var message = $"🎊 **RUN COMPLETED SUCCESSFULLY!**\n" +
                     $"✅ Goal achieved: {plan.Goal}\n" +
                     $"📊 Steps completed: {plan.Steps.Count}\n" +
                     $"⏱️ Total time: {elapsed.TotalMinutes:F1} minutes\n" +
                     $"📁 Artifacts generated: {artifactCount}\n" +
                     $"🎯 All success criteria met!";
        
        await _bus.PublishAsync(new AgentNarration(runId, "FINAL", message), ct);
    }

    private static object? TryDeserialize(JsonElement el)
    {
        try { return JsonSerializer.Deserialize<object>(el.GetRawText()); }
        catch { return null; }
    }
}

// New event for agent narration
public record AgentNarration(Guid RunId, string StepId, string Message);
