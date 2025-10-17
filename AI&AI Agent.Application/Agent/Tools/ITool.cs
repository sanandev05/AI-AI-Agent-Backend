using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent.Legacy
{
    [Obsolete("Use AI_AI_Agent.Application.Agent.ITool instead.")]
    public interface ILegacyTool
    {
        Task InvokeAsync(string input, CancellationToken cancellationToken);
    }
}