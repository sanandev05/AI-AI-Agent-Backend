# GeneralAgent - Autonomous Agent Platform (.NET 8)

This project is a .NET 8 implementation of a general-purpose autonomous agent platform inspired by Manus AI. It uses Semantic Kernel for planning and tool orchestration, allowing users to submit complex, natural-language goals (e.g., "Analyze Tesla revenue reports and give me a DOCX") and receive structured artifacts like DOCX, PDF, and CSV files.

The agent operates on a ReAct (Reason-Act) loop, where it creates a plan, executes tools, observes the results, and iterates until the goal is achieved or constraints are met.

## Features

- **Multi-Step Planning:** Decomposes complex user prompts into a sequence of executable steps.
- **Extensible Tool Registry:** A collection of powerful, sandboxed tools for web interaction, file manipulation, data analysis, and content generation.
- **Multi-LLM Support:** Route tasks to different AI models (OpenAI, Gemini, Anthropic) based on cost and capability.
- **Background Job Processing:** Uses Hangfire to run complex jobs asynchronously.
- **Artifact Generation:** Produces downloadable DOCX, PDF, CSV, and PNG files.
- **Safety First:** Risky tools like code execution and Playwright are disabled by default and must be explicitly enabled.

## Quickstart

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell) (Core or Windows)
- (Optional) [Node.js](https://nodejs.org/) for Playwright tool.
- (Optional) An API key for OpenAI, Google Gemini, or Anthropic.

### 1. Environment Variables

You must provide an API key for at least one of the supported LLM providers. Set it as an environment variable before running the bootstrap script.

**Example (PowerShell):**
```powershell
$env:OPENAI_API_KEY="sk-..."
# or
$env:GEMINI_API_KEY="AIza..."
# or
$env:ANTHROPIC_API_KEY="sk-ant-..."
```

You can also set optional environment variables to control model selection and feature flags:
```powershell
$env:MODEL_PLANNER="gpt-4o-mini"
$env:MODEL_SYNTH="gpt-4o"
$env:PLAYWRIGHT_ENABLE="true"
$env:CODE_RUN_ENABLE="true"
```

### 2. Bootstrap the Solution

Open a new PowerShell terminal in an **empty directory** and run the bootstrap script. This will scaffold the entire solution, install dependencies, and build the project.

```powershell
# Ensure you are in an empty folder
# Save the bootstrap.ps1 content from the prompt into a file named bootstrap.ps1
./bootstrap.ps1
```

### 3. Run the API

Once the bootstrap is complete, you can run the API server.

```powershell
cd src/Api
dotnet run
```
The API will be available at `http://localhost:5079`.

### 4. Interact with the API

Use a tool like `curl` or an API client to interact with the agent.

**A) Start a Job:**

```bash
curl -X POST http://localhost:5079/api/jobs/run -H "Content-Type: application/json" -d '{
  "prompt": "Analyze the latest quarterly revenue report for Microsoft (MSFT) and generate a 1-page DOCX summary with a revenue chart.",
  "constraints": {
    "maxIterations": 10,
    "budgetUsd": 0.25,
    "domainHints": ["finance", "webscrape"]
  },
  "deliverables": ["docx"]
}'
```
This will return a `jobId`.

**B) Check Job Status:**

```bash
curl http://localhost:5079/api/jobs/{your-job-id}
```
Poll this endpoint until the status is `Succeeded` or `Failed`. A successful job will include artifact details with a `fileId`.

**C) Download the Artifact:**

```bash
curl -o report.docx http://localhost:5079/api/files/{your-file-id}
```

## Tool Catalog

The agent has access to the following tools. You can get a live list from the `/api/tools` endpoint.

| Tool                  | Description                                                                 |
| --------------------- | --------------------------------------------------------------------------- |
| `web.search`          | Searches the web for a query.                                               |
| `web.fetch`           | Fetches the content of a URL.                                               |
| `web.playwright`      | Runs a Playwright script for dynamic sites (disabled by default).           |
| `file.parse`          | Parses text from various file formats (PDF, DOCX, HTML, CSV).               |
| `data.extract`        | Extracts structured JSON data from text based on a schema.                  |
| `data.tabulate`       | Creates a CSV file from structured data.                                    |
| `report.compose`      | Converts Markdown text into a DOCX or PDF document.                         |
| `viz.chart`           | Generates a PNG chart (line, bar, pie) from data.                           |
| `code.run.csharp`     | Executes a C# snippet in a sandbox (disabled by default).                   |
| `util.merge`          | Merges multiple text blocks into a single Markdown document.                |
| `cite.collect`        | Collects and formats a list of source URLs.                                 |

## Troubleshooting

- **403 Forbidden from LLM Provider:** Your API key is likely invalid, expired, or lacks the correct permissions for the model you are trying to use.
- **PDF Parsing Fails:** `PdfPig` is robust but may struggle with complex layouts or scanned documents. The agent is designed to gracefully skip sources it cannot parse.
- **Job Fails with Max Iterations:** The agent could not complete the task within the allowed number of steps. Try increasing `maxIterations` or simplifying the prompt.
- **Playwright/Code Runner Disabled:** These tools are disabled by default for security. You must set the `PLAYWRIGHT_ENABLE=true` or `CODE_RUN_ENABLE=true` environment variable to use them.