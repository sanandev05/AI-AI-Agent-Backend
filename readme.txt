Multi-LLM Chatbot

Users can choose which LLM (GPT, Gemini, Grok, etc.) to query.

Supports real-time streaming responses (chunk-by-chunk, like ChatGPT).

Message history stored in a database (linked to user sessions).

Autonomous AI Agent

Powered by Semantic Kernel for planning and memory.

Capable of calling external APIs and executing tasks autonomously.

Web automation using Playwright (e.g., scrape data, interact with sites).

Task chaining: the agent can break down a goal into smaller steps.

Backend Implementation (.NET Core)

ASP.NET Core Web API as backend.

Clean Architecture (Domain, Application, Infrastructure, Presentation).

Repository & Service patterns for maintainability.

Authentication & Authorization with ASP.NET Identity.

# Project Chimera: Manus-Style Autonomous AI Agent Prototype

## Overview
Project Chimera is a prototype of a "Manus-style" autonomous AI agent, designed for high-level goal understanding, dynamic planning, tool use, self-correction, and learning. Built in C# with .NET and Microsoft Semantic Kernel, it demonstrates modular, extensible, and persistent agent capabilities.

## Architecture
- **IAgent**: Core agent interface for autonomy, planning, tool use, and memory.
- **ITool**: Plugin interface for agent tools (e.g., web search, file system, API clients).
- **IPlanner**: Interface for dynamic plan generation and adaptation (Semantic Kernel powered).
- **IMemory**: Interface for short-term and long-term memory modules.

### Key Classes
- `ChimeraAgent`: Implements IAgent, orchestrates planning, tool execution, and memory.
- `WebSearchTool`: Example ITool plugin for web search.
- `SemanticKernelPlanner`: Uses Semantic Kernel to generate/adapt plans.
- `ShortTermMemory` & `SemanticKernelLongTermMemory`: Memory modules for context and recall.

## Extensibility
- Add new tools by implementing `ITool` and registering with the agent.
- Swap planners or memory modules by implementing respective interfaces.

## Example Usage
```csharp
// Setup Semantic Kernel and memory (pseudo-code)
var kernel = new Kernel();
var semanticMemory = new SemanticTextMemory();

// Instantiate tools
var tools = new List<ITool> { new WebSearchTool() };

// Instantiate planner and memory
var planner = new SemanticKernelPlanner(kernel);
var memory = new ShortTermMemory(); // or new SemanticKernelLongTermMemory(semanticMemory)

// Create the agent
var agent = new ChimeraAgent(planner, memory, tools);

// Achieve a high-level goal
string result = await agent.AchieveGoalAsync("Find the latest research on autonomous agents");
Console.WriteLine(result);
```

## How It Works
1. **Goal Input**: User provides a high-level goal.
2. **Planning**: Agent uses planner (Semantic Kernel) to generate a plan (e.g., select tool and input).
3. **Tool Execution**: Agent executes the plan using the appropriate tool plugin.
4. **Self-Correction**: On failure, agent adapts the plan and retries.
5. **Memory**: Results and context are stored in memory for future learning.

## Requirements
- .NET 7+
- Microsoft.SemanticKernel NuGet package

## Extending Project Chimera
- Implement new tools by creating classes that implement `ITool`.
- Enhance planning by customizing `SemanticKernelPlanner` prompts or logic.
- Integrate richer memory by extending `IMemory` implementations.

---
This prototype provides a foundation for building advanced, autonomous, and extensible AI agents in .NET.
