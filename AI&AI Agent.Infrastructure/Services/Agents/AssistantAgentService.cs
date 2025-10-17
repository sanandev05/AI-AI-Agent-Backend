using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using AI_AI_Agent.Domain.Agents;
using DomainAgentMetadata = AI_AI_Agent.Domain.Agents.AgentMetadata;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only
#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace AI_AI_Agent.Infrastructure.Services.Agents
{
    /// <summary>
    /// Service for managing OpenAI Assistant Agents using Semantic Kernel
    /// </summary>
    public class AssistantAgentService
    {
        private readonly Kernel _kernel;
        private readonly IAgentRegistry _agentRegistry;
        private readonly AssistantClient _assistantClient;
        private readonly Dictionary<string, OpenAIAssistantAgent> _activeAssistants = new();

        public AssistantAgentService(
            Kernel kernel, 
            IAgentRegistry agentRegistry,
            AssistantClient assistantClient)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
            _assistantClient = assistantClient ?? throw new ArgumentNullException(nameof(assistantClient));
        }

        /// <summary>
        /// Create a new OpenAI Assistant Agent
        /// </summary>
        public async Task<OpenAIAssistantAgent> CreateAssistantAsync(
            DomainAgentMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            var modelId = metadata.Configuration.GetValueOrDefault("ModelId", "gpt-4o");
            var temperature = metadata.Configuration.ContainsKey("Temperature") ?
                             float.Parse(metadata.Configuration["Temperature"]) : 0.7f;

            // Create assistant definition using OpenAI AssistantClient
            var options = new AssistantCreationOptions
            {
                Name = metadata.Name,
                Instructions = metadata.Instructions,
                Description = metadata.Description,
                Temperature = temperature
            };
            
            // Add metadata
            foreach (var kvp in metadata.Configuration)
            {
                options.Metadata.Add(kvp.Key, kvp.Value);
            }

            var assistant = await _assistantClient.CreateAssistantAsync(
                modelId,
                options,
                cancellationToken: cancellationToken
            );

            // Create SK agent wrapping the assistant
            var agent = new OpenAIAssistantAgent(assistant, _assistantClient)
            {
                Kernel = _kernel
            };

            // Store in active assistants
            _activeAssistants[metadata.Id] = agent;

            // Register in agent registry
            await _agentRegistry.RegisterAgentAsync(metadata, cancellationToken);

            return agent;
        }

        /// <summary>
        /// Get an existing assistant by agent ID
        /// </summary>
        public async Task<OpenAIAssistantAgent?> GetAssistantAsync(
            string agentId,
            CancellationToken cancellationToken = default)
        {
            if (_activeAssistants.TryGetValue(agentId, out var assistant))
            {
                return assistant;
            }

            // Try to retrieve from registry and recreate
            var metadata = await _agentRegistry.GetAgentAsync(agentId, cancellationToken);
            if (metadata != null)
            {
                return await CreateAssistantAsync(metadata, cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Delete an assistant
        /// </summary>
        public async Task DeleteAssistantAsync(
            string agentId,
            CancellationToken cancellationToken = default)
        {
            if (_activeAssistants.TryGetValue(agentId, out var assistant))
            {
                // Delete from OpenAI
                await _assistantClient.DeleteAssistantAsync(assistant.Id, cancellationToken);

                // Remove from active assistants
                _activeAssistants.Remove(agentId);

                // Unregister from registry
                await _agentRegistry.UnregisterAgentAsync(agentId, cancellationToken);
            }
        }

        /// <summary>
        /// Create a new chat thread for an assistant
        /// </summary>
        public async Task<OpenAIAssistantAgentThread> CreateThreadAsync(
            string agentId,
            CancellationToken cancellationToken = default)
        {
            var assistant = await GetAssistantAsync(agentId, cancellationToken);
            if (assistant == null)
            {
                throw new InvalidOperationException($"Assistant with ID '{agentId}' not found");
            }

            // Create a new thread
            var thread = new OpenAIAssistantAgentThread(_assistantClient);
            return thread;
        }

        /// <summary>
        /// Send a message to an assistant and get response
        /// </summary>
        public async Task<string> SendMessageAsync(
            string agentId,
            OpenAIAssistantAgentThread thread,
            string message,
            CancellationToken cancellationToken = default)
        {
            var assistant = await GetAssistantAsync(agentId, cancellationToken);
            if (assistant == null)
            {
                throw new InvalidOperationException($"Assistant with ID '{agentId}' not found");
            }

            // Get assistant response
            var responses = new List<string>();
            await foreach (var response in assistant.InvokeAsync(
                new[] { new ChatMessageContent(AuthorRole.User, message) },
                thread,
                cancellationToken: cancellationToken))
            {
                if (response.Message.Content != null)
                {
                    responses.Add(response.Message.Content);
                }
            }

            return string.Join("\n", responses);
        }

        /// <summary>
        /// Invoke assistant with streaming response
        /// </summary>
        public async IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(
            string agentId,
            OpenAIAssistantAgentThread thread,
            string message,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var assistant = await GetAssistantAsync(agentId, cancellationToken);
            if (assistant == null)
            {
                throw new InvalidOperationException($"Assistant with ID '{agentId}' not found");
            }

            // Stream assistant response
            await foreach (var response in assistant.InvokeStreamingAsync(
                new[] { new ChatMessageContent(AuthorRole.User, message) },
                thread,
                cancellationToken: cancellationToken))
            {
                yield return response.Message;
            }
        }

        /// <summary>
        /// Get chat history from a thread
        /// </summary>
        public async Task<IReadOnlyList<ChatMessageContent>> GetChatHistoryAsync(
            OpenAIAssistantAgentThread thread,
            CancellationToken cancellationToken = default)
        {
            var history = new List<ChatMessageContent>();
            await foreach (var message in thread.GetMessagesAsync(cancellationToken: cancellationToken))
            {
                history.Add(message);
            }
            return history;
        }

        /// <summary>
        /// Clear all active assistants (cleanup)
        /// </summary>
        public async Task ClearAllAssistantsAsync(CancellationToken cancellationToken = default)
        {
            foreach (var kvp in _activeAssistants.ToList())
            {
                await DeleteAssistantAsync(kvp.Key, cancellationToken);
            }
        }
    }
}
