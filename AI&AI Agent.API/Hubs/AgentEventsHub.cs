using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.API.Hubs;

[Authorize]
public class AgentEventsHub : Hub
{
    private readonly ILogger<AgentEventsHub> _logger;

    public AgentEventsHub(ILogger<AgentEventsHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToChat(string chatId)
    {
        try
        {
            // Handle both GUID strings and regular strings
            var groupName = chatId?.ToString() ?? "";
            if (!string.IsNullOrEmpty(groupName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                _logger.LogInformation("User {UserId} subscribed to chat {ChatId}", Context.UserIdentifier, groupName);
            }
            else
            {
                _logger.LogWarning("Empty chatId provided for subscription");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to chat {ChatId}", chatId);
            throw;
        }
    }

    public async Task UnsubscribeFromChat(string chatId)
    {
        try
        {
            var groupName = chatId?.ToString() ?? "";
            if (!string.IsNullOrEmpty(groupName))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                _logger.LogInformation("User {UserId} unsubscribed from chat {ChatId}", Context.UserIdentifier, groupName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from chat {ChatId}", chatId);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.UserIdentifier ?? "Anonymous");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.UserIdentifier ?? "Anonymous");
        
        if (exception != null)
        {
            _logger.LogError(exception, "SignalR client disconnected with error");
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
