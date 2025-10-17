# Frontend API Integration Guide - PDF Upload & Chat

## 🎯 Complete Workflow for PDF Q&A

To enable users to upload PDFs and ask questions about them, follow this endpoint flow:

---

## 📋 Step-by-Step API Flow

### **Step 1: User Authentication**

#### 1.1 Register User (if new)
```http
POST /api/identity/register
Content-Type: application/json

{
  "userName": "john.doe",
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

**Response:**
```json
{
  "userId": "user-guid-123",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "message": "User registered successfully"
}
```

#### 1.2 Login User
```http
POST /api/identity/login
Content-Type: application/json

{
  "userName": "john.doe",
  "password": "SecurePass123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "user-guid-123",
  "userName": "john.doe",
  "expiresAt": "2025-01-17T23:59:59Z"
}
```

⚠️ **Important**: Save the `token` - use it in `Authorization: Bearer {token}` header for all subsequent requests.

---

### **Step 2: Create a Chat Session**

```http
POST /api/chat/create
Authorization: Bearer {token}
Content-Type: application/json

{
  "title": "PDF Discussion - Research Paper"
}
```

**Response:**
```json
{
  "chatId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "PDF Discussion - Research Paper",
  "createdAt": "2025-01-17T10:30:00Z"
}
```

💡 **Save the `chatId`** - you'll need it for uploading files and sending messages.

---

### **Step 3: Upload PDF File**

```http
POST /api/files
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [Binary PDF file]
```

**Example using JavaScript:**
```javascript
const formData = new FormData();
formData.append('file', pdfFile); // pdfFile is a File object from input

const response = await fetch('https://your-api.com/api/files', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
  },
  body: formData
});

const result = await response.json();
console.log(result);
```

**Response:**
```json
{
  "name": "research_paper_20250117_103045.pdf",
  "size": 2458624,
  "createdUtc": "2025-01-17T10:30:45Z",
  "downloadUrl": "/api/files/research_paper_20250117_103045.pdf"
}
```

💾 **Save the `name`** (e.g., `research_paper_20250117_103045.pdf`) - you'll reference it in the chat.

---

### **Step 4: Ask Questions About the PDF**

You have **three options** for asking questions:

#### **Option A: Simple Chat (Recommended for Basic Q&A)**

```http
POST /api/chat/stream
Authorization: Bearer {token}
Content-Type: application/json

{
  "chatId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Read the file research_paper_20250117_103045.pdf and tell me the main findings",
  "modelKey": "gpt-4o"
}
```

**Response (Server-Sent Events Stream):**
```
data: {"type":"status","data":"Processing request..."}

data: {"type":"message_chunk","data":"Based on the PDF, the main findings are:\n\n1. "}

data: {"type":"message_chunk","data":"The research demonstrates..."}

data: {"type":"done"}
```

#### **Option B: Agent Mode (Recommended for Complex Tasks)**

For more intelligent PDF analysis with automatic tool usage:

```http
POST /api/agent/chat/{chatId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "prompt": "Analyze research_paper_20250117_103045.pdf and extract: 1) Main hypothesis 2) Methodology 3) Key results 4) Limitations"
}
```

**Response:**
```json
{
  "message": "Agent loop started. Listen for events on the SignalR hub.",
  "chatId": "550e8400-e29b-41d4-a716-446655440000"
}
```

💡 **Agent mode automatically**:
- Detects the PDF filename
- Uses `PdfReader` tool to extract text
- Analyzes content intelligently
- Provides structured answers
- Sends updates via SignalR

#### **Option C: One-Step Upload & Ask (NEW - Recommended for Simple Workflows)**

Upload any file (PDF, DOCX, XLSX, PPTX, images) and ask a question in **one request**:

```http
POST /api/agent/ask-file/{chatId}
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [Binary file]
question: "What are the key findings in this document?"
```

**Response:**
```json
{
  "fileName": "research_paper.pdf",
  "question": "What are the key findings in this document?",
  "answer": "Based on the document, the key findings are:\n\n1. The study demonstrates...\n2. Results indicate...\n3. Conclusions suggest...",
  "fileType": ".pdf",
  "timestamp": "2025-10-17T14:30:00Z"
}
```

**Example using JavaScript:**
```javascript
const formData = new FormData();
formData.append('file', pdfFile); // File from <input type="file">
formData.append('question', 'Summarize the main points of this document');

const response = await fetch(`https://your-api.com/api/agent/ask-file/${chatId}`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
  },
  body: formData
});

