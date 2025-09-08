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

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }



        [HttpPost("stream")]
        public async Task StreamChat(ChatRequestDto request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(request.ChatId))
            {
                Response.Headers.Add("Content-Type", "text/event-stream");

                await foreach (var chunk in _chatService.StreamChatAsync(request, userId))
                {
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        await Response.WriteAsync($"data: {chunk}\n\n");
                        await Response.Body.FlushAsync();
                    }
                }
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateChat()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var chat = await _chatService.CreateChatAsync(userId);
            return Ok(chat);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetChats()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var chats = await _chatService.GetChatsByUserIdAsync(userId);
            return Ok(chats);
        }

        [HttpGet("{uid}")]
        public async Task<IActionResult> GetChat([FromRoute] Guid uid)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var chat = await _chatService.GetChatByUIdAsync(uid, userId);
            if (chat == null)
            {
                return NotFound();
            }
            return Ok(chat);
        }
    }
}
