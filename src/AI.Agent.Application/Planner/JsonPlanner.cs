using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI.Agent.Domain;

namespace AI.Agent.Application.Planner;

public sealed class JsonPlanner : IPlanner
{
    private readonly HashSet<string> _allowedTools;
    private readonly PlanValidator _validator;

    public JsonPlanner(IEnumerable<ITool> tools)
    {
        var toolList = tools.ToList();
        _allowedTools = toolList.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _validator = new PlanValidator(toolList);
    }

    public Task<Plan> MakePlanAsync(string goal, CancellationToken ct)
    {
        // LLM-first: classify task and decide on minimal plan
        var cls = TaskClassifier.Classify(goal);
        var steps = new List<Step>();

        if (!cls.needsWeb && !cls.needsFiles)
        {
            // Trivial or general QA → single-step LLM.Answer
            var input = CreateJsonInput(new { question = goal, format = "plain" });
            steps.Add(new Step("s1", "LLM.Answer", input, "Direct answer returned"));
        }
        else
        {
            // Fall back to existing policy-driven plans
            var goalLower = goal.ToLowerInvariant();
            if (IsResearchTask(goalLower)) steps.AddRange(CreateResearchPlan(goal));
            else if (IsAnalysisTask(goalLower)) steps.AddRange(CreateAnalysisPlan(goal));
            else if (IsCreationTask(goalLower)) steps.AddRange(CreateCreationPlan(goal));
            else if (IsBrowsingTask(goalLower)) steps.AddRange(CreateBrowsingPlan(goal));
            else steps.AddRange(CreateComprehensivePlan(goal));
        }

        // Validate/repair
        var plan = _validator.Validate(goal, steps);
        if (plan.Steps.Count == 0)
        {
            // Fallback minimal plan: direct answer
            var input = CreateJsonInput(new { question = goal, format = "plain" });
            plan = new Plan(goal, new[] { new Step("s1", "LLM.Answer", input, "Direct answer returned") });
        }

        Validate(plan.Steps);
        return Task.FromResult(plan);
    }

    private bool IsResearchTask(string goalLower)
    {
        var researchKeywords = new[] { "research", "investigate", "find out", "learn about", "study", "explore", "gather information" };
        return researchKeywords.Any(k => goalLower.Contains(k));
    }

    private bool IsAnalysisTask(string goalLower)
    {
        var analysisKeywords = new[] { "analyze", "compare", "evaluate", "assess", "review", "examine", "breakdown" };
        return analysisKeywords.Any(k => goalLower.Contains(k));
    }

    private bool IsCreationTask(string goalLower)
    {
        var creationKeywords = new[] { "create", "generate", "build", "write", "make", "develop", "produce", "design" };
        return creationKeywords.Any(k => goalLower.Contains(k));
    }

    private bool IsBrowsingTask(string goalLower)
    {
        var browsingKeywords = new[] { "visit", "navigate", "browse", "go to", "check website", "scrape", "extract from" };
        return browsingKeywords.Any(k => goalLower.Contains(k));
    }

    private List<Step> CreateResearchPlan(string goal)
    {
        var steps = new List<Step>();
        var searchTerms = ExtractSearchTerms(goal);

        // Step 1: Search for relevant sources
        var searchInput = CreateJsonInput(new { query = searchTerms, maxResults = 10 });
        steps.Add(new Step("s1", "Browser.Search", searchInput, "Found relevant sources"));

        // Step 2: Goto top result to set nav:url
        var gotoInput = CreateJsonInput(new { fromSearchStep = true, index = 0 });
        steps.Add(new Step("s2", "Browser.Goto", gotoInput, "page_loaded:true", new[] { "s1" }));

        // Step 3-5: Extract content from top sources with screenshots
        for (int i = 0; i < 3; i++)
        {
            var stepId = $"s{i + 3}";
            var screenshotInput = CreateJsonInput(new { stepNumber = i + 1, sourceRank = i + 1, fullPage = true });
            steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput, "artifact_exists:true", new[] { "s2" }));