const result = await response.json();
console.log(result.answer);
```

💡 **This endpoint**:
- Works with **any file type** (PDF, DOCX, XLSX, PPTX, TXT, CSV, images)
- Automatically extracts content from the file
- Combines file content with your question
- Returns an immediate answer
- Saves both question and answer to chat history
- No need to reference filenames or manage uploads separately

**Supported file types:**
- 📄 PDF documents
- 📝 Word documents (.docx)
- 📊 Excel spreadsheets (.xlsx)
- 📑 Text files (.txt, .csv)
- 🖼️ Images (.jpg, .jpeg, .png) - OCR support coming soon

---

## 🔄 SignalR Real-Time Updates (for Agent Mode)

When using Agent mode, connect to SignalR for real-time updates:

**Connection:**
```javascript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://your-api.com/hubs/agent-events', {
    accessTokenFactory: () => token
  })
  .configureLogging(signalR.LogLevel.Information)
  .build();

await connection.start();

// Listen for agent events
connection.on('ReceiveEvent', (chatId, event) => {
  console.log('Event:', event);
  
  switch(event.type) {
    case 'thinking':
      console.log('Agent is thinking:', event.data);
      break;
    case 'tool_use':
      console.log('Using tool:', event.tool, event.args);
      break;
    case 'tool_result':
      console.log('Tool result:', event.result);
      break;
    case 'response':
      console.log('Final answer:', event.data);
      break;
    case 'complete':
      console.log('Agent finished');
      break;
  }
});
```

---

## 📊 Complete Example Flow

### Scenario 1: User uploads a contract PDF and asks questions (Traditional Flow)

```javascript
// 1. Login
const loginResponse = await fetch('/api/identity/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    userName: 'john.doe',
    password: 'SecurePass123!'
  })
});
const { token, userId } = await loginResponse.json();

// 2. Create chat
const chatResponse = await fetch('/api/chat/create', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    title: 'Contract Review'
  })
});
const { chatId } = await chatResponse.json();

// 3. Upload PDF
const formData = new FormData();
formData.append('file', contractFile); // File from <input type="file">

const uploadResponse = await fetch('/api/files', {
  method: 'POST',
  headers: { 'Authorization': `Bearer ${token}` },
  body: formData
});
const { name: fileName } = await uploadResponse.json();

// 4a. Ask question via streaming chat
const streamResponse = await fetch('/api/chat/stream', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    chatId: chatId,
    message: `Read ${fileName} and tell me the payment terms`,
    modelKey: 'gpt-4o'
  })
});

// Read SSE stream
const reader = streamResponse.body.getReader();
const decoder = new TextDecoder();

while (true) {
  const { value, done } = await reader.read();
  if (done) break;
  
  const chunk = decoder.decode(value);
  const lines = chunk.split('\n');
  
  for (const line of lines) {
    if (line.startsWith('data: ')) {
      const data = JSON.parse(line.substring(6));
      
      if (data.type === 'message_chunk') {
        console.log('Answer chunk:', data.data);
        // Update UI with chunk
      } else if (data.type === 'done') {
        console.log('Stream complete');
      }
    }
  }
}

// OR

// 4b. Ask question via agent mode (more intelligent)
const agentResponse = await fetch(`/api/agent/chat/${chatId}`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    prompt: `Analyze ${fileName} and extract all payment terms, deadlines, and penalties`
  })
});

// Listen to SignalR for updates
connection.on('ReceiveEvent', (receivedChatId, event) => {
  if (receivedChatId === chatId) {
    if (event.type === 'response') {
      console.log('Final answer:', event.data);
      // Update UI with final answer
    }
  }
});
```

### Scenario 2: **One-Step Upload & Ask (RECOMMENDED - Simplest)**

```javascript
// 1. Login
const loginResponse = await fetch('/api/identity/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    userName: 'john.doe',
    password: 'SecurePass123!'
  })
});
const { token } = await loginResponse.json();

// 2. Create chat
const chatResponse = await fetch('/api/chat/create', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    title: 'Document Analysis'
  })
});
const { chatId } = await chatResponse.json();

// 3. Upload file AND ask question in ONE step
const formData = new FormData();
formData.append('file', documentFile); // Can be PDF, DOCX, XLSX, etc.
formData.append('question', 'What are the main terms and deadlines in this contract?');

const response = await fetch(`/api/agent/ask-file/${chatId}`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
  },
  body: formData
});

const result = await response.json();
console.log('Question:', result.question);
console.log('Answer:', result.answer);
console.log('File processed:', result.fileName);

