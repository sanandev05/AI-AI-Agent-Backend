using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application;
using AI_AI_Agent.Application.Budgets;
using AI_AI_Agent.Application.Critic;
using AI_AI_Agent.Application.Executor;
using AI_AI_Agent.Application.Routing;
using AI_AI_Agent.Application.Tools;
using AI_AI_Agent.Domain;
using AI_AI_Agent.Domain.Events;
using AI_AI_Agent.Infrastructure.Orchestration.Stores;
using AI_AI_Agent.Infrastructure.Orchestration.Storage;
using Xunit;

namespace AI_AI_Agent.Tests;

public class ExecutorTests
{
    [Fact]
    public async Task Summarize_Then_DocxCreate_Produces_Docx()
    {
        // Arrange
        var eventBus = new InMemoryEventBus();
        var artifactStore = new FileArtifactStore();
        var runStore = new InMemoryRunStore();
        await using var budget = new BudgetManager(eventBus);
        var critic = new SimpleCritic();
        var tools = new ITool[] { new SummarizeTool(), new DocxCreateTool() };
        var router = new ToolRouter(tools);
    var approval = new InMemoryApprovalGate();
    var exec = new Executor(router, eventBus, artifactStore, runStore, budget, critic, approval);
        var runId = Guid.NewGuid();
        var steps = new List<Step>
        {
            new("s1","Summarize", JsonDocument.Parse("{\"text\":\"This is a long enough text to pass the critic because it exceeds twenty characters.\"}").RootElement, "ok"),
            new("s2","Docx.Create", JsonDocument.Parse("{\"title\":\"Test Report\",\"bodyFromStep\":\"s1\"}").RootElement, "ok")
        };
        var plan = new Plan("test", steps);

        // Act
        await exec.ExecuteAsync(runId, plan, CancellationToken.None);

        // Assert: find a docx under storage/runId
        var dir = Path.Combine(AppContext.BaseDirectory, "storage", runId.ToString());
        Assert.True(Directory.Exists(dir), $"storage directory not found: {dir}");
        var hasDocx = Directory.EnumerateFiles(dir, "*.docx", SearchOption.TopDirectoryOnly).Any();
        Assert.True(hasDocx, "Expected a DOCX artifact to be created");

        // Optional: ensure events were emitted
        Assert.Contains(eventBus.Events, e => e is RunStarted rs && rs.RunId == runId);
        Assert.Contains(eventBus.Events, e => e is StepSucceeded ss && ss.StepId == "s2");
        Assert.Contains(eventBus.Events, e => e is RunSucceeded);
    }

    private sealed class InMemoryEventBus : IEventBus
    {
        public List<object> Events { get; } = new();
        public Task PublishAsync(object evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }
}
