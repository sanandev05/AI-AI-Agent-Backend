# AI&AI Agent Backend (.NET 8)

Autonomous multi-tool agent backend inspired by Manus AI. Built with ASP.NET Core 8, Semantic Kernel, Playwright, SignalR, and a growing toolset for research, browsing, analysis, and artifact generation (DOCX/PDF/PPTX/PNG/ICS/EML).

The agent runs a sequential plan/act loop, emits live events, and saves artifacts in a workspace folder served by the Files API.

## Architecture

- API: ASP.NET Core Web API + SignalR hub for real-time events
- Application/Domain: Agent loop, planning, orchestration, contracts
- Infrastructure: Tools (WebBrowse, ResearchSummarize, DataAnalyze, ChartCreate, PdfCreate, PptxCreate, DocxRead/Write, Csv/Excel, Email, Calendar, ProductCompare, Tasks, Translate), services (URL safety, approvals)
- Persistence: EF Core (AIDbContext) for identity and chat storage
- Auth: JWT Bearer (single scheme) + ASP.NET Identity users

## Getting Started

### Prerequisites

- .NET 8 SDK
- Windows PowerShell (v5+)
- Optional: API keys for OpenAI and/or Google Gemini

### Configure appsettings

Edit `AI&AI Agent.API/appsettings.Development.json` and set at minimum:

```json
{
  "Jwt": {
    "Key": "<32+ char random secret>",
    "Issuer": "AI-Agent",
    "Audience": "AI-Agent-Clients",
    "ExpireMinutes": 120
  },
  "OpenAI": { "ApiKey": "sk-...", "ModelId": "gpt-4o" },
  "Google": { "ApiKey": "AIza..." },
  "Agent": {
    "WorkspacePath": "workspace",
    "Backends": {
      "OpenAI:Default": { "Provider": "OpenAI", "ModelId": "gpt-4o", "ApiKey": "sk-..." }
    }
  }
}
```

### Build and Run (Dev)

```powershell
dotnet build "AI&AI Agent Backend.sln" -v:m
dotnet run --project "AI&AI Agent.API/AI&AI Agent.API.csproj"
```

Defaults: Swagger at /swagger (dev), SignalR hub at /hubs/agent-events, Files API at /api/files.

## Authentication

1) Register and login to get a JWT:

- POST `/api/identity/register` { email, password, isPersistent }
- POST `/api/identity/login` { email, password } → { token }

2) Send `Authorization: Bearer <token>` on all protected endpoints.

Diagnostics: GET `/api/diagnostics/jwt` shows whether JWT config is present (dev only).

## Core Endpoints

- Chat
  - POST `/api/chat/create` → create chat
  - GET `/api/chat/list` → list chats
  - GET `/api/chat/{uid}` → get chat by id
  - PUT `/api/chat/{uid}/rename` { newTitle }
  - DELETE `/api/chat/{uid}`
  - POST `/api/chat/stream` SSE streaming; body: `ChatRequestDto { chatId, message, model, imageUrls? }`
  - POST `/api/chat/web-search` SSE streaming; body: `WebSearchRequestDto { chatId, query }`

- Agent loop (SignalR-driven)
  - POST `/api/agent/chat/{chatId}/cancel` → stop a running plan
  - POST `/api/agent/chat/{chatId}` { prompt } → start agent turn (returns 202); subscribe to hub for events

- Files
  - GET `/api/files` → list artifacts
  - GET `/api/files/{fileName}` → download .docx/.png/.pdf/.pptx/.eml/.ics
  - DELETE `/api/files/{fileName}` → delete artifact

- Approvals (for sensitive actions like EmailSend)
  - GET `/api/approvals?status=Pending|Approved|Denied`
  - POST `/api/approvals/{id}/approve`
  - POST `/api/approvals/{id}/deny`

## SignalR Events (hub: /hubs/agent-events)

Clients should call `SubscribeToChat(chatId)` after connecting. Events emitted to the chat group:

- `step:start` { chatId, step, userPrompt, historyCount }
- `tool:start` { chatId, step, tool, args }
- `tool:end` { chatId, step, tool, result }
- `file:created` { chatId, step, fileName, downloadUrl, sizeBytes }
- `plan:created` { chatId, plan }
- `plan:updated` { chatId, plan }
- `final:answer` { chatId, step, text }

Chat streaming via SSE ends with a JSON chunk: `{ "type":"done" }` and includes a final `usage` event from the chat service with token counts.

## Tool Catalog (Implemented)

- WebBrowse: realistic browsing with action sequences (wait/type/click/press/submit), stealth, and raw HTTP fallback
- ResearchSummarize: multi-URL aggregation + extractive summary; optional DOCX export
- PdfRead, DocxRead, CsvAnalyze, ExcelRead: parse/ingest content
- DataAnalyze: stats, trend slope, anomaly detection
- ChartCreate: PNG charts via SkiaSharp
- PdfCreate: generate PDFs (iText7)
- PptxCreate: simple PPTX (title + bullets)
- DocxCreate: write DOCX text reports
- WebWatch: website change tracker
- ProductCompare: heuristic price/rating extraction and tiering
- Translate: LLM-based translation
- Tasks: to-do add/list/complete/delete (workspace JSON)
- CalendarCreate/List: export ICS and list
- EmailDraft/EmailSend: draft creates approval; send requires approval; writes .eml to workspace

Artifacts are saved under `workspace/` and available through the Files API. Supported types: .docx, .png, .pdf, .pptx, .eml, .ics

## Usage Examples

1) Create a chat and stream a response

```http
POST /api/chat/create
Authorization: Bearer <jwt>

{}
```

```http
POST /api/chat/stream
Accept: text/event-stream
Authorization: Bearer <jwt>
Content-Type: application/json

{ "chatId": "<guid>", "message": "Compare iPhone 15 vs Galaxy S24 and create a 1-page summary." }
```

2) Ask the agent loop to perform a goal and watch SignalR

```http
POST /api/agent/chat/{chatId}
Authorization: Bearer <jwt>
Content-Type: application/json

{ "prompt": "Research PLC basics, summarize, and export a PPTX with bullets." }
```

3) List and download artifacts

```http
GET /api/files
GET /api/files/<fileName>
```

## Configuration Notes

- Backends: set `Agent:Backends` with one or more entries. Supported Provider values: `OpenAI`, `AzureOpenAI`. If none valid, falls back to `OpenAI:ApiKey`.
- URL Safety: outbound browsing is enforced by a policy service. Denied URLs are blocked with an explanation.
- JWT Key: must be at least 32 bytes (HS256). The app validates at startup.

## Roadmap

- Per-bullet citation mapping in ResearchSummarize and two-pass synthesis
- Richer charts (multi-series) and embedding in PPTX
- Media (video/podcast) transcript ingestion and summarization
- Stronger retries/timeouts and parameter validation

## Troubleshooting

- 401/403: Ensure you’re sending `Authorization: Bearer <token>`. Visit `/api/identity/me` to inspect claims.
- Swagger auth: In Swagger UI, click Authorize and paste ONLY the raw JWT (no "Bearer " prefix).
- Playwright errors or bot checks: The tool auto-falls back to raw HTTP when possible; some sites may still block scraping.
- Missing files: Artifacts are written to `workspace/` in the API process directory; ensure the app has write permissions.