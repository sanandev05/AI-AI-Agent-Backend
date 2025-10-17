using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent.Routing;
using AI_AI_Agent.Application.Agent.Storage;
using AI_AI_Agent.Application.Agent.Planning;
using Microsoft.Extensions.Logging;
using AI_AI_Agent.Domain.Repositories;

namespace AI_AI_Agent.Application.Agent;

public class AgentLoop
{
    private readonly IToolBus _toolBus;
    private readonly LLMRouter _router;
    private readonly ILogger<AgentLoop> _logger;
    private readonly IChatStore _chatStore;
    private readonly IReadOnlyList<ITool> _tools;
    private readonly IPlanner _planner;
    private readonly IPlanStore _planStore;
    private readonly IRunRepository _runRepository;
    private readonly IStepLogRepository _stepLogRepository;
    private readonly IArtifactRepository _artifactRepository;

    public AgentLoop(IToolBus toolBus, LLMRouter router, ILogger<AgentLoop> logger,
        IChatStore chatStore, IEnumerable<ITool> tools, IPlanner planner, IPlanStore planStore,
        IRunRepository runRepository, IStepLogRepository stepLogRepository, IArtifactRepository artifactRepository)
    {
        _toolBus = toolBus;
        _router = router;
        _logger = logger;
        _chatStore = chatStore;
        _tools = tools.ToList();
        _planner = planner;
        _planStore = planStore;
        _runRepository = runRepository;
        _stepLogRepository = stepLogRepository;
        _artifactRepository = artifactRepository;
    }

    public async Task RunAsync(string chatId, string userPrompt, CancellationToken cancellationToken)
    {
        var currentStep = 0;
        const int maxSteps = 20;

        var run = new Domain.Entities.Run
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.TryParse(chatId, out var guid) ? guid : Guid.NewGuid(), // Assuming chatId can be a taskId
            Status = Domain.Entities.Enums.RunStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
        await _runRepository.AddAsync(run);
        // Emit a timeline event for UI visibility
        await _toolBus.EmitTimelineAsync(chatId, "run", $"Run started: {run.Id}");

        try
        {
            await _chatStore.AppendUserAsync(chatId, userPrompt, cancellationToken);
            var history = await _chatStore.LoadHistoryAsync(chatId, cancellationToken);

            _logger.LogInformation("🧠 Context loaded: {HistoryCount} previous messages in conversation {ChatId}", history.Count, chatId);

            var systemPrompt = BuildSystemPrompt(_tools);
            var recentToolCalls = new List<string>(); // Track recent tool calls to detect loops

            // Act loop - simplified version
            while (currentStep < maxSteps && !cancellationToken.IsCancellationRequested)
            {
                currentStep++;

                var backend = _router.GetBackend(userPrompt, history.Count);
                await _toolBus.EmitStepStartAsync(chatId, currentStep, userPrompt, history.Count);
                await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = "Starting step " + currentStep });


                var result = await backend.CompleteAsync(systemPrompt, userPrompt, history, null, cancellationToken);

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    await _toolBus.EmitRawModelAsync(chatId, currentStep, result.Text.Substring(0, Math.Min(result.Text.Length, 4000)));
                }

