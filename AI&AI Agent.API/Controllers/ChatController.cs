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
        private IChatService _chatService;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly Kernel _kernel;

        public ChatController(IChatService chatService, IChatCompletionService chatCompletionService, Kernel kernel)
        {
            _chatService = chatService;
            _chatCompletionService = chatCompletionService;
            _kernel = kernel;
        }

        //[HttpPost]
        //public Task<string> SendMessage([FromBody]ChatRequestDto request)
        //{
        //   return _chatService.GetNonStreamingChatMessage(request.Message,request);
        //}

        [HttpGet("stream")]
        public async Task Stream(ChatRequestDto request)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(request.ChatId))
            {
                Response.Headers.Add("Content-Type", "text/event-stream");

                var history = new ChatHistory();
                history.AddUserMessage(request.Message);
                var response = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                chatHistory: history,
                kernel: _kernel
                                );

                await foreach (var chunk in response)
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        await Response.WriteAsync($"data: {chunk.Content}\n\n");
                        await Response.Body.FlushAsync();
                    }
                }
            }
            else
            {
               await _chatService.CreateChatAsync(userId);
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
            return Ok(userId);
        }

        [HttpGet("/{uid}")]
        public Task GetChat([FromRoute] Guid uid)
        {
            return null;
        }
    }
}
