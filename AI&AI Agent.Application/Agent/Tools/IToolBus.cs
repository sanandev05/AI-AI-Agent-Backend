using System;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent.Legacy
{
    [Obsolete("Use AI_AI_Agent.Application.Agent.IToolBus instead.")]
    public interface ILegacyToolBus
    {
        Task EmitFinalAsync(string result);
        Task EmitToolStartAsync(string toolName);
        Task EmitToolEndAsync(string toolName, string result);
    }
}