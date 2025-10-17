using System.Text.RegularExpressions;

namespace AI.Agent.Application.Search;

public static class QueryBuilder
{
    // Normalize queries and build a domain allowlist using 'site:' operators.
    public static (string normalized, string[] allow) Normalize(string userQuery, string[]? domainAllowlist)
    {
        var allow = (domainAllowlist?.Length ?? 0) > 0 ? domainAllowlist! : Array.Empty<string>();

        // If the user text contains a domain, capture and allowlist it (e.g., iticket.az)
        var m = Regex.Match(userQuery ?? string.Empty, @"\b([a-z0-9-]+\.[a-z]{2,})\b", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var dom = m.Groups[1].Value.ToLowerInvariant();
            allow = allow.Concat(new[] { dom }).Distinct().ToArray();
        }

        // If we are clearly talking about iTicket, force the right TLD.
        if (!string.IsNullOrWhiteSpace(userQuery) && userQuery.Contains("iticket", StringComparison.OrdinalIgnoreCase) &&
            !allow.Any(d => d.Contains("iticket.az", StringComparison.OrdinalIgnoreCase)))
        {
            allow = allow.Concat(new[] { "iticket.az", "www.iticket.az" }).Distinct().ToArray();
        }

        // Build final query with site: operators when allowlist exists.
        var sitePrefix = allow.Length > 0 ? string.Join(" OR ", allow.Select(d => $"site:{d}")) : string.Empty;
        var normalized = string.IsNullOrWhiteSpace(sitePrefix) ? (userQuery ?? string.Empty).Trim()
                                                               : $"{sitePrefix} {(userQuery ?? string.Empty).Trim()}";
        return (normalized, allow);
    }
}
