using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using AI_AI_Agent.Contract.Services;
using Tesseract;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AI_AI_Agent.API.Controllers
{
    [ApiController]
    [Route("api/agent")]
    [Authorize]
    public class SpecialAgentController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly ILogger<SpecialAgentController> _logger;

        public SpecialAgentController(Kernel kernel, ILogger<SpecialAgentController> logger)
        {
            _kernel = kernel;
            _logger = logger;
        }

        /// <summary>
        /// Summarizes a PDF file for the given chat session.
        /// </summary>
        /// <param name="chatId">Chat session ID</param>
        /// <param name="request">PDF summarization request</param>
        /// <returns>Summary result</returns>
        [HttpPost("pdf-summarize/{chatId}")]
        public async Task<IActionResult> PdfSummarize(string chatId, [FromBody] PdfSummarizeRequest request)
        {
            // Get userId from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Save user message to chat history
            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");

            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"Summarize PDF '{request.FileName}' in mode '{request.SummaryMode}'",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            // TODO: Call service to extract PDF text and summarize
            // For now, return mock result
            var summaryText = $"[Mock] Summary of {request.FileName} in mode {request.SummaryMode}...";

            // Save AI message to chat history
            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = summaryText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            var result = new PdfSummaryResult
            {
                FileName = request.FileName,
                SummaryMode = request.SummaryMode,
                Summary = summaryText
            };
            return Ok(result);
        }

        /// <summary>
        /// Universal endpoint: Upload any file (PDF, DOCX, XLSX, PPTX, image) and ask a question.
        /// The backend extracts content, combines it with the question, and returns an AI answer.
        /// </summary>
        /// <param name="chatId">Chat session ID</param>
        /// <param name="file">The file to analyze</param>
        /// <param name="question">The question to ask about the file</param>
        /// <returns>AI answer</returns>
        [HttpPost("ask-file/{chatId}")]
        public async Task<IActionResult> AskFile(string chatId, IFormFile file, [FromForm] string question)
        {
            // Get userId from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (string.IsNullOrWhiteSpace(question))
                return BadRequest("Question is required.");

            // Get repositories and services
            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            if (chatRepo == null)
                return StatusCode(500, "Chat repository not available.");

            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");

            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            // Save the file temporarily to process it
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File uploaded: {FileName}, Size: {Size} bytes", uniqueFileName, file.Length);

            // Extract content based on file type
            string extractedContent;
            try
            {
                extractedContent = await ExtractFileContentAsync(filePath, file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content from file: {FileName}", file.FileName);
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }

            // Save user message to chat history
            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] {question}",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            // Build prompt: extracted content + user question
            var prompt = $@"The user uploaded a file named '{file.FileName}' and asked the following question:

**User Question:** {question}

**File Content:**
{extractedContent}

Please provide a comprehensive answer to the user's question based on the file content above. Answer in the user's native language.";

            // Get AI response
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);

            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            // Save AI message to chat history
            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new
            {
                fileName = file.FileName,
                question = question,
                answer = answerText,
                fileType = Path.GetExtension(file.FileName),
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Translates a document to the specified target language.
        /// </summary>
        [HttpPost("translate/{chatId}")]
        public async Task<IActionResult> DocumentTranslate(string chatId, IFormFile file, [FromForm] string targetLanguage)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (string.IsNullOrWhiteSpace(targetLanguage))
                return BadRequest("Target language is required.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedContent = await ExtractFileContentAsync(filePath, file.FileName);

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] Translate to {targetLanguage}",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"Translate the following document to {targetLanguage}:\n{extractedContent}";
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileName = file.FileName, targetLanguage, translation = answerText });
        }

        /// <summary>
        /// Analyzes a contract or policy document and extracts key clauses and risks.
        /// </summary>
        [HttpPost("analyze-contract/{chatId}")]
        public async Task<IActionResult> ContractPolicyAnalyze(string chatId, IFormFile file)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedContent = await ExtractFileContentAsync(filePath, file.FileName);

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] Analyze contract/policy document",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"Analyze the following contract or policy document. Extract key clauses, obligations, risks, and summarize in plain language:\n{extractedContent}";
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileName = file.FileName, analysis = answerText });
        }

        /// <summary>
        /// Extracts tables from a document and returns them in a structured format.
        /// </summary>
        [HttpPost("extract-table/{chatId}")]
        public async Task<IActionResult> TableExtractor(string chatId, IFormFile file)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedContent = await ExtractFileContentAsync(filePath, file.FileName);

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] Extract tables",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"Extract all tables from the following document and return them in a structured format (CSV or Markdown):\n{extractedContent}";
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileName = file.FileName, tables = answerText });
        }

        /// <summary>
        /// Generates a presentation (PPTX) from a document or summary.
        /// </summary>
        [HttpPost("generate-presentation/{chatId}")]
        public async Task<IActionResult> PresentationGenerator(string chatId, IFormFile file, [FromForm] string topic)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (string.IsNullOrWhiteSpace(topic))
                return BadRequest("Topic is required.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedContent = await ExtractFileContentAsync(filePath, file.FileName);

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] Generate presentation on topic: {topic}",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"Create a presentation (PPTX outline) on the topic '{topic}' using the following document content. List slides with titles and bullet points:\n{extractedContent}";
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileName = file.FileName, topic, presentationOutline = answerText });
        }

        /// <summary>
        /// Q&A Tutor: Ask a question about a document and get a detailed answer.
        /// </summary>
        [HttpPost("qa-tutor/{chatId}")]
        public async Task<IActionResult> QATutor(string chatId, IFormFile file, [FromForm] string question)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (string.IsNullOrWhiteSpace(question))
                return BadRequest("Question is required.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedContent = await ExtractFileContentAsync(filePath, file.FileName);

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[File: {file.FileName}] Q&A Tutor: {question}",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"You are a helpful tutor. Answer the user's question about the following document in detail:\nQuestion: {question}\nDocument Content:\n{extractedContent}";
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileName = file.FileName, question, answer = answerText });
        }

        /// <summary>
        /// Multi-file Compare: Compare the contents of multiple files and summarize differences.
        /// </summary>
        [HttpPost("multi-file-compare/{chatId}")]
        public async Task<IActionResult> MultiFileCompare(string chatId, List<IFormFile> files)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (files == null || files.Count < 2)
                return BadRequest("At least two files are required for comparison.");

            var chatRepo = HttpContext.RequestServices.GetService(typeof(AI_AI_Agent.Domain.Repositories.IChatRepository)) as AI_AI_Agent.Domain.Repositories.IChatRepository;
            var chat = await chatRepo.GetByIdAsync(Guid.Parse(chatId), c => c.Messages);
            if (chat == null || chat.UserId != userId)
                return NotFound("Chat not found or access denied.");
            if (chat.Messages == null)
                chat.Messages = new System.Collections.Generic.List<AI_AI_Agent.Domain.Entities.Message>();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileContents = new List<string>();
            var fileNames = new List<string>();

            foreach (var file in files)
            {
                var uniqueFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                var content = await ExtractFileContentAsync(filePath, file.FileName);
                fileContents.Add(content);
                fileNames.Add(file.FileName);
            }

            var userMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = $"[Files: {string.Join(", ", fileNames)}] Multi-file compare",
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.User,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(userMessage);
            await chatRepo.UpdateAsync(chat);

            var prompt = $"Compare the following files and summarize key similarities and differences:\n" + string.Join("\n---\n", fileContents);
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            var chatService = _kernel.Services.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var aiResponse = await chatService.GetChatMessageContentsAsync(chatHistory, kernel: _kernel);
            var answerText = aiResponse.LastOrDefault()?.Content ?? "[No response generated]";

            var aiMessage = new AI_AI_Agent.Domain.Entities.Message
            {
                Content = answerText,
                Roles = AI_AI_Agent.Domain.Entities.Enums.MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                TimeStamp = DateTime.UtcNow
            };
            chat.Messages.Add(aiMessage);
            await chatRepo.UpdateAsync(chat);

            return Ok(new { fileNames, comparison = answerText });
        }

        // Private helper methods for file content extraction

        private async Task<string> ExtractFileContentAsync(string filePath, string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

            switch (extension)
            {
                case ".pdf":
                    return await ExtractPdfContentAsync(filePath);
                case ".docx":
                    return await ExtractDocxContentAsync(filePath);
                case ".xlsx":
                    return await ExtractXlsxContentAsync(filePath);
                case ".txt":
                    return await System.IO.File.ReadAllTextAsync(filePath);
                case ".csv":
                    return await System.IO.File.ReadAllTextAsync(filePath);
                case ".jpg":
                case ".jpeg":
                case ".png":
                    // Image analysis is not supported unless a multimodal LLM is configured
                    return $"[Image file uploaded: {Path.GetFileName(originalFileName)}. Image analysis is not supported. Please describe the image or upgrade to a multimodal LLM for direct analysis.]";
                default:
                    throw new NotSupportedException($"File type '{extension}' is not supported for content extraction.");
            }
        }

        private async Task<string> ExtractPdfContentAsync(string filePath)
        {
            try
            {
                using var pdfReader = new iText.Kernel.Pdf.PdfReader(filePath);
                using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader);
                var text = new System.Text.StringBuilder();

                for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
                {
                    var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(page));
                    text.AppendLine(pageText);
                }

                var result = text.ToString();
                return string.IsNullOrWhiteSpace(result) ? "[PDF contains no extractable text]" : result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting PDF content");
                return $"[Error extracting PDF: {ex.Message}]";
            }
        }

        private async Task<string> ExtractDocxContentAsync(string filePath)
        {
            try
            {
                using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null)
                    return "[DOCX contains no readable content]";

                var text = body.InnerText;
                return string.IsNullOrWhiteSpace(text) ? "[DOCX contains no text]" : text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting DOCX content");
                return $"[Error extracting DOCX: {ex.Message}]";
            }
        }

        private async Task<string> ExtractXlsxContentAsync(string filePath)
        {
            try
            {
                using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(filePath, false);
                var workbookPart = spreadsheet.WorkbookPart;
                var worksheetPart = workbookPart?.WorksheetParts.FirstOrDefault();
                if (worksheetPart == null)
                    return "[XLSX contains no worksheets]";

                var sheetData = worksheetPart.Worksheet.Elements<DocumentFormat.OpenXml.Spreadsheet.SheetData>().FirstOrDefault();
                if (sheetData == null)
                    return "[XLSX worksheet contains no data]";

                var text = new System.Text.StringBuilder();
                foreach (var row in sheetData.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>())
                {
                    foreach (var cell in row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                    {
                        var cellValue = cell.CellValue?.Text ?? cell.InnerText;
                        text.Append(cellValue + "\t");
                    }
                    text.AppendLine();
                }

                var result = text.ToString();
                return string.IsNullOrWhiteSpace(result) ? "[XLSX contains no readable data]" : result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting XLSX content");
                return $"[Error extracting XLSX: {ex.Message}]";
            }
        }

        private async Task<string> ExtractImageContentAsync(string filePath)
        {
            // Use Tesseract OCR to extract text from images
            try
            {
                using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
                using var img = Pix.LoadFromFile(filePath);
                using var page = engine.Process(img);
                var text = page.GetText();
                return string.IsNullOrWhiteSpace(text)
                    ? "[Image contains no readable text]"
                    : text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image via OCR");
                return $"[Error extracting image text: {ex.Message}]";
            }
        }
    }

    public class PdfSummarizeRequest
    {
        public string? FileName { get; set; }
        public string? SummaryMode { get; set; } // "tldr", "detailed", "section"
    }

    public class PdfSummaryResult
    {
        public string? FileName { get; set; }
        public string? SummaryMode { get; set; }
        public string? Summary { get; set; }
    }
}
