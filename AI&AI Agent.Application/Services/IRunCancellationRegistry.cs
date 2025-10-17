using System.Threading;

namespace AI_AI_Agent.Application.Services;

public interface IRunCancellationRegistry
{
    // Create or replace a CTS for a chat run and return its token
    CancellationToken Register(string chatId);
    // Request cancellation; returns true if found
    bool Cancel(string chatId);
    // Remove completed entry
    void Complete(string chatId);
    // Check if there is an active entry
    bool IsRunning(string chatId);
}
