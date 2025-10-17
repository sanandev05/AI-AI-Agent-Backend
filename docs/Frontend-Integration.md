## Frontend Integration Guide — AI Agent API and Hub

This guide explains how to integrate your frontend with the AI Agent backend: authentication, REST endpoints, the SignalR hub, event types, and practical code snippets. It assumes the backend exposes a static test page at `/` and the API base at the same origin, but your frontend can run elsewhere by setting the correct API base URL and handling CORS.

If you prefer a working reference, see the sample `wwwroot/index.html` in the API project. The sections below extract the essentials for any frontend stack (React, Vue, Angular, or vanilla JS).

---

### Quickstart

- Start the API locally
  - Option A (Visual Studio): Open the solution, set the API project as Startup, press F5.
  - Option B (CLI): From the repo root:
    - Windows PowerShell
      - `dotnet restore`
      - `dotnet run --project "AI&AI Agent.API/AI&AI Agent.API.csproj"`
  - Note the listening URL shown in the console (for example, https://localhost:7210). Swagger UI should be at `/swagger`.

- Configure your frontend
  - Set API Base URL to the API origin, e.g., `https://localhost:7210`.
  - If auth is enabled, first call `POST /api/identity/login` to get a JWT. Use it for both REST (Authorization header) and SignalR (access_token).

---

## Authentication

The API uses JWT Bearer tokens for protected endpoints. Tokens are issued by:

- POST `/api/identity/login`
  - Request body
    ```json
    {
      "email": "<string>",
      "password": "<string>"
    }
    ```
  - Response body
    ```json
    {
      "success": true,
      "message": "Login successful",
      "token": "<JWT string>"
    }
    ```
  - Notes: Replace with your real auth in production. This endpoint typically allows anonymous access and returns a short-lived token.

How to use the token:
- REST: `Authorization: Bearer <token>`
- SignalR: Provide `accessTokenFactory` so the token is included as `access_token` in the connection query string.

---

## REST Endpoints

Base URL: `{API_BASE}` (e.g., `https://localhost:7210`)

1) POST `{API_BASE}/tasks/{taskId}/runs`
- Purpose: Start a new agent run for a given goal.
- Auth: Depends on configuration (default project allows anonymous unless you add [Authorize]).
- Request:
  ```json
  {
    "goal": "Research the current state of quantum computing and its potential applications"
  }
  ```
- Response (202 Accepted):
  ```json
  {
    "runId": "a2f0c9c7-0a11-4f7e-a987-1f7d1d8f8a42"
  }
  ```

2) GET `{API_BASE}/api/runs/{runId}/files`
- Purpose: List generated artifacts for a run.
- Auth: Optional or Required depending on configuration. If protected, send Bearer token.
- Response:
  ```json
  [
    {
      "name": "report.docx",
      "size": 123456,
      "mimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "createdUtc": "2025-09-29T10:00:00Z",
      "downloadUrl": "https://localhost:7210/api/runs/<runId>/files/report.docx"
    }
  ]
  ```

3) GET `{API_BASE}/api/runs/{runId}/files/{fileName}`
- Purpose: Download a specific artifact.
- Auth: Optional or Required depending on configuration.
- Response: File stream (use browser download).

4) GET `{API_BASE}/api/runs/{runId}/files/zip`
- Purpose: Download all artifacts for the run as a single ZIP.
- Auth: Optional or Required depending on configuration.
- Response: `application/zip` stream.

5) GET `{API_BASE}/api/runs/{runId}/files/provenance`
- Purpose: Get a JSON manifest of artifacts with SHA-256 and sizes.
- Auth: Optional or Required depending on configuration.
- Response:
  ```json
  [
    { "name": "report.docx", "sha256": "...", "size": 123456 },
    { "name": "sources.json", "sha256": "...", "size": 2345 }
  ]
  ```

---

## SignalR Hub

- URL: `{API_BASE}/hub/runs`
- Transport: SignalR (WebSockets with fallback)
- Auth: Required if the hub requires authorization. Provide token via `accessTokenFactory`.

Client-to-Server methods:
- `Join(runId: string)` — Subscribe to events for a specific run.
- `Grant(runId: string, stepId: string)` — Approve a pending step/tool.
- `Deny(runId: string, stepId: string, reason: string)` — Deny a pending step/tool.

Server-to-Client messages:
- `event` — Discrete lifecycle events. Each payload has a `$type` discriminator.
- `narration` — Free-form narration messages as the agent plans and executes.

### Event Types

All events include `$type` to make switching easy on the frontend. Below are the typical shapes (camelCase fields):

```ts
// Common envelope discriminator
type AgentEvent =
  | RunStarted
  | PermissionRequested
  | PermissionGranted
  | PermissionDenied
  | StepStarted
  | ToolOutput
  | StepSucceeded
  | StepFailed
  | RunSucceeded
  | RunFailed
  | ArtifactCreated;

export interface RunStarted {
  $type: 'RunStarted';
  runId: string;
  goal: string;
}

export interface PermissionRequested {
  $type: 'PermissionRequested';
  runId: string;
  stepId: string;
  tool: string;
  reason?: string;
}

export interface PermissionGranted {
  $type: 'PermissionGranted';
  runId: string;
  stepId: string;
}

export interface PermissionDenied {
  $type: 'PermissionDenied';
  runId: string;
  stepId: string;
  reason?: string;
}

export interface StepStarted {
  $type: 'StepStarted';
  runId: string;
  stepId: string;
  tool: string;
}

export interface ToolOutput {
  $type: 'ToolOutput';
  runId: string;
  stepId: string;
  summary: string; // human-readable summary of output
}

export interface StepSucceeded {
  $type: 'StepSucceeded';
  runId: string;
  stepId: string;
}

export interface StepFailed {
  $type: 'StepFailed';
  runId: string;
  stepId: string;
  message: string;
  attempt?: number;
}

export interface RunSucceeded {
  $type: 'RunSucceeded';
  runId: string;
  elapsed: { totalMinutes: number };
}

export interface RunFailed {
  $type: 'RunFailed';
  runId: string;
  message: string;
}

export interface ArtifactCreated {
  $type: 'ArtifactCreated';
  runId: string;
  artifact: { fileName: string; size?: number };
}

export interface Narration {
  runId: string;
  stepId: string; // 'PLAN', 'FINAL', or a step id
  message: string;
}
```

