using System;
using System.Collections.Concurrent;
using System.Threading;
using AI_AI_Agent.Application.Services;

namespace AI_AI_Agent.Infrastructure.Services;

public class RunCancellationRegistry : IRunCancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _map = new(StringComparer.OrdinalIgnoreCase);

    public CancellationToken Register(string chatId)
    {
        var cts = new CancellationTokenSource();
        _map.AddOrUpdate(chatId, cts, (_, old) => { try { old.Cancel(); old.Dispose(); } catch { } return cts; });
        return cts.Token;
    }

    public bool Cancel(string chatId)
    {
        if (_map.TryGetValue(chatId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            return true;
        }
        return false;
    }

    public void Complete(string chatId)
    {
        if (_map.TryRemove(chatId, out var cts))
        {
            try { cts.Dispose(); } catch { }
        }
    }

    public bool IsRunning(string chatId) => _map.ContainsKey(chatId);
}
