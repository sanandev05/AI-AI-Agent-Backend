using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AI_AI_Agent.Infrastructure.Services.Memory
{
    /// <summary>
    /// Service for managing vector-based semantic memory and RAG functionality
    /// </summary>
    public class VectorMemoryService
    {
        private readonly ISemanticTextMemory _memory;
        private const string DefaultCollection = "agent-knowledge";

        public VectorMemoryService(ISemanticTextMemory memory)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        /// <summary>
        /// Store a document or text in semantic memory
        /// </summary>
        public async Task<string> SaveTextAsync(
            string text,
            string id,
            string? description = null,
            Dictionary<string, string>? additionalMetadata = null,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionName ?? DefaultCollection;
            
            var metadata = additionalMetadata ?? new Dictionary<string, string>();
            if (description != null)
            {
                metadata["description"] = description;
            }

            await _memory.SaveInformationAsync(
                collection: collection,
                text: text,
                id: id,
                description: description,
                additionalMetadata: string.Join("; ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")),
                cancellationToken: cancellationToken
            );

            return id;
        }

        /// <summary>
        /// Search semantic memory for relevant information
        /// </summary>
        public async Task<IEnumerable<MemoryQueryResult>> SearchAsync(
            string query,
            int limit = 5,
            double minRelevanceScore = 0.7,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionName ?? DefaultCollection;

            var results = _memory.SearchAsync(
                collection: collection,
                query: query,
                limit: limit,
                minRelevanceScore: minRelevanceScore,
                cancellationToken: cancellationToken
            );

            var resultList = new List<MemoryQueryResult>();
            await foreach (var result in results)
            {
                resultList.Add(result);
            }

            return resultList;
        }

        /// <summary>
        /// Get a specific memory by ID
        /// </summary>
        public async Task<MemoryQueryResult?> GetAsync(
            string id,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionName ?? DefaultCollection;

            return await _memory.GetAsync(
                collection: collection,
                key: id,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Remove a memory by ID
        /// </summary>
        public async Task RemoveAsync(
            string id,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var collection = collectionName ?? DefaultCollection;

            await _memory.RemoveAsync(
                collection: collection,
                key: id,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Store multiple documents in batch
        /// </summary>
        public async Task<List<string>> SaveBatchAsync(
            Dictionary<string, string> documents,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var ids = new List<string>();

            foreach (var doc in documents)
            {
                var id = await SaveTextAsync(
                    text: doc.Value,
                    id: doc.Key,
                    collectionName: collectionName,
                    cancellationToken: cancellationToken
                );
                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// Get contextually relevant information for a query (RAG pattern)
        /// </summary>
        public async Task<string> GetRelevantContextAsync(
            string query,
            int maxResults = 3,
            double minRelevanceScore = 0.7,
            string? collectionName = null,
            CancellationToken cancellationToken = default)
        {
            var results = await SearchAsync(
                query: query,
                limit: maxResults,
                minRelevanceScore: minRelevanceScore,
                collectionName: collectionName,
                cancellationToken: cancellationToken
            );

            if (!results.Any())
            {
                return string.Empty;
            }

            var contextParts = results
                .OrderByDescending(r => r.Relevance)
                .Select((r, i) => $"[Context {i + 1}] (Relevance: {r.Relevance:F2})\n{r.Metadata.Text}");

            return string.Join("\n\n---\n\n", contextParts);
        }
    }

    /// <summary>
    /// Extension methods for registering vector memory services
    /// </summary>
    public static class VectorMemoryExtensions
    {
        /// <summary>
        /// Add vector memory services to DI container
        /// Note: Requires ISemanticTextMemory to be registered first
        /// </summary>
        public static IServiceCollection AddVectorMemory(this IServiceCollection services)
        {
            services.AddScoped<VectorMemoryService>();
            return services;
        }

        /// <summary>
        /// Add Qdrant vector database (future implementation)
        /// </summary>
        public static IServiceCollection AddQdrantVectorStore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // TODO: Implement Qdrant integration
            // var qdrantEndpoint = configuration["Qdrant:Endpoint"];
            // var qdrantApiKey = configuration["Qdrant:ApiKey"];
            // services.AddSingleton<IMemoryStore>(sp => new QdrantMemoryStore(...));
            
            throw new NotImplementedException("Qdrant integration not yet implemented");
        }

        /// <summary>
        /// Add PostgreSQL pgvector store (future implementation)
        /// </summary>
        public static IServiceCollection AddPgVectorStore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // TODO: Implement pgvector integration
            // var connectionString = configuration.GetConnectionString("VectorDb");
            // services.AddSingleton<IMemoryStore>(sp => new PgVectorMemoryStore(...));
            
            throw new NotImplementedException("pgvector integration not yet implemented");
        }
    }
}
