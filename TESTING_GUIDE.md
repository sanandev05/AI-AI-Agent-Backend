# AI Agent Desktop Visualization - Testing Guide

## Overview
This guide provides step-by-step instructions for testing the AI Agent's desktop visualization features, including real-time SignalR event streaming and chat logging.

## Prerequisites

### 1. Database Setup
Ensure your SQL Server is running and the database is configured:
```
Data Source=DESKTOP-SANAN;Initial Catalog=AI_AIAgentDB;Integrated Security=True;TrustServerCertificate=True
```

### 2. Configuration
Set the following in your `appsettings.Development.json` or user secrets:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=DESKTOP-SANAN;Initial Catalog=AI_AIAgentDB;Integrated Security=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "your-super-secret-jwt-key-that-is-at-least-32-characters-long",
    "Issuer": "AI-Agent-API",
    "Audience": "AI-Agent-Frontend"
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here",
    "ModelId": "gpt-4o"
  }
}
```

### 3. Apply Database Migrations
Run the following command to apply migrations:
```bash
dotnet ef database update --project "AI&AI Agent.Persistance" --startup-project "AI&AI Agent.API"
```

## Testing Steps

### Step 1: Start the Application
1. Open the solution in Visual Studio or use dotnet CLI
2. Run the API project (F5 or `dotnet run` from the API project folder)
3. The application should start on `https://localhost:7210` and `http://localhost:5216`

### Step 2: Access the Test Console
1. Navigate to: `https://localhost:7210/test-agent.html`
2. You should see the "AI Agent Test Console" interface

### Step 3: Authenticate

#### Option A: Register & Login (Recommended)
1. Click the "Login" tab in the authentication section
2. Enter an email and password (e.g., `test@example.com` / `Test123!`)
3. Click "Register New User" (first time)
4. The system should automatically log you in and get a JWT token
5. Verify the status shows "authorized"

#### Option B: Use Swagger for Manual Token Generation
1. Go to `https://localhost:7210/swagger`
2. Use POST `/api/identity/register` or `/api/identity/login`
3. Copy the returned token and paste it in the "Token" tab

### Step 4: Create and Connect to Chat
1. Click "Create New Chat" - this should generate a new GUID
2. Click "Connect" to establish SignalR connection
3. Verify connection status shows "connected"
4. Check the Activity Log for connection messages

### Step 5: Test Authentication
1. Click "Test Auth" button
2. Verify you see authentication details in the Activity Log
3. This confirms JWT authentication is working

### Step 6: Test SignalR Connection
1. Click "Test Connection" button
2. You should see a test message appear in the Activity Log
3. This confirms SignalR hub communication is working

### Step 7: Test Agent Execution

#### Simple Test Prompts:
1. **Calculator Test**: 
   ```
   Calculate 123 * 456 and explain the result
   ```

2. **File Creation Test** (Direct tool call):
   ```
   Create a summary report and save it as summary.md: {"tool":"FileWriter","args":{"fileName":"summary.md","content":"# Project Summary\n\n## Status\n- All systems operational\n- Tests passing\n- Ready for deployment"}}
   ```

3. **Research + Document Test**:
   ```
   Research the basics of artificial intelligence and create a summary document
   ```

### Step 8: Monitor Activity Log
During agent execution, you should see:
- 📊 **timeline** events: Run start/end, tool execution, errors
- 🟢 **start** events: Tool execution beginning  
- 🔵 **end** events: Tool execution completion
- 🔴 **final** events: Final answers from the agent
- 🟣 **raw** events: Raw model output (truncated)
- 📄 **info** events: File creation with download links

### Step 9: Verify File Creation
1. When FileWriter tool is used, check "Files & Downloads" section
2. Click on file links to download created files
3. Verify files are properly formatted (Markdown/Text)

### Step 10: Test Management Features
1. Go to "Management" tab
2. Use "Refresh" to reload chats and files
3. Test chat selection and deletion

## Expected SignalR Events

The system emits these SignalR events to the `agent-events` hub:

| Event | Description | Sample Data |
|-------|-------------|-------------|
| `timeline:log` | General timeline events | `{kind: "run", message: "Run started", data: {...}}` |
| `tool:start` | Tool execution begins | `{tool: "FileWriter", args: {...}}` |
| `tool:end` | Tool execution ends | `{tool: "FileWriter", result: {...}}` |
| `step:start` | Agent step begins | `{step: 1, userPrompt: "...", historyCount: 2}` |
| `raw:model` | Raw LLM output | `{rawText: "I'll help you with that..."}` |
| `final:answer` | Agent's final response | `{text: "Here's your answer..."}` |
| `file:created` | File was created | `{fileName: "summary.md", downloadUrl: "/api/files/summary.md"}` |
| `test:message` | Test message (diagnostic) | `{message: "Test message", sender: "System"}` |

## Troubleshooting

### Connection Issues
- **"Connection failed"**: Check JWT token validity and server logs
- **"Unauthorized"**: Verify JWT is properly set and not expired
- **"Error"**: Check browser console for detailed error messages

### Authentication Issues  
- **Login fails**: Check database connection and user creation
- **JWT invalid**: Ensure JWT key is configured and at least 32 characters

### SignalR Issues
- **No events received**: Check browser console for WebSocket errors
- **Authorization failed**: Verify JWT token in connection headers
- **Connection drops**: Check server logs for hub authorization failures

### Agent Execution Issues
- **Agent doesn't respond**: Check OpenAI API key configuration
- **Tool calls fail**: Verify tool registration in DI container
- **No file creation**: Check workspace folder permissions

### Database Issues
- **Connection fails**: Verify SQL Server is running and connection string is correct
- **Migration errors**: Run `dotnet ef database update` from the correct directory

## Server Logs to Monitor

Enable these log levels in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "AI_AI_Agent.API.Hubs": "Debug",
      "AI_AI_Agent.Application.Agent": "Debug"
    }
  }
}
```

Look for these log patterns:
- `[JWT] OnMessageReceived: Authorization present for GET wss://...` 
- `SignalR client connected: {ConnectionId}, User: {UserId}`
- `User {UserId} subscribed to chat {ChatId}`
- `Starting agent with prompt: ...`
- `Tool executed: FileWriter`

## Success Criteria

✅ **Authentication**: User can register/login and get valid JWT tokens
✅ **Connection**: SignalR connects successfully with "connected" status  
✅ **Events**: Real-time events appear in Activity Log during agent execution
✅ **Tools**: FileWriter creates downloadable files with proper links
✅ **Persistence**: Runs, StepLogs, and Artifacts are saved to database
✅ **UI**: Timeline shows step-by-step progress with proper event types
✅ **Downloads**: Created files are accessible via Files & Downloads section

The desktop visualization is working correctly when all events flow in real-time from backend to frontend, showing the complete agent execution process step-by-step.