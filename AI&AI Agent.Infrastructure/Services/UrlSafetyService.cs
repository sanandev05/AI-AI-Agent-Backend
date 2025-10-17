using System;
using AI_AI_Agent.Application.Services;
using Microsoft.Extensions.Configuration;

namespace AI_AI_Agent.Infrastructure.Services;

public class UrlSafetyService : IUrlSafetyService
{
    private readonly string[] _allow;
    private readonly string[] _deny;

    public UrlSafetyService(IConfiguration config)
    {
        _allow = config.GetSection("UrlSafety:AllowList").Get<string[]>() ?? Array.Empty<string>();
        _deny = config.GetSection("UrlSafety:DenyList").Get<string[]>() ?? Array.Empty<string>();
    }

    public bool IsAllowed(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        // Deny has priority
        foreach (var d in _deny)
        {
            if (!string.IsNullOrWhiteSpace(d) && url.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
        }
        // If allow list set, require a match; if empty, allow by default
        if (_allow.Length == 0) return true;
        foreach (var a in _allow)
        {
            if (!string.IsNullOrWhiteSpace(a) && url.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    public string? GetViolationReason(string url)
    {
        foreach (var d in _deny)
        {
            if (!string.IsNullOrWhiteSpace(d) && url.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0)
                return $"URL blocked by deny list: contains '{d}'";
        }
        if (_allow.Length > 0)
        {
            foreach (var a in _allow)
            {
                if (!string.IsNullOrWhiteSpace(a) && url.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)
                    return null;
            }
            return "URL not in allow list.";
        }
        return null;
    }
}
