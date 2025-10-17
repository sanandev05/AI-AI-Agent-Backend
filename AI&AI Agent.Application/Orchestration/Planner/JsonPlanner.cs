using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI_AI_Agent.Domain;

namespace AI_AI_Agent.Application.Planner;

public sealed class JsonPlanner : IPlanner
{
    private readonly HashSet<string> _allowedTools;

    public JsonPlanner(IEnumerable<ITool> tools)
    {
        _allowedTools = tools.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task<Plan> MakePlanAsync(string goal, CancellationToken ct)
    {
        // Analyze the goal to determine the appropriate approach
        var goalLower = goal.ToLowerInvariant();
        var steps = new List<Step>();

        // Always add a thinking step first for transparency
        using (var thinkDoc = JsonDocument.Parse($"{{\"goal\":\"{EscapeJson(goal)}\"}}"))
        {
            steps.Add(new Step("s0", "Think.DecideExternal", thinkDoc.RootElement.Clone(), "decision recorded"));
        }

        // Early decision: if outside help likely not needed, use local LLM answer
        if (!NeedsExternalHelp(goalLower))
        {
            using var doc = JsonDocument.Parse($"{{\"prompt\":\"{EscapeJson(goal)}\",\"minWords\":120,\"style\":\"concise\"}}");
            steps.Add(new Step("s1", "LLM.Answer", doc.RootElement.Clone(), ">=120 words answer", new[] { "s0" }));
            Validate(steps);
            return Task.FromResult(new Plan(goal, steps));
        }

        // Determine task type and generate appropriate plan
        if (IsPresentationTask(goalLower))
        {
            steps.AddRange(CreatePresentationPlan(goal));
        }
        else if (IsResearchTask(goalLower))
        {
            steps.AddRange(CreateResearchPlan(goal));
        }
        else if (IsAnalysisTask(goalLower))
        {
            steps.AddRange(CreateAnalysisPlan(goal));
        }
        else if (IsCreationTask(goalLower))
        {
            steps.AddRange(CreateCreationPlan(goal));
        }
        else if (IsBrowsingTask(goalLower))
        {
            steps.AddRange(CreateBrowsingPlan(goal));
        }
        else
        {
            // Default: comprehensive research and documentation
            steps.AddRange(CreateComprehensivePlan(goal));
        }

        Validate(steps);
        return Task.FromResult(new Plan(goal, steps));
    }

    private bool IsResearchTask(string goalLower)
    {
        var researchKeywords = new[]
        {
            "research", "find information", "learn about", "investigate", "study", "explore",
            "what is", "tell me about", "explain", "information about", "facts about",
            "latest developments", "current trends", "overview of", "summarize", "summary",
            "fetch from", "get content from", "analyze website", "analyze url", "read from url"
        };
        return researchKeywords.Any(keyword => goalLower.Contains(keyword)) || 
               Regex.IsMatch(goalLower, @"https?://[^\s]+"); // Contains URL
    }

    private bool IsAnalysisTask(string goalLower)
    {
        var analysisKeywords = new[]
        {
            "analyze", "analysis", "compare", "comparison", "evaluate", "assessment",
            "pros and cons", "advantages and disadvantages", "versus", "vs",
            "differences between", "similarities", "review", "critique"
        };
        return analysisKeywords.Any(keyword => goalLower.Contains(keyword));
    }

    private bool IsCreationTask(string goalLower)
    {
        var creationKeywords = new[]
        {
            "create", "generate", "write", "make", "build", "develop",
            "guide", "tutorial", "manual", "handbook", "report", "document",
            "plan", "strategy", "roadmap", "checklist", "template"
        };
        return creationKeywords.Any(keyword => goalLower.Contains(keyword));
    }

    private bool IsPresentationTask(string goalLower)
    {
        var pptxKeywords = new[]
        {
            "pptx", "powerpoint", "presentation", "slides",
            // Azerbaijani
            "təqdimat", "slayd", "slaydlar"
        };
        return pptxKeywords.Any(k => goalLower.Contains(k));
    }

    private bool IsBrowsingTask(string goalLower)
    {
        var browsingKeywords = new[]
        {
            "website", "browse", "visit", "check", "analyze site", "web page",
            "homepage", "online", "url", "domain", "site analysis"
        };
        return browsingKeywords.Any(keyword => goalLower.Contains(keyword));
    }

    private List<Step> CreateResearchPlan(string goal)
    {
        var steps = new List<Step>();
        var searchQuery = ExtractSearchQuery(goal);
        
        // Check if the goal contains specific URLs
        var urls = ExtractUrlsFromText(goal);
        
        if (urls.Any())
        {
            // Direct URL research plan - use WebResearchAgent for targeted content
            steps.Add(CreateWebResearchStep("s1", goal, urls));
            
            // Optional: Create document with findings
            if (ShouldCreateDocument(goal))
            {
                steps.Add(CreateDocxStep("s2", goal, "s1", new string[0]));
            }
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            // Traditional search + research plan with enhanced web content fetching
            steps.Add(CreateWebResearchStep("s1", searchQuery, new List<string>()));
            
            // Optional: Create detailed document
            if (ShouldCreateDocument(goal))
            {
                steps.Add(CreateDocxStep("s2", goal, "s1", new string[0]));
            }
        }
        else
        {
            // Fallback: Use search + web content fetch approach for better reliability
            if (!string.IsNullOrWhiteSpace(searchQuery) || !string.IsNullOrWhiteSpace(goal))
            {
                var queryToUse = !string.IsNullOrWhiteSpace(searchQuery) ? searchQuery : goal;
                
                // Step 1: Search for content
                steps.Add(CreateSearchStep("s1", queryToUse));
                
                // Step 2: Use WebContentFetch to get content from search results
                steps.Add(CreateWebContentFetchStep("s2", new[] { "s1" }));
                
                // Step 3: Summarize the results
                steps.Add(CreateSummarizeStep("s3", "smart", new[] { "s2" }, 200));
                
                // Optional: Create final document if needed
                if (ShouldCreateDocument(goal))
                {
                    steps.Add(CreateDocxStep("s4", goal, "s3", new string[0]));
                }
            }
            else
            {
                // Last resort: Just provide a summary response
                steps.Add(CreateSummarizeStep("s1", "smart", new string[0], 100));
            }
        }

        return steps;
    }

    private bool NeedsExternalHelp(string goalLower)
    {
        // Heuristic: if the user asks for current data, specific URLs/domains, product comparisons,
        // numerical facts that change, or site analysis, we likely need external help.
        var freshness = new[] { "today", "latest", "current", "news", "up to date", "recent" };
        var webby = new[] { "from site", "from url", "visit", "browse", "check website", "analyze website", "web page", "screenshot" };
        var retrieval = new[] { "find", "search", "compare", "price", "prices", "documentation", "docs", "specs", "data sheet", "manual" };
        var hasUrl = System.Text.RegularExpressions.Regex.IsMatch(goalLower, @"https?://[^\s]+");
        var mentionsDomain = System.Text.RegularExpressions.Regex.IsMatch(goalLower, @"\b([a-z0-9\-]+\.)+[a-z]{2,}\b");

        bool triggers = hasUrl
            || mentionsDomain
            || freshness.Any(k => goalLower.Contains(k))
            || webby.Any(k => goalLower.Contains(k))
            || retrieval.Any(k => goalLower.Contains(k));

        // Also: if explicitly asking for a document/report creation based on research, require external
        var docy = new[] { "report", "document", "docx", "pptx", "presentation", "sources", "citations" };
        if (docy.Any(k => goalLower.Contains(k))) triggers = true;

        // Otherwise, default to local answer
        return triggers;
    }

    private List<Step> CreateAnalysisPlan(string goal)
    {
        var steps = new List<Step>();
        var searchQuery = ExtractSearchQuery(goal);

        // Step 1: Search for comparative information
        steps.Add(CreateSearchStep("s1", searchQuery));
        
        // Step 2-4: Extract from multiple sources for comparison
        for (int i = 2; i <= 4; i++)
        {
            steps.Add(CreateExtractStep($"s{i}", new[] { "s1" }));
        }

        // Step 5: Screenshot for documentation
        steps.Add(CreateScreenshotStep("s5", new[] { "s2" }));

        // Step 6: Compile comparative notes
        steps.Add(CreateSummarizeStep("s6", "research-notes", new[] { "s2", "s3", "s4" }));

        // Step 7: Comparative analysis
        steps.Add(CreateSummarizeStep("s7", "comparative-analysis", new[] { "s6" }, 1200));

        // Step 8: Create analysis document
        steps.Add(CreateDocxStep("s8", goal, "s7", new[] { "s5" }));

        return steps;
    }

    private List<Step> CreateCreationPlan(string goal)
    {
        var steps = new List<Step>();
        var searchQuery = ExtractSearchQuery(goal);

        // Step 1: Research for creation
        steps.Add(CreateSearchStep("s1", searchQuery));
        
        // Step 2-3: Extract information for creation
        for (int i = 2; i <= 3; i++)
        {
            steps.Add(CreateExtractStep($"s{i}", new[] { "s1" }));
        }

        // Step 4: Screenshot for reference
        steps.Add(CreateScreenshotStep("s4", new[] { "s2" }));

        // Step 5: Compile requirements
        steps.Add(CreateSummarizeStep("s5", "creation-requirements", new[] { "s2", "s3" }));

        // Step 6: Creative synthesis
        steps.Add(CreateSummarizeStep("s6", "creative-synthesis", new[] { "s5" }, 1500));

        // Step 7: Create final guide/document
        steps.Add(CreateDocxStep("s7", goal, "s6", new[] { "s4" }));

        return steps;
    }

    private List<Step> CreateBrowsingPlan(string goal)
    {
        var steps = new List<Step>();
        var urls = ExtractUrls(goal);

        if (urls.Any())
        {
            // Direct URL analysis
            for (int i = 0; i < Math.Min(urls.Count, 3); i++)
            {
                steps.Add(CreateDirectExtractStep($"s{i+1}", urls[i]));
                steps.Add(CreateScreenshotStepWithUrl($"s{i+4}", urls[i]));
            }
        }
        else
        {
            // Search-based browsing
            var searchQuery = ExtractSearchQuery(goal);
            steps.Add(CreateSearchStep("s1", searchQuery));
            steps.Add(CreateExtractStep("s2", new[] { "s1" }));
            steps.Add(CreateScreenshotStep("s3", new[] { "s2" }));
        }

        // Browsing summary
        var fromSteps = steps.Where(s => s.Tool == "Browser.Extract").Select(s => s.Id).ToArray();
        steps.Add(CreateSummarizeStep("sB", "browsing-summary", fromSteps));

        // Final document
        var imageSteps = steps.Where(s => s.Tool == "Browser.Screenshot").Select(s => s.Id).ToArray();
        steps.Add(CreateDocxStep("sF", goal, "sB", imageSteps));

        return steps;
    }

    private List<Step> CreateComprehensivePlan(string goal)
    {
        var steps = new List<Step>();
        var searchQuery = ExtractSearchQuery(goal);

        // Step 1: Comprehensive search
        steps.Add(CreateSearchStep("s1", searchQuery));
        
        // Step 2-5: Extract from multiple sources
        for (int i = 2; i <= 5; i++)
        {
            steps.Add(CreateExtractStep($"s{i}", new[] { "s1" }));
        }

        // Step 6: Screenshot for documentation
        steps.Add(CreateScreenshotStep("s6", new[] { "s2" }));

        // Step 7: Compile comprehensive notes
        steps.Add(CreateSummarizeStep("s7", "research-notes", new[] { "s2", "s3", "s4", "s5" }));

        // Step 8: Comprehensive analysis
        steps.Add(CreateSummarizeStep("s8", "comprehensive-report", new[] { "s7" }, 1800));

        // Step 9: Create final comprehensive document
        steps.Add(CreateDocxStep("s9", goal, "s8", new[] { "s6" }));

        return steps;
    }

    private List<Step> CreatePresentationPlan(string goal)
    {
        var steps = new List<Step>();
        var searchQuery = ExtractSearchQuery(goal);

        // Search and extract a couple of sources
        steps.Add(CreateSearchStep("s1", searchQuery));
        steps.Add(CreateExtractStep("s2", new[] { "s1" }));
        steps.Add(CreateExtractStep("s3", new[] { "s1" }));

        // Summarize with shorter min words, bilingual if Azerbaijani likely
        var bilingual = goal.ToLowerInvariant().Contains("azərbay") ? ",\"bilingual\":true" : string.Empty;
        using (var doc = System.Text.Json.JsonDocument.Parse($"{{\"mode\":\"final-synthesis\",\"fromSteps\":[\"s2\",\"s3\"],\"minWords\":600{bilingual}}}"))
        {
            steps.Add(new Step("s4", "Summarize", doc.RootElement.Clone(), ">=600 words synthesized"));
        }

        // Create PPTX from the synthesized summary
        steps.Add(CreatePptxStep("s5", goal, "s4"));
        return steps;
    }

    private Step CreateSearchStep(string id, string query)
    {
        using var doc = JsonDocument.Parse($"{{\"query\":\"{EscapeJson(query)}\"}}");
    return new Step(id, "SearchAPI.Query", doc.RootElement.Clone(), "search results found");
    }

    private Step CreateExtractStep(string id, string[] dependencies)
    {
        using var doc = JsonDocument.Parse("{\"fromSearchStep\":true,\"selector\":\"main, article, #content, [role=main]\"}");
        return new Step(id, "Browser.Extract", doc.RootElement.Clone(), ">=500 chars extracted", dependencies);
    }

    private Step CreateDirectExtractStep(string id, string url)
    {
        using var doc = JsonDocument.Parse($"{{\"url\":\"{EscapeJson(url)}\",\"selector\":\"main, article, #content, [role=main]\"}}");
        return new Step(id, "Browser.Extract", doc.RootElement.Clone(), ">=500 chars extracted");
    }

    private Step CreateScreenshotStep(string id, string[] dependencies)
    {
        using var doc = JsonDocument.Parse("{\"fromPreviousStep\":true,\"fullPage\":true}");
        return new Step(id, "Browser.Screenshot", doc.RootElement.Clone(), "screenshot captured", dependencies);
    }

    private Step CreateScreenshotStepWithUrl(string id, string url)
    {
        using var doc = JsonDocument.Parse($"{{\"url\":\"{EscapeJson(url)}\",\"fullPage\":true}}");
        return new Step(id, "Browser.Screenshot", doc.RootElement.Clone(), "screenshot captured");
    }

    private Step CreateSummarizeStep(string id, string mode, string[] fromSteps, int minWords = 800)
    {
        var stepsList = string.Join(",", fromSteps.Select(s => $"\"{s}\""));
        using var doc = JsonDocument.Parse($"{{\"mode\":\"{mode}\",\"fromSteps\":[{stepsList}],\"minWords\":{minWords}}}");
        return new Step(id, "Summarize", doc.RootElement.Clone(), $">={minWords} words synthesized");
    }

    private Step CreateDocxStep(string id, string goal, string bodyFromStep, string[] imageSteps)
    {
        var imagesList = string.Join(",", imageSteps.Select(s => $"\"{s}\""));
        using var doc = JsonDocument.Parse($"{{\"title\":\"{EscapeJson($"Report: {goal}")}\",\"bodyFromStep\":\"{bodyFromStep}\",\"imagesFromSteps\":[{imagesList}]}}");
        return new Step(id, "Docx.Create", doc.RootElement.Clone(), ">5KB document created");
    }

    private Step CreatePptxStep(string id, string goal, string fromStep)
    {
        using var doc = JsonDocument.Parse($"{{\"title\":\"{EscapeJson(goal)}\",\"fromStep\":\"{fromStep}\"}}");
        return new Step(id, "Pptx.Create", doc.RootElement.Clone(), "PPTX created");
    }

    private string ExtractSearchQuery(string goal)
    {
        // Handle non-English queries by translating common patterns
        if (goal.ToLowerInvariant().Contains("azərbaycan"))
        {
            return "Azerbaijan information facts country";
        }
        
        // Remove common task words and extract the core subject
        var taskWords = new[] { "research", "analyze", "create", "make", "find", "information", "about", "tell me", "explain", "generate", "write", "build", "develop", "haqda", "məlumat", "ver" };
        var words = goal.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !taskWords.Contains(word) && word.Length > 2)
            .Take(5);
        
        var query = string.Join(" ", words);
        
        // If query is empty or very short, use a more general approach
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            // Extract key nouns/topics from the original goal
            if (goal.ToLowerInvariant().Contains("azerbaijan") || goal.ToLowerInvariant().Contains("azərbaycan"))
                return "Azerbaijan country information";
            
            // Fallback: use first few meaningful words
            var fallbackWords = goal.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(3);
            return string.Join(" ", fallbackWords);
        }
        
