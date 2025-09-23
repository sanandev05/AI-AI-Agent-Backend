using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Contract.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AI_AI_Agent.API.Controllers
{
   [Authorize]
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ChatController(IChatService chatService, IWebHostEnvironment webHostEnvironment)
        {
            _chatService = chatService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"{Request.Scheme}://{Request.Host}/images/{fileName}";
            return Ok(new { imageUrl });
        }

        // Helper: unified SSE streaming
        private async Task StreamSseAsync(IAsyncEnumerable<string> source, CancellationToken ct)
        {
            if (!Response.HasStarted)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache, no-transform");
            }

            await foreach (var chunk in source.WithCancellation(ct))
            {
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    await Response.WriteAsync($"data: {chunk}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }

            // Signal completion
            await Response.WriteAsync("data: {\"type\":\"done\"}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        private async Task WriteSseErrorAsync(string message, int statusCode = 400, CancellationToken ct = default)
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = statusCode;
                Response.Headers.Append("Content-Type", "text/event-stream");
            }
            // Always write the error message to the stream
            await Response.WriteAsync($"data: {{\"type\":\"error\",\"message\":\"{message}\"}}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        [HttpPost("stream")]
        public async Task StreamChat(
            ChatRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await WriteSseErrorAsync("Unauthorized", 401, cancellationToken);
                return;
            }

            if (string.IsNullOrEmpty(request?.ChatId))
            {
                await WriteSseErrorAsync("ChatId is required.", 400, cancellationToken);
                return;
            }

            try
            {
                var stream = _chatService.StreamChatAsync(request, userId);
                await StreamSseAsync(stream, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or request aborted; silently end.
            }
            catch (Exception ex)
            {
                await WriteSseErrorAsync("Internal server error: " + ex.Message, 500, cancellationToken);
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateChat()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var chat = await _chatService.CreateChatAsync(userId);
            return Ok(chat);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetChats()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var chats = await _chatService.GetChatsByUserIdAsync(userId);
            return Ok(chats);
        }

        [HttpGet("{uid}")]
        public async Task<IActionResult> GetChat([FromRoute] Guid uid)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var chat = await _chatService.GetChatByUIdAsync(uid, userId);
            if (chat == null)
            {
                return NotFound();
            }
            return Ok(chat);
        }

        [HttpDelete("{uid}")]
        public async Task<IActionResult> DeleteChat([FromRoute] Guid uid)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var success = await _chatService.DeleteChatAsync(uid, userId);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPut("{uid}/rename")]
        public async Task<IActionResult> RenameChat([FromRoute] Guid uid, [FromBody] RenameChatDto renameDto)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var success = await _chatService.RenameChatAsync(uid, renameDto.NewTitle, userId);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("web-search")]
        public async Task StreamWebSearch(WebSearchRequestDto request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await WriteSseErrorAsync("Unauthorized", 401, cancellationToken);
                return;
            }
            if (string.IsNullOrEmpty(request.ChatId))
            {
                await WriteSseErrorAsync("ChatId is required.", 400, cancellationToken);
                return;
            }

            try
            {
                await StreamSseAsync(_chatService.StreamWebSearchAsync(request, userId, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Silent
            }
            catch (Exception ex)
            {
                await WriteSseErrorAsync("Internal server error: " + ex.Message, 500, cancellationToken);
            }
        }
    }
}
