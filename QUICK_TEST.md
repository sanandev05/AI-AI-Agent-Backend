# 🚀 Quick Test Guide - AI Agent

## Prerequisites Check ✓

Before testing, ensure you have:
- [ ] .NET 8 SDK installed
- [ ] SQL Server running
- [ ] OpenAI API key configured
- [ ] Database migrated

## 5-Minute Quick Test

### Step 1: Start the Server (30 seconds)

```powershell
cd 'c:\Users\Icomp\Desktop\Final Project\AI-AI-Agent-Backend'
dotnet run --project "AI&AI Agent.API/AI&AI Agent.API.csproj"
```

**Expected Output:**
```
Now listening on: https://localhost:7210
Now listening on: http://localhost:5216
```

### Step 2: Open Test Console (10 seconds)

Open in browser: **https://localhost:7210/test-agent.html**

### Step 3: Authenticate (1 minute)

1. Click **"Register New User"** tab
2. Enter email: `test@example.com`
3. Enter password: `Test123!`
4. Click **"Register New User"** button
5. System auto-logs you in ✅

### Step 4: Create Chat Session (10 seconds)

1. Click **"Create New Chat"** button
2. Copy the generated Chat ID (GUID)
3. Click **"Connect"** button
4. Wait for status: **"connected"** ✅

### Step 5: Test the Agent (2 minutes)

Try these test prompts:

#### **Test 1: Simple Calculation**
```
Calculate 15 * 23 and explain the result
```
**Expected**: Immediate response with calculation

#### **Test 2: File Creation**
```
Create a markdown file called 'test.md' with a list of 5 AI benefits
```
**Expected**: 
- See tool execution in Activity Log
- File appears in "Files & Downloads" section
- Can download the file

#### **Test 3: Research Task**
```
Research the basics of quantum computing and create a summary
```
**Expected**:
- Multiple tool calls (WebBrowse, ResearchSummarize)
- Real-time events in Activity Log
- Final summary response

### Step 6: Verify Results (1 minute)

Check the **Activity Log** for:
- 📊 Timeline events (run start/end)
- 🟢 Tool execution start
- 🔵 Tool execution end
- 📄 File creation notifications
- 🔴 Final answer

Check **Files & Downloads** for:
- Created artifacts (.md, .docx, .pdf, .pptx)
- Download buttons working

---

## Alternative Testing Methods

### Method A: HTTP File (VS Code REST Client)

1. Install "REST Client" extension in VS Code
2. Open `AGENT_TESTING.http`
3. Follow the numbered sections:
   - Register/Login (get token)
   - Create chat (get chatId)
   - Send test prompts
   - Download files

### Method B: Swagger UI

1. Open: https://localhost:7210/swagger
2. Click **"Authorize"** button
3. Get token from `/api/identity/login`
4. Paste token (WITHOUT "Bearer" prefix)
5. Test endpoints directly

### Method C: Programmatic Testing (cURL)

```powershell
# 1. Register
curl -X POST "https://localhost:7210/api/identity/register" `
  -H "Content-Type: application/json" `
  -d '{"email":"test@example.com","password":"Test123!","isPersistent":true}'

# 2. Login
$response = curl -X POST "https://localhost:7210/api/identity/login" `
  -H "Content-Type: application/json" `
  -d '{"email":"test@example.com","password":"Test123!"}' | ConvertFrom-Json
$token = $response.token

# 3. Create Chat
$chatResponse = curl -X POST "https://localhost:7210/api/chat/create" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{}' | ConvertFrom-Json
$chatId = $chatResponse.id

# 4. Test Agent
curl -X POST "https://localhost:7210/api/agent/chat/$chatId" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{"prompt":"Calculate 10 * 5 and explain"}'
```

---

## Test Scenarios by Feature

### 🎯 Core Features

| Feature | Test Prompt | Expected Result |
|---------|-------------|-----------------|
| **Calculation** | `Calculate 123 * 456` | Immediate math result |
| **File Creation** | `Create a summary.md file` | File in downloads |
| **Web Search** | `Search for AI news 2025` | Research results |
| **Document Export** | `Create a DOCX report about AI` | .docx file |

