# Quick Tool Reference Guide

## 📋 Fast Access Tool List

### 📄 Document Creation
```
"Create a DOCX document about [topic]"          → DocxCreate
"Generate a PDF report on [topic]"              → PdfCreate  
"Create a PowerPoint presentation about [topic]" → PptxCreate
"Write text to a file called [name]"            → FileWriter
```

### 📖 Document Reading
```
"Read the DOCX file [name]"                     → DocxRead
"Extract text from PDF [name]"                  → PdfReader
"Analyze Excel file [name]"                     → ExcelRead
"Analyze CSV data in [name]"                    → CsvAnalyze
```

### 📊 Data & Visualization
```
"Analyze this data: [data]"                     → DataAnalyze
"Create a [type] chart showing [data]"          → ChartCreate
```

### 🌐 Web Operations
```
"Search the web for [query]"                    → WebSearch
"Browse [URL] and extract content"              → WebBrowser
```

### 🧮 Utilities
```
"Calculate [expression]"                        → Calculator
"Extract [fields] from this text: [text]"       → Extractor
"Translate '[text]' to [language]"              → Translate
```

### 📧 Productivity
```
"Draft an email to [recipient] about [topic]"   → EmailDraft
"Send email to [recipient]: [subject]"          → EmailSend
"Create calendar event: [details]"              → CalendarCreate
"List my calendar events"                       → CalendarList
"Create a task: [description]"                  → Tasks
```

### 🔍 Advanced Analysis
```
"Summarize this document: [text]"               → ResearchSummarize
"Compare [product1] and [product2]"             → ProductCompare
"Analyze revenue data: [data]"                  → FinanceRevenue
```

---

## 🚀 Test Commands Ready to Copy

### DOCX Test (Original Issue)
```
Create a DOCX document about quantum computing with sections on qubits, superposition, and entanglement
```

### PDF Test
```
Generate a PDF report on climate change impact with sections on temperature rise, sea level, and carbon emissions
```

### PowerPoint Test
```
Create a PowerPoint presentation about our Q4 results with slides for revenue, expenses, profit, and outlook
```

### Web Search Test
```
Search the web for the latest breakthroughs in artificial intelligence from 2024
```

### Data Analysis Test
```
Analyze this sales data and provide insights: [{"month": "Jan", "sales": 10000}, {"month": "Feb", "sales": 12000}]
```

### Chart Test
```
Create a bar chart showing monthly sales: January 50000, February 62000, March 58000, April 71000
```

### Calculator Test
```
Calculate the compound interest on $10,000 at 5% annual rate for 10 years
```

### Translation Test
```
Translate "Hello, how are you today?" to Spanish, French, and German
```

---

## ✅ Verification Checklist

Use this for quick testing:

- [ ] DOCX file creation works
- [ ] PDF file creation works  
- [ ] PPTX file creation works
- [ ] Web search returns results
- [ ] Calculator performs math
- [ ] Files appear in file list
- [ ] Download links work
- [ ] SignalR events emit
- [ ] Agent provides confirmation after tool execution

---

## 🔧 Quick Troubleshooting

### Agent Still Not Using Tools?

1. **Check Model**: Use gpt-4o or gpt-4 (not gpt-3.5-turbo)
2. **Check Logs**: Look for `tool:start` events in browser console
3. **Verify Build**: Ensure solution built successfully
4. **Check Request**: Use clear, direct language like "Create a DOCX about X"

### Tool Execution Fails?

1. **Check Parameters**: Verify prompt has required info (filename, content, etc.)
2. **Check Workspace**: Ensure `workspace/` folder exists and has permissions
3. **Check Logs**: Look in browser console and API logs for error details

---

## 📊 Quick Stats

- **Total Tools**: 24
- **Registered**: 24 (100%)
- **Categories**: 7
- **Build Status**: ✅ SUCCESS
- **Documentation**: 3 comprehensive files

---

## 🎯 Most Common Use Cases

### 1. Create Document with Content
```
Create a DOCX document called "project_report.docx" about our Q4 project achievements including milestones, challenges, and next steps
```

### 2. Research and Summarize
```
Search the web for information about renewable energy trends in 2024, then summarize the findings in a PDF report
```

### 3. Data Analysis and Visualization  
```
Analyze this sales data [data here] and create a bar chart showing the results
```

### 4. Multi-Tool Workflow
```
Search the web for AI market trends, summarize the findings, and create a PowerPoint presentation with the key insights
```

---

## 📚 Full Documentation

For complete details, see:
- `TOOL_REGISTRY.md` - Complete tool inventory
- `TOOL_EXECUTION_FIX.md` - Technical fix details
- `TOOL_WORKABILITY_SUMMARY.md` - Verification summary

---

**Last Updated**: October 14, 2025  
**Status**: ✅ All Tools Workable