---

## Integration Snippets (JavaScript/TypeScript)

Install SignalR (npm) or use CDN.

```bash
npm install @microsoft/signalr
```

```ts
import * as signalR from '@microsoft/signalr';

const API_BASE = 'https://localhost:7210';
let token: string | null = null;
let connection: signalR.HubConnection | null = null;
let currentRunId: string | null = null;

async function login(email: string, password: string) {
  const resp = await fetch(`${API_BASE}/api/identity/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  if (!resp.ok) throw new Error(`Login failed ${resp.status}`);
  const data = await resp.json();
  token = data.token; // response includes { success, message, token }
}

async function connectHub() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE}/hub/runs`, {
      accessTokenFactory: () => token ?? ''
    })
    .withAutomaticReconnect()
    .build();

  connection.on('event', (evt: any) => {
    switch (evt.$type) {
      case 'RunStarted':
        console.log('Run started', evt.goal);
        break;
      case 'PermissionRequested':
        // show UI, then call approve() or deny()
        break;
      case 'ArtifactCreated':
        // refresh file list
        break;
    }
  });

  connection.on('narration', (n: any) => {
    console.log('Narration:', n.message);
  });

  await connection.start();
}

async function startRun(goal: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const resp = await fetch(`${API_BASE}/api/runs`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ goal })
  });
  if (!resp.ok) throw new Error(`Start run failed ${resp.status}`);
  const data = await resp.json();
  currentRunId = data.runId;
  await connection!.invoke('Join', currentRunId);
}

async function approve(stepId: string) {
  if (!currentRunId || !connection) return;
  await connection.invoke('Grant', currentRunId, stepId);
}

async function deny(stepId: string, reason?: string) {
  if (!currentRunId || !connection) return;
  await connection.invoke('Deny', currentRunId, stepId, reason ?? 'Denied by user');
}

async function listFiles(runId: string) {
  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const resp = await fetch(`${API_BASE}/api/runs/${runId}/files`, { headers });
  if (!resp.ok) throw new Error(`List files failed ${resp.status}`);
  const items = await resp.json();
  // Map server shape { name, size, mimeType, createdUtc, downloadUrl } to a simpler client shape if desired
  return items.map((it: any) => ({ fileName: it.name, size: it.size, url: it.downloadUrl, createdUtc: it.createdUtc, mimeType: it.mimeType }));
}
```

---

## UX Ideas

- Show a live narration panel that appends each `narration.message` and highlights PLAN/FINAL messages.
- Use an approval bar/modal when you receive `PermissionRequested` and call `Grant`/`Deny` accordingly.
- Auto-refresh the files list on `ArtifactCreated` and on `StepSucceeded`.

---

## Troubleshooting

- 404 Not Found when calling `/api/identity/login` or `/tasks/{taskId}/runs`:
  - Ensure your frontend points to the correct API Base URL (origin + port). Open the API Swagger at `{API_BASE}/swagger` to confirm endpoints.
  - If you have multiple API projects, confirm you’re running the one exposing these routes.

- 401 Unauthorized:
  - Acquire a token via `/api/identity/login` and include it: `Authorization: Bearer <token>` for REST, and `accessTokenFactory` for the hub.
  - Ensure clock skew isn’t invalidating your JWT (use the same machine time). Check `exp` claim.

- SignalR connection errors:
  - If the hub is protected, you must pass a valid token; reconnect after refreshing the token.
  - Verify the hub path `{API_BASE}/hub/runs` is accessible and not blocked by proxies/CORS.

- CORS issues when frontend runs on a different origin:
  - Enable CORS in the API to allow your frontend origin, or proxy requests through your frontend dev server to the API.

- Files endpoints security:
  - Depending on configuration, files may be protected. If you get 401s, add the `Authorization` header to files requests.

---

## Reference: Expected Shapes (JSON)

Run started event
```json
{ "$type": "RunStarted", "runId": "...", "goal": "..." }
```

Permission requested event
```json
{ "$type": "PermissionRequested", "runId": "...", "stepId": "...", "tool": "...", "reason": "..." }
```

Narration message
```json
{ "runId": "...", "stepId": "PLAN", "message": "Planning steps..." }
```

Artifact created
```json
{ "$type": "ArtifactCreated", "runId": "...", "artifact": { "fileName": "report.docx", "size": 123456 } }
```

Files list
```json
[
  { "fileName": "report.docx", "size": 123456 },
  { "fileName": "sources.json", "size": 2345 }
]
```

---

## Notes

- API and Hub namespaces follow `AI_AI_Agent.*` in code; this does not affect HTTP endpoints, which remain as documented.
- JSON casing is camelCase for properties. Events include a `$type` discriminator for easy client-side routing.
- The sample `index.html` demonstrates end-to-end flow and can serve as a blueprint for your own frontend implementation.
