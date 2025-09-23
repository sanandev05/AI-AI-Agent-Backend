using Microsoft.SemanticKernel;

namespace AI_AI_Agent.Domain.Plugins
{
    public class DateTimePlugin
    {
        [KernelFunction("now_iso")]
        [Description("Gets the current UTC timestamp in ISO 8601 format.")]
        public string NowIso() => DateTime.UtcNow.ToString("o");

        [KernelFunction("today")]
        [Description("Gets the current UTC date (no time).")]
        public string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd");
    }
}