// That's it! The answer is returned immediately and saved to chat history.
```

---

## 🎨 UI/UX Flow Recommendation

### User Experience:

1. **Upload Section**
   ```
   [📄 Upload PDF]  [contract.pdf ✓ Uploaded]
   ```

2. **Chat Interface**
   ```
   User: "What are the payment terms in the contract?"
   
   🤖 AI: [Reading contract.pdf...]
        Based on the contract, the payment terms are:
        
        1. Initial payment: 30% due upon signing
        2. Second payment: 40% due after Phase 1 completion
        3. Final payment: 30% due upon project delivery
        
        Payment deadline: Within 15 business days of invoice
        Late fee: 2% per month on overdue amounts
   ```

3. **Follow-up Questions**
   ```
   User: "What happens if we miss the deadline?"
   
   🤖 AI: According to Section 5.3 of the contract:
        - Grace period: 5 business days
        - After grace period: 2% penalty per month
        - After 60 days: Contract may be terminated
   ```

---

## 📝 Supported File Types

The `/api/files` endpoint supports:

✅ **Documents**: `.pdf`, `.docx`
✅ **Spreadsheets**: `.xlsx`, `.csv`
✅ **Presentations**: `.pptx`
✅ **Images**: `.png`, `.jpg`, `.jpeg`
✅ **Audio**: `.mp3`, `.wav`
✅ **Other**: `.eml`, `.ics`

**File Size Limit**: 50 MB

---

## 🔐 Authentication Headers

All requests (except login/register) require:

```
Authorization: Bearer {token}
```

**Token expiration**: Check `expiresAt` in login response. Implement token refresh or re-login when expired.

---

## ⚡ Quick Reference - Essential Endpoints

| Endpoint | Method | Purpose | Auth Required |
|----------|--------|---------|---------------|
| `/api/identity/login` | POST | Login user | ❌ |
| `/api/identity/register` | POST | Register new user | ❌ |
| `/api/chat/create` | POST | Create chat session | ✅ |
| `/api/files` | POST | Upload PDF/file | ✅ |
| `/api/files` | GET | List uploaded files | ✅ |
| `/api/files/{fileName}` | GET | Download file | ✅ |
| `/api/chat/stream` | POST | Ask question (streaming) | ✅ |
| `/api/agent/chat/{chatId}` | POST | Ask question (agent mode) | ✅ |
| `/api/agent/ask-file/{chatId}` | POST | **Upload file + ask question (one-step)** | ✅ |

---

## 🐛 Error Handling

### Example Error Responses:

**401 Unauthorized:**
```json
{
  "type": "error",
  "message": "Unauthorized"
}
```

**400 Bad Request:**
```json
{
  "type": "error",
  "message": "File type not allowed."
}
```

**500 Internal Server Error:**
```json
{
  "type": "error",
  "message": "An error occurred while processing your request."
}
```

---

## 💡 Best Practices

1. **Use the new one-step endpoint for simplest workflow**:
   - ✅ Use `/api/agent/ask-file/{chatId}` for immediate upload + question
   - ✅ No need to manage filenames or separate upload/ask steps

2. **Always mention the filename** (only if using traditional flow):
   - ✅ "Read research_paper_20250117_103045.pdf and summarize it"
   - ❌ "Summarize the file" (AI won't know which file)

3. **Use specific questions** for better answers:
   - ✅ "What is the project budget mentioned in the document?"
   - ❌ "Tell me about the file"

4. **Supported file types for one-step upload**:
   - PDF, DOCX, XLSX, TXT, CSV files work immediately
   - Images supported (OCR integration coming soon)

5. **Handle file uploads asynchronously** in UI
6. **Show upload progress** for better UX
7. **Cache the token** securely (not in localStorage for production)
8. **Implement token refresh** before expiration
9. **Show typing indicators** while AI processes

---

## 🚀 Ready to Implement!

Your backend fully supports:
- ✅ **One-step file upload + Q&A** (NEW - simplest workflow)
- ✅ Multiple file types (PDF, DOCX, XLSX, TXT, CSV, images)
- ✅ Automatic content extraction
- ✅ PDF upload
- ✅ Text extraction from PDFs
- ✅ Intelligent Q&A about file content
- ✅ Multi-turn conversations
- ✅ Real-time streaming responses
- ✅ Agent-based analysis
- ✅ Automatic chat history saving

**Recommended for most use cases:** Use `/api/agent/ask-file/{chatId}` for the simplest integration—upload any file and ask a question in one request!

Just integrate these endpoints into your frontend and you're good to go! 🎉
