namespace AI_AI_Agent.Application.Services;

public interface IUrlSafetyService
{
    bool IsAllowed(string url);
    string? GetViolationReason(string url);
}
