namespace AI.Agent.Infrastructure.Eventing;

// No-op event bus in Infrastructure to satisfy compilation without introducing API dependency.
public sealed class NoopEventBus : AI.Agent.Application.IEventBus
{
    public Task PublishAsync(object evt, CancellationToken ct = default) => Task.CompletedTask;
}