            var extractInput = CreateJsonInput(new { selector = "main, article, #content, [role=main], .content" });
            steps.Add(new Step(stepId, "Browser.Extract", extractInput, "extracted_chars>=800", new[] { "s2" }));
        }

        // Step 6: Research notes synthesis
        var notesInput = CreateJsonInput(new { mode = "research-notes", fromSteps = new[] { "s3", "s4", "s5" } });
        steps.Add(new Step("s6", "Summarize", notesInput, "Research notes compiled"));

        // Step 7: Final comprehensive summary
        var finalInput = CreateJsonInput(new { mode = "final-synthesis", fromSteps = new[] { "s6" }, minWords = 1000 });
        steps.Add(new Step("s7", "Summarize", finalInput, "Comprehensive research report (>=1000 words)"));

        // Step 8: Document creation
        var docInput = CreateJsonInput(new { title = $"Research Report: {goal}", bodyFromStep = "s7", imagesFromSteps = new[] { "s3_screenshot", "s4_screenshot", "s5_screenshot" } });
        steps.Add(new Step("s8", "Docx.Create", docInput, "Research document created"));

        return steps;
    }

    private List<Step> CreateAnalysisPlan(string goal)
    {
        var steps = new List<Step>();
        var searchTerms = ExtractSearchTerms(goal);

        // Search for sources to analyze
        var searchInput = CreateJsonInput(new { query = searchTerms, maxResults = 15 });
        steps.Add(new Step("s1", "Browser.Search", searchInput, "Analysis sources identified"));

        // Goto first result to set nav:url
        var gotoInput = CreateJsonInput(new { fromSearchStep = true, index = 0 });
        steps.Add(new Step("s2", "Browser.Goto", gotoInput, "page_loaded:true", new[] { "s1" }));

        // Extract content for comparison
        for (int i = 0; i < 4; i++)
        {
            var stepId = $"s{i + 3}";
            var screenshotInput2 = CreateJsonInput(new { analysisStep = i + 1, purpose = "data collection", fullPage = true });
            steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput2, "artifact_exists:true", new[] { "s2" }));

            var extractInput2 = CreateJsonInput(new { selector = "main, article, .analysis, .data, #content" });
            steps.Add(new Step(stepId, "Browser.Extract", extractInput2, "extracted_chars>=800", new[] { "s2" }));
        }

        // Comparative analysis
        var analysisInput = CreateJsonInput(new { mode = "comparative-analysis", fromSteps = new[] { "s2", "s3", "s4", "s5" }, analysisType = "comprehensive" });
    steps.Add(new Step("s7", "Summarize", analysisInput, "Comparative analysis completed"));

        // Final analytical report
        var reportInput = CreateJsonInput(new { mode = "analytical-report", fromSteps = new[] { "s6" }, minWords = 1200, includeCharts = true });
    steps.Add(new Step("s8", "Summarize", reportInput, "Analytical report generated"));

        // Create document with analysis
    var docInput2 = CreateJsonInput(new { title = $"Analysis Report: {goal}", bodyFromStep = "s8", imagesFromSteps = new[] { "s3_screenshot", "s4_screenshot", "s5_screenshot", "s6_screenshot" } });
    steps.Add(new Step("s9", "Docx.Create", docInput2, "Analysis document created"));

        return steps;
    }

    private List<Step> CreateCreationPlan(string goal)
    {
        var steps = new List<Step>();
        var searchTerms = ExtractSearchTerms(goal);

        // Research for creation inspiration/requirements
        var searchInput = CreateJsonInput(new { query = searchTerms + " examples tutorial guide", maxResults = 8 });
        steps.Add(new Step("s1", "Browser.Search", searchInput, "Creation references found"));

        // Goto first result to set nav:url
        var gotoInput = CreateJsonInput(new { fromSearchStep = true, index = 0 });
        steps.Add(new Step("s2", "Browser.Goto", gotoInput, "page_loaded:true", new[] { "s1" }));

        // Gather examples and best practices
        for (int i = 0; i < 2; i++)
        {
            var stepId = $"s{i + 3}";
            var screenshotInput3 = CreateJsonInput(new { inspirationStep = i + 1, purpose = "example gathering", fullPage = true });
            steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput3, "artifact_exists:true", new[] { "s2" }));

            var extractInput3 = CreateJsonInput(new { selector = "main, article, .tutorial, .example, .guide" });
            steps.Add(new Step(stepId, "Browser.Extract", extractInput3, "extracted_chars>=800", new[] { "s2" }));
        }

        // Synthesize creation requirements
        var requirementsInput = CreateJsonInput(new { mode = "creation-requirements", fromSteps = new[] { "s2", "s3" }, goal = goal });
    steps.Add(new Step("s4", "Summarize", requirementsInput, "Creation requirements defined"));

        // Generate the creation
        var creationInput = CreateJsonInput(new { mode = "creative-synthesis", fromSteps = new[] { "s4" }, goal = goal, minWords = 800 });
    steps.Add(new Step("s5", "Summarize", creationInput, "Creation completed"));

        // Document the creation
    var docInput3 = CreateJsonInput(new { title = $"Creation: {goal}", bodyFromStep = "s5", imagesFromSteps = new[] { "s3_screenshot", "s4_screenshot" } });
    steps.Add(new Step("s6", "Docx.Create", docInput3, "Creation documented"));

        return steps;
    }

    private List<Step> CreateBrowsingPlan(string goal)
    {
        var steps = new List<Step>();
        var urls = ExtractUrlsFromGoal(goal);

        if (urls.Any())
        {
            // Direct URL browsing
            for (int i = 0; i < Math.Min(urls.Count, 3); i++)
            {
                var stepId = $"s{i + 1}";
                var screenshotInput4 = CreateJsonInput(new { url = urls[i], fullPage = true, purpose = "browsing documentation" });
                steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput4, "Page screenshot captured"));

                var extractInput4 = CreateJsonInput(new { url = urls[i], selector = "body", fullContent = true });
                steps.Add(new Step(stepId, "Browser.Extract", extractInput4, "Page content extracted", new[] { $"{stepId}_screenshot" }));
            }
        }
        else
        {
            // Search-based browsing
            var searchTerms = ExtractSearchTerms(goal);
            var searchInput = CreateJsonInput(new { query = searchTerms, maxResults = 5 });
            steps.Add(new Step("s1", "Browser.Search", searchInput, "Target pages found"));
            var gotoInput = CreateJsonInput(new { fromSearchStep = true, index = 0 });
            steps.Add(new Step("s2", "Browser.Goto", gotoInput, "page_loaded:true", new[] { "s1" }));

            for (int i = 0; i < 2; i++)
            {
                var stepId = $"s{i + 3}";
                var screenshotInput5 = CreateJsonInput(new { purpose = "page exploration", fullPage = true });
                steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput5, "artifact_exists:true", new[] { "s2" }));

                var extractInput5 = CreateJsonInput(new { fullContent = true });
                steps.Add(new Step(stepId, "Browser.Extract", extractInput5, "extracted_chars>=500", new[] { "s2" }));
            }
        }

        // Summarize browsing results
        var extractSteps = steps.Where(s => s.Tool == "Browser.Extract").Select(s => s.Id).ToArray();
        var summaryInput = CreateJsonInput(new { mode = "browsing-summary", fromSteps = extractSteps, goal = goal });
        steps.Add(new Step("summary", "Summarize", summaryInput, "Browsing results summarized"));

        return steps;
    }

    private List<Step> CreateComprehensivePlan(string goal)
    {
        // Default comprehensive approach for unclear goals
        var steps = new List<Step>();
        var searchTerms = ExtractSearchTerms(goal);

    // Broad search
    var searchInput = CreateJsonInput(new { query = searchTerms, maxResults = 12 });
    steps.Add(new Step("s1", "Browser.Search", searchInput, "Comprehensive sources found"));
    var gotoInputC = CreateJsonInput(new { fromSearchStep = true, index = 0 });
    steps.Add(new Step("s2", "Browser.Goto", gotoInputC, "page_loaded:true", new[] { "s1" }));

        // Multi-source extraction with screenshots
        for (int i = 0; i < 3; i++)
        {
            var stepId = $"s{i + 3}";
            var screenshotInput6 = CreateJsonInput(new { comprehensiveStep = i + 1, goal = goal, fullPage = true });
            steps.Add(new Step($"{stepId}_screenshot", "Browser.Screenshot", screenshotInput6, "artifact_exists:true", new[] { "s2" }));

            var extractInput6 = CreateJsonInput(new { selector = "main, article, #content, [role=main]" });
            steps.Add(new Step(stepId, "Browser.Extract", extractInput6, "extracted_chars>=800", new[] { "s2" }));
        }

        // Research synthesis
    var notesInput = CreateJsonInput(new { mode = "research-notes", fromSteps = new[] { "s3", "s4", "s5" } });
    steps.Add(new Step("s6", "Summarize", notesInput, "Research synthesis completed"));

        // Final comprehensive report
    var finalInput = CreateJsonInput(new { mode = "comprehensive-report", fromSteps = new[] { "s6" }, minWords = 1200, goal = goal });
    steps.Add(new Step("s7", "Summarize", finalInput, "Comprehensive report generated"));

        // Document creation
    var docInput = CreateJsonInput(new { title = $"Comprehensive Report: {goal}", bodyFromStep = "s7", imagesFromSteps = new[] { "s3_screenshot", "s4_screenshot", "s5_screenshot" } });
    steps.Add(new Step("s8", "Docx.Create", docInput, "Comprehensive document created"));

        return steps;
    }

    private string ExtractSearchTerms(string goal)
    {
        // Extract meaningful keywords from the goal
        var tokens = goal.Split(new[] { ' ', '\t', '\n', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(t => t.Length > 2 && !IsStopWord(t))
                         .Take(8).ToArray();
        return string.Join(' ', tokens);
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new[] { "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "new", "now", "old", "see", "two", "way", "who", "boy", "did", "may", "she", "use", "what", "with" };
        return stopWords.Contains(word.ToLowerInvariant());
    }

    private List<string> ExtractUrlsFromGoal(string goal)
    {
        var urlPattern = @"https?://[^\s<>""]+";
        var matches = Regex.Matches(goal, urlPattern);
        return matches.Cast<Match>().Select(m => m.Value).ToList();
    }

    private JsonElement CreateJsonInput(object input)
    {
        var json = JsonSerializer.Serialize(input);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private void Validate(IEnumerable<Step> steps)
    {
        foreach (var s in steps)
        {
            if (!_allowedTools.Contains(s.Tool))
            {
                throw new InvalidOperationException($"Planner produced unknown tool: {s.Tool}");
            }
        }
    }
}