        return query;
    }

    private List<string> ExtractUrls(string goal)
    {
        var urlPattern = @"https?://[^\s]+";
        var matches = Regex.Matches(goal, urlPattern, RegexOptions.IgnoreCase);
        return matches.Select(m => m.Value).ToList();
    }

    private List<string> ExtractUrlsFromText(string text)
    {
        var urlPattern = @"https?://[^\s<>""{}|\\^`\[\]]+";
        var matches = Regex.Matches(text, urlPattern, RegexOptions.IgnoreCase);
        return matches.Select(m => m.Value).ToList();
    }

    private bool ShouldCreateDocument(string goal)
    {
        var documentKeywords = new[]
        {
            "document", "report", "docx", "create", "generate", "write",
            "manual", "guide", "handbook", "summary report"
        };
        return documentKeywords.Any(keyword => goal.ToLowerInvariant().Contains(keyword));
    }

    private Step CreateWebResearchStep(string id, string query, List<string> urls)
    {
        var inputData = new Dictionary<string, object>
        {
            ["userPrompt"] = query,
            ["summaryMode"] = "smart"
        };

        if (urls.Any())
        {
            inputData["urls"] = urls.ToArray();
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            inputData["query"] = query;
        }

        var json = JsonSerializer.Serialize(inputData);
        using var doc = JsonDocument.Parse(json);
        return new Step(id, "WebResearchAgent", doc.RootElement.Clone(), "Web research completed with intelligent summary");
    }

    private Step CreateWebContentFetchStep(string id, string[] dependencies)
    {
        using var doc = JsonDocument.Parse("{\"fromSearchStep\":true,\"method\":\"browser\",\"timeoutSec\":30}");
        return new Step(id, "WebContentFetch", doc.RootElement.Clone(), "Content fetched from search results", dependencies);
    }

    private string EscapeJson(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private void Validate(List<Step> steps)
    {
        if (steps.Count == 0)
            throw new InvalidOperationException("Plan must contain at least one step");

        var requiredTools = steps.Select(s => s.Tool).Distinct();
        foreach (var tool in requiredTools)
        {
            if (!_allowedTools.Contains(tool))
                throw new InvalidOperationException($"Required tool '{tool}' is not available");
        }

        // Validate step dependencies
        var stepIds = steps.Select(s => s.Id).ToHashSet();
        foreach (var step in steps)
        {
            if (step.Deps != null)
            {
                foreach (var dep in step.Deps)
                {
                    if (!stepIds.Contains(dep))
                        throw new InvalidOperationException($"Step '{step.Id}' depends on non-existent step '{dep}'");
                }
            }
        }
    }
}