### 🚀 Phase 5 Features

| Feature | Test Prompt | Expected Result |
|---------|-------------|-----------------|
| **Workspace** | `Create a Python workspace` | Workspace with template files |
| **Code Analysis** | `Analyze this C# code: public class Test { }` | Code insights |
| **Code Execution** | `Execute Python: print(10*5)` | Execution output: 50 |
| **Knowledge Search** | `Search knowledge base for ML algorithms` | Semantic search results |
| **Document Analysis** | `Analyze the README.md file` | Document understanding |

### 🔧 Tool Testing

| Tool | Test Prompt | Expected Output |
|------|-------------|-----------------|
| **WebBrowse** | `Browse example.com` | Page content |
| **ResearchSummarize** | `Research and summarize AI` | Summary document |
| **PdfCreate** | `Create a PDF report` | .pdf file |
| **DocxCreate** | `Create a Word document` | .docx file |
| **PptxCreate** | `Create a PowerPoint` | .pptx file |
| **ChartCreate** | `Create a sales chart` | .png chart |
| **EmailDraft** | `Draft an email` | .eml file |
| **CalendarCreate** | `Create a meeting` | .ics file |
| **ProductCompare** | `Compare iPhone vs Galaxy` | Comparison table |
| **Translate** | `Translate "Hello" to Spanish` | "Hola" |

---

## Troubleshooting

### ❌ "Connection failed"
**Solution**: Check JWT token validity
```powershell
# Test auth endpoint
curl https://localhost:7210/api/identity/me -H "Authorization: Bearer YOUR_TOKEN"
```

### ❌ "Database error"
**Solution**: Apply migrations
```powershell
dotnet ef database update --project "AI&AI Agent.Persistance" --startup-project "AI&AI Agent.API"
```

### ❌ "OpenAI error"
**Solution**: Check API key in `appsettings.Development.json`
```json
{
  "OpenAI": {
    "ApiKey": "sk-YOUR_KEY_HERE",
    "ModelId": "gpt-4o"
  }
}
```

### ❌ "No events received"
**Solution**: Check browser console for WebSocket errors
- Open DevTools → Console
- Look for SignalR connection errors
- Verify JWT is valid

### ❌ "Tool execution failed"
**Solution**: Check server logs
```powershell
# Enable detailed logging in appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "AI_AI_Agent.Application": "Debug"
    }
  }
}
```

---

## Success Checklist

After testing, you should have:
- ✅ Successfully authenticated (JWT token received)
- ✅ Created chat session (Chat ID generated)
- ✅ Connected to SignalR (Status: "connected")
- ✅ Executed at least 3 test prompts
- ✅ Received real-time events in Activity Log
- ✅ Created and downloaded at least 1 artifact
- ✅ Verified tool execution (FileWriter, WebBrowse, etc.)

---

## Next Steps

Once basic testing works:

1. **Integration Testing**: Test all 21 services
2. **Load Testing**: Multiple concurrent users
3. **Phase 5 Deep Testing**: Test workspace isolation, code execution, knowledge retrieval
4. **Multi-Modal Testing**: Image/document/audio analysis (when APIs integrated)
5. **Production Testing**: Deploy to Azure/AWS and test with production data

---

## Quick Reference

| Resource | URL |
|----------|-----|
| **Test Console** | https://localhost:7210/test-agent.html |
| **Swagger UI** | https://localhost:7210/swagger |
| **Run Monitor** | https://localhost:7210/run-monitor.html |
| **Files API** | https://localhost:7210/api/files |
| **SignalR Hub** | wss://localhost:7210/hubs/agent-events |

## Documentation

- 📖 **Full Testing Guide**: `TESTING_GUIDE.md`
- 📖 **HTTP Tests**: `AGENT_TESTING.http`
- 📖 **Architecture**: `README.md`
- 📖 **Memory Bank**: `.github/memory-bank/`

---

**Happy Testing! 🎉**