                var toolCall = ToolCallParser.TryParse(result);
                if (toolCall is null)
                {
                    var finalAnswer = result.Text ?? string.Empty;
                    
                    // Detect if LLM is trying to explain document creation instead of calling tool
                    var lowerAnswer = finalAnswer.ToLowerInvariant();
                    if ((lowerAnswer.Contains("generate") || lowerAnswer.Contains("create") || lowerAnswer.Contains("i'll") || lowerAnswer.Contains("i will")) &&
                        (lowerAnswer.Contains("pdf") || lowerAnswer.Contains("docx") || lowerAnswer.Contains("document")) &&
                        !lowerAnswer.Contains("unable") && !lowerAnswer.Contains("cannot") && !lowerAnswer.Contains("can't"))
                    {
                        _logger.LogWarning("⚠️ DETECTED: LLM is explaining document creation instead of calling tool. Response: {Response}", finalAnswer.Substring(0, Math.Min(200, finalAnswer.Length)));
                        
                        // Add helpful message to guide the LLM
                        var toolHint = "\n\n⚠️ Note: To actually create the file, you must output a tool call in JSON format like:\n" +
                                     "{\"tool\":\"PdfCreate\",\"args\":{\"title\":\"...\",\"content\":\"...\",\"fileName\":\"...\"}}";
                        finalAnswer += toolHint;
                    }
                    
                    await _toolBus.EmitFinalAsync(chatId, currentStep, finalAnswer);
                    await _chatStore.AppendAssistantAsync(chatId, finalAnswer, cancellationToken);
                    await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = "Final Answer: " + finalAnswer, Level = "success" });
                    await _toolBus.EmitTimelineAsync(chatId, "final", "Final answer produced.");
                    run.Status = Domain.Entities.Enums.RunStatus.Completed;
                    break;
                }
                else
                {
                    // Check for repetitive tool calls (same tool + same args)
                    var toolName = toolCall.Name?.ToString() ?? "UnknownTool";
                    var toolArgs = toolCall.Arguments.ToString() ?? "{}";
                    var toolSignature = $"{toolName}:{toolArgs}";
                    
                    recentToolCalls.Add(toolSignature);
                    if (recentToolCalls.Count > 15)
                        recentToolCalls.RemoveAt(0); // Keep last 15 calls for better pattern detection
                    
                    // Count identical calls (same tool AND same arguments)
                    var identicalCallCount = recentToolCalls.Count(t => t == toolSignature);
                    
                    // Check for alternating pattern (A-B-A-B-A-B) which indicates stuck behavior
                    var isAlternatingPattern = false;
                    if (recentToolCalls.Count >= 6)
                    {
                        var last6 = recentToolCalls.Skip(Math.Max(0, recentToolCalls.Count - 6)).ToList();
                        // Check if we see A-B-A-B-A-B pattern
                        if (last6.Count == 6 && 
                            last6[0] == last6[2] && last6[2] == last6[4] &&
                            last6[1] == last6[3] && last6[3] == last6[5] &&
                            last6[0] != last6[1])
                        {
                            isAlternatingPattern = true;
                        }
                    }
                    
                    // ONLY stop if: 
                    // (1) Exact same call 3+ times consecutively, OR 
                    // (2) Alternating A-B-A-B-A-B pattern detected
                    // Allow productive tool usage without arbitrary limits!
                    
                    // Check for 3 consecutive identical calls
                    var lastThree = recentToolCalls.Skip(Math.Max(0, recentToolCalls.Count - 3)).ToList();
                    var threeConsecutiveIdentical = lastThree.Count == 3 && 
                                                     lastThree[0] == lastThree[1] && 
                                                     lastThree[1] == lastThree[2];
                    
                    if (threeConsecutiveIdentical)
                    {
                        var loopWarning = $"⚠️ Detected stuck behavior: {toolName} called 3 times consecutively with identical arguments.\n\n" +
                                        "This suggests either:\n" +
                                        "• The tool is not providing useful results\n" +
                                        "• The tool might not be the right choice for this task\n" +
                                        "• The requested operation isn't possible with available tools\n\n" +
                                        "Please provide a final answer with the information gathered so far, or suggest an alternative approach if the tool cannot accomplish the goal.";
                        await _toolBus.EmitFinalAsync(chatId, currentStep, loopWarning);
                        await _chatStore.AppendAssistantAsync(chatId, loopWarning, cancellationToken);
                        await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = loopWarning, Level = "warning" });
                        await _toolBus.EmitTimelineAsync(chatId, "warning", "Stuck behavior detected (3 consecutive identical calls)");
                        run.Status = Domain.Entities.Enums.RunStatus.Completed;
                        break;
                    }
                    else if (isAlternatingPattern)
                    {
                        var loopWarning = $"⚠️ Detected repetitive pattern: Agent is cycling between the same two tool calls repeatedly.\n\n" +
                                        "This indicates the agent is stuck and not making progress.\n\n" +
                                        "Please provide a final answer with the information gathered so far, or try a different approach.";
                        await _toolBus.EmitFinalAsync(chatId, currentStep, loopWarning);
                        await _chatStore.AppendAssistantAsync(chatId, loopWarning, cancellationToken);
                        await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = loopWarning, Level = "warning" });
                        await _toolBus.EmitTimelineAsync(chatId, "warning", "Alternating pattern detected");
                        run.Status = Domain.Entities.Enums.RunStatus.Completed;
                        break;
                    }
                    
                    var toolResult = await ExecuteTool(run.Id, chatId, currentStep, toolCall, cancellationToken);

                    // Add the tool call and its result to the history
                    await _chatStore.AppendAssistantAsync(chatId, result.Text ?? string.Empty, cancellationToken);
                    var toolResultString = toolResult?.ToString() ?? string.Empty;
                    await _chatStore.AppendToolResultAsync(chatId, toolName, toolResultString, cancellationToken);
                    
                    // ✅ FORCE COMPLETION after successful document creation
                    // Check if tool is a document creation tool and returned success
                    var documentCreationTools = new[] { "PdfCreate", "DocxCreate", "ExcelCreate", "PptxCreate", "ChartCreate" };
                    if (documentCreationTools.Contains(toolName) && toolResult != null)
                    {
                        var resultJson = toolResultString.ToLowerInvariant();
                        // If result contains fileName or downloadUrl, the file was created successfully
                        if (resultJson.Contains("filename") || resultJson.Contains("downloadurl") || resultJson.Contains("created"))
                        {
                            _logger.LogInformation("✅ Document creation successful: {ToolName}. Injecting completion hint.", toolName);
                            
                            // Add a strong hint via tool result to force completion
                            var completionHint = "⚠️ CRITICAL INSTRUCTION: The file was created SUCCESSFULLY. You MUST now:\n" +
                                               "1. Provide a brief confirmation: 'Created [filename] successfully'\n" +
                                               "2. Include the download link from the tool result\n" +
                                               "3. STOP immediately - DO NOT call PdfCreate/DocxCreate again!\n" +
                                               "4. DO NOT try to recreate or improve the file!\n" +
                                               "Respond with plain text (not JSON) to complete the task.";
                            await _chatStore.AppendToolResultAsync(chatId, "SYSTEM_COMPLETION_HINT", completionHint, cancellationToken);
                        }
                    }
                }

                history = await _chatStore.LoadHistoryAsync(chatId, cancellationToken);
            }
            
            // Handle limit exceeded
            if (run.Status == Domain.Entities.Enums.RunStatus.InProgress)
            {
                var limitMessage = $"⚠️ LIMIT EXCEEDED: Maximum steps ({maxSteps}) reached. Task may be incomplete.";
                await _toolBus.EmitFinalAsync(chatId, currentStep, limitMessage);
                await _chatStore.AppendAssistantAsync(chatId, limitMessage, cancellationToken);
                await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = limitMessage, Level = "warning" });
                await _toolBus.EmitTimelineAsync(chatId, "warning", "Step limit reached");
                run.Status = Domain.Entities.Enums.RunStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in agent loop");
            await _toolBus.EmitFinalAsync(chatId, currentStep, "Agent error: " + ex.Message);
            await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = run.Id, StepId = currentStep, Message = "Agent error: " + ex.Message, Level = "error" });
            await _toolBus.EmitTimelineAsync(chatId, "error", ex.Message);
            run.Status = Domain.Entities.Enums.RunStatus.Failed;
        }
        finally
        {
            run.EndedAt = DateTime.UtcNow;
            await _runRepository.UpdateAsync(run);
            await _toolBus.EmitTimelineAsync(chatId, "run", $"Run ended: {run.Status}");
        }
    }

    private async Task<object?> ExecuteTool(Guid runId, string chatId, int step, dynamic toolCall, CancellationToken ct)
    {
        await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = runId, StepId = step, Message = "Executing tool: " + toolCall.Name, PayloadJson = toolCall.Arguments.ToString() });
        try
        {
            var tool = _tools.FirstOrDefault(t => t.Name == toolCall.Name);
            if (tool is null)
            {
                await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = runId, StepId = step, Message = "Unknown tool: " + toolCall.Name, Level = "error" });
                return null;
            }

            await _toolBus.EmitToolStartAsync(chatId, step, toolCall.Name, toolCall.Arguments);
            var result = await tool.InvokeAsync(toolCall.Arguments, ct);
            await _toolBus.EmitToolEndAsync(chatId, step, toolCall.Name, result);

            var resultString = result?.ToString() ?? "";
            await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = runId, StepId = step, Message = "Tool executed: " + toolCall.Name, PayloadJson = resultString });
            await _toolBus.EmitTimelineAsync(chatId, "tool", $"Executed {toolCall.Name}");

            await CaptureArtifactsAsync(runId, chatId, step, toolCall.Name, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool failed");
            await _stepLogRepository.AddAsync(new Domain.Entities.StepLog { RunId = runId, StepId = step, Message = "Tool failed: " + ex.Message, Level = "error" });
            return null;
        }
    }

    private async Task CaptureArtifactsAsync(Guid runId, string chatId, int step, string toolName, object? result)
    {
        if (result is null) return;

        JsonElement root;
        if (result is JsonElement jsonElement)
        {
            root = jsonElement;
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(result);
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
            }
            catch
            {
                return;
            }
        }

        foreach (var descriptor in ExtractFileDescriptors(root))
        {
            var artifact = new Domain.Entities.Artifact
            {
                RunId = runId,
                Kind = "file",
                FileName = descriptor.FileName,
                Path = descriptor.FilePath,
                MimeType = descriptor.MimeType,
                Size = descriptor.SizeBytes
            };
            await _artifactRepository.AddAsync(artifact);
            await _stepLogRepository.AddAsync(new Domain.Entities.StepLog
            {
                RunId = runId,
                StepId = step,
                Message = $"Created artifact from {toolName}: {artifact.FileName}"
            });

            var downloadUrl = descriptor.DownloadUrl ?? $"/api/files/{artifact.FileName}";
            await _toolBus.EmitFileCreatedAsync(chatId, step, artifact.FileName, downloadUrl, artifact.Size);
            await _toolBus.EmitTimelineAsync(chatId, "artifact", $"File created: {artifact.FileName}", new { downloadUrl, artifact.Size });
        }
    }

    private static IEnumerable<FileDescriptor> ExtractFileDescriptors(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("fileName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String &&
                element.TryGetProperty("filePath", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
            {
                var descriptor = new FileDescriptor
                {
                    FileName = nameProp.GetString() ?? string.Empty,
                    FilePath = pathProp.GetString() ?? string.Empty,
                    DownloadUrl = element.TryGetProperty("downloadUrl", out var urlProp) && urlProp.ValueKind == JsonValueKind.String ? urlProp.GetString() : null,
                    SizeBytes = element.TryGetProperty("sizeBytes", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number ? sizeProp.GetInt64() : 0,
                    MimeType = element.TryGetProperty("mimeType", out var mimeProp) && mimeProp.ValueKind == JsonValueKind.String ? mimeProp.GetString() ?? GetMimeType(pathProp.GetString()) : GetMimeType(pathProp.GetString())
                };
                if (!string.IsNullOrWhiteSpace(descriptor.FileName) && !string.IsNullOrWhiteSpace(descriptor.FilePath))
                {
                    yield return descriptor;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in ExtractFileDescriptors(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in ExtractFileDescriptors(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static string GetMimeType(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "application/octet-stream";
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ics" => "text/calendar",
            _ => "application/octet-stream"
        };
    }

    private sealed record FileDescriptor
    {
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string? DownloadUrl { get; init; }
        public string MimeType { get; init; } = "application/octet-stream";
        public long SizeBytes { get; init; }
    }

    private static string BuildSystemPrompt(IReadOnlyList<ITool> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an AI agent with access to powerful tools. Your knowledge cutoff is in the past, so you MUST use tools for current information.");
        sb.AppendLine();
        sb.AppendLine("💬 CONTEXT AWARENESS:");
        sb.AppendLine("- You receive conversation history with EVERY request");
        sb.AppendLine("- Review previous messages to understand context and references");
        sb.AppendLine("- When user says 'it', 'that', 'the file', check history for what they're referring to");
        sb.AppendLine("- Build upon previous answers instead of repeating them");
        sb.AppendLine("- Remember files you created, data you analyzed, searches you performed");
        sb.AppendLine();
        sb.AppendLine("⚠️ CRITICAL RULES - MUST FOLLOW:");
        sb.AppendLine("0. DOCUMENT CREATION DETECTION:");
        sb.AppendLine("   IF user message contains ANY of these patterns:");
        sb.AppendLine("   • 'generate' + 'PDF' or 'DOCX'");
        sb.AppendLine("   • 'create' + 'PDF' or 'DOCX' or 'document'");
        sb.AppendLine("   • 'make' + 'PDF' or 'DOCX'");
        sb.AppendLine("   • 'prepare' + 'PDF' or 'document'");
        sb.AppendLine("   THEN you MUST output tool call JSON immediately:");
        sb.AppendLine("   {\"tool\":\"PdfCreate\",\"args\":{\"title\":\"...\",\"content\":\"[full text here]\",\"fileName\":\"...\"}}");
        sb.AppendLine("   DO NOT write content in chat. DO NOT explain what you'll do. JUST call the tool!");
        sb.AppendLine();
        sb.AppendLine("1. TOOL FORMAT: Output ONLY JSON → {\"tool\":\"Name\",\"args\":{...}}");
        sb.AppendLine("2. WHEN TO USE TOOLS:");
        sb.AppendLine("   - ANY question about current events, news, schedules, scores → USE WebSearch");
        sb.AppendLine("   - Questions with 'latest', 'current', 'today', 'now', 'next' → USE WebSearch");
        sb.AppendLine("   - User says 'create', 'generate', 'make' + 'PDF'/'DOCX'/'document' → MUST USE PdfCreate/DocxCreate");
        sb.AppendLine("   - User wants to save/export content → USE appropriate creation tool (PdfCreate/DocxCreate/ExcelCreate)");
        sb.AppendLine("   - Need Excel creation or format conversion → USE ExcelCreate/CsvToXlsx/PdfToDocx");
        sb.AppendLine("   - Need OCR, audio transcription, or PDF summaries → USE ImageTextExtract/AudioTranscribe/PdfSummarize");
        sb.AppendLine("   - Data analysis or charting → USE DataAnalyze (set generateChart=true for charts or call ChartCreate directly)");
        sb.AppendLine("   - ONLY answer without tools if it's pure math, logic, or general knowledge from training");
        sb.AppendLine("3. COMPLETION: When you have enough information → respond with plain text (NOT JSON)");
        sb.AppendLine("4. NO IDENTICAL CALLS: Never call the same tool with the exact same arguments twice");
        sb.AppendLine("5. BE EFFICIENT: Aim for 2-4 tool calls maximum before providing a final answer");
        sb.AppendLine("6. KNOW YOUR LIMITS: If a tool doesn't exist, suggest an alternative approach!");
        sb.AppendLine();
        sb.AppendLine("� EXAMPLES - WHEN TO USE WebSearch:");
        sb.AppendLine("✅ 'What is Azerbaijan's next football match?' → USE WebSearch (current schedule info)");
        sb.AppendLine("✅ 'Latest AI news' → USE WebSearch (current events)");
        sb.AppendLine("✅ 'Stock price of Tesla' → USE WebSearch (current data)");
        sb.AppendLine("✅ 'Weather in New York' → USE WebSearch (current conditions)");
        sb.AppendLine("❌ 'What is 2+2?' → Answer directly (basic math, no tool needed)");
        sb.AppendLine("❌ 'Explain photosynthesis' → Answer directly (general knowledge)");
        sb.AppendLine();
        sb.AppendLine("🔍 WEB RESEARCH STRATEGY (Enhanced - Like ChatGPT Search):");
        sb.AppendLine("- WebSearch automatically fetches FULL content from top 3 results");
        sb.AppendLine("- Each result includes: title, url, snippet, AND full article text");
        sb.AppendLine("- You get detailed, up-to-date information in ONE call!");
        sb.AppendLine("- For most queries, ONE WebSearch call is enough");
        sb.AppendLine("- Only search again if you need different perspectives/topics");
        sb.AppendLine();
        sb.AppendLine("⚠️ IMPORTANT: WebSearch now extracts full content automatically!");
        sb.AppendLine("- NO need for WebBrowser for simple searches");
        sb.AppendLine("- WebBrowser is for: screenshots, clicking buttons, form filling");
        sb.AppendLine("- WebSearch handles: news, articles, info lookup, research");
        sb.AppendLine();
        sb.AppendLine("📝 HOW TO ANSWER AFTER WEB SEARCH:");
        sb.AppendLine("✅ DO THIS:");
        sb.AppendLine("   1. Read the search results carefully - you have FULL article content!");
        sb.AppendLine("   2. Extract the EXACT answer the user wants (date, name, number, fact)");
        sb.AppendLine("   3. Start your response with the DIRECT ANSWER immediately");
        sb.AppendLine("   4. Then provide context and details");
        sb.AppendLine("   5. Cite sources using the URLs provided: [1], [2], [3]");
        sb.AppendLine();
        sb.AppendLine("❌ DON'T DO THIS:");
        sb.AppendLine("   - DON'T say 'I searched the web and found...' (too verbose)");
        sb.AppendLine("   - DON'T give vague summaries when user wants specific facts");
        sb.AppendLine("   - DON'T repeat the user's question back to them");
        sb.AppendLine("   - DON'T say 'According to search results' at the start");
        sb.AppendLine();
        sb.AppendLine("💡 EXAMPLES:");
        sb.AppendLine("User: 'When is the next Azerbaijan football match?'");
        sb.AppendLine("✅ Good: 'Azerbaijan plays against Turkey on March 25, 2025 at 19:00 (Baku time) in the UEFA Euro qualifiers. The match will be held at the Olympic Stadium in Baku. [1]'");
        sb.AppendLine("❌ Bad: 'I searched for information about Azerbaijan football matches. Here's what I found: There are several matches scheduled...'");
        sb.AppendLine();
        sb.AppendLine("User: 'Who is the richest person?'");
        sb.AppendLine("✅ Good: 'Elon Musk is currently the world's richest person with a net worth of $245 billion as of January 2025, according to Forbes. [1] His wealth primarily comes from Tesla and SpaceX holdings.'");
        sb.AppendLine("❌ Bad: 'Based on the search results, there are several wealthy individuals. Let me break this down by category...'");
        sb.AppendLine();
        sb.AppendLine("📊 DATA/FILE CREATION:");
        sb.AppendLine("- Excel from scratch: Use ExcelCreate for multi-sheet XLSX workbooks");
        sb.AppendLine("- Quick CSV tables: Use FileWriter with CSV format (comma-separated values)");
        sb.AppendLine("- PDF/Word: Use PdfCreate, DocxCreate, or PdfToDocx for conversions");
        sb.AppendLine("- CSV<->Excel: Use CsvToXlsx when users want spreadsheets from CSV inputs");
        sb.AppendLine("- Note: ExcelRead is for READING existing Excel files, not creating them!");
        sb.AppendLine();
        sb.AppendLine("📄 DOCUMENT GENERATION WORKFLOW (CRITICAL - READ CAREFULLY):");
        sb.AppendLine("When user says 'generate PDF', 'create PDF', 'make a PDF' or similar:");
        sb.AppendLine();
        sb.AppendLine("❌ WRONG APPROACH - DO NOT DO THIS:");
        sb.AppendLine("   'I'll create a detailed text... [writes long content in chat]'");
        sb.AppendLine("   ↑ This is WRONG! User can't download text from chat!");
        sb.AppendLine();
        sb.AppendLine("✅ CORRECT APPROACH - DO THIS INSTEAD:");
        sb.AppendLine("   Step 1: Prepare content mentally/briefly");
        sb.AppendLine("   Step 2: Output ONLY this JSON (example):");
        sb.AppendLine("   {\"tool\":\"PdfCreate\",\"args\":{\"title\":\"How Printers Work\",\"content\":\"[full scientific text here]\",\"fileName\":\"printer-science.pdf\"}}");
        sb.AppendLine("   Step 3: Wait for tool result with downloadUrl");
        sb.AppendLine("   Step 4: Confirm: 'Created printer-science.pdf - [downloadUrl]'");
        sb.AppendLine();
        sb.AppendLine("⚠️ REMEMBER: Users need ACTUAL FILES, not text in chat!");
        sb.AppendLine("⚠️ When they say 'generate PDF' → You MUST call PdfCreate tool!");
        sb.AppendLine();
        sb.AppendLine("🛑 WHEN TO STOP (respond with text, not JSON):");
        sb.AppendLine("- ⚠️ IMMEDIATELY AFTER creating a file (PdfCreate/DocxCreate/ExcelCreate returned SUCCESS) → Provide simple confirmation with download link, THEN STOP!");
        sb.AppendLine("  Example: 'Created printer-science.pdf successfully. Download: [url]' ← THAT'S IT! DO NOT create again!");
        sb.AppendLine("- AFTER using WebSearch and getting results → Summarize key findings with details");
        sb.AppendLine("- AFTER tool returned empty/error → Explain limitation and provide best answer");
        sb.AppendLine("- AFTER 3-4 tool calls → Provide comprehensive answer with all gathered data");
        sb.AppendLine("- NEVER stop before using tools for current information questions!");
        sb.AppendLine("- Tool doesn't exist → Explain limitation and suggest alternative");
        sb.AppendLine();
        sb.AppendLine("⛔ NEVER DO THIS:");
        sb.AppendLine("- DO NOT call PdfCreate/DocxCreate multiple times for the same document");
        sb.AppendLine("- DO NOT regenerate files that were already created successfully");
        sb.AppendLine("- DO NOT try to 'improve' a file by creating it again");
        sb.AppendLine("- If tool result shows fileName and downloadUrl → The file is DONE! Just confirm and stop!");
        sb.AppendLine();
        sb.AppendLine("Available Tools: " + string.Join(", ", tools.Select(t => t.Name)));
        sb.AppendLine();
        sb.AppendLine("Tool Examples:");
        sb.AppendLine("WebSearch: {\"tool\":\"WebSearch\",\"args\":{\"query\":\"AI advancements 2025\"}}");
        sb.AppendLine("PdfCreate (IMPORTANT!): {\"tool\":\"PdfCreate\",\"args\":{\"title\":\"Research Paper\",\"content\":\"Full document text goes here with all paragraphs and sections...\",\"fileName\":\"research.pdf\"}}");
        sb.AppendLine("DocxCreate: {\"tool\":\"DocxCreate\",\"args\":{\"title\":\"Report\",\"content\":\"Document content...\",\"fileName\":\"report.docx\"}}");
        sb.AppendLine("DataAnalyze with chart: {\"tool\":\"DataAnalyze\",\"args\":{\"path\":\"workspace/sales.csv\",\"generateChart\":true}}");
        sb.AppendLine("Image OCR: {\"tool\":\"ImageTextExtract\",\"args\":{\"path\":\"https://example.com/receipt.png\"}}");
        sb.AppendLine("Audio transcription: {\"tool\":\"AudioTranscribe\",\"args\":{\"path\":\"workspace/interview.mp3\"}}");
        sb.AppendLine("Pdf summary: {\"tool\":\"PdfSummarize\",\"args\":{\"path\":\"workspace/report.pdf\",\"maxPages\":5}}");
        sb.AppendLine("Excel workbook: {\"tool\":\"ExcelCreate\",\"args\":{\"fileName\":\"analysis.xlsx\",\"sheets\":[{\"name\":\"Summary\",\"rows\":[]}]}}");
        sb.AppendLine("WebBrowser (goto): {\"tool\":\"WebBrowser\",\"args\":{\"action\":\"goto\",\"url\":\"https://example.com\"}}");
        sb.AppendLine("WebBrowser (getText): {\"tool\":\"WebBrowser\",\"args\":{\"action\":\"getText\"}}");
        sb.AppendLine("DocxCreate: {\"tool\":\"DocxCreate\",\"args\":{\"fileName\":\"report.docx\",\"title\":\"Title\",\"content\":\"...\"}}");
        sb.AppendLine("PdfCreate: {\"tool\":\"PdfCreate\",\"args\":{\"fileName\":\"doc.pdf\",\"title\":\"Title\",\"content\":\"...\"}}");
        sb.AppendLine("FileWriter (CSV): {\"tool\":\"FileWriter\",\"args\":{\"fileName\":\"data.csv\",\"content\":\"Col1,Col2\\nVal1,Val2\"}}");
        sb.AppendLine();
        sb.AppendLine("Remember: Quality over quantity. Provide comprehensive answers based on what you find!");

        return sb.ToString();
    }
}
