using AI_AI_Agent.Domain.Knowledge;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Knowledge
{
    /// <summary>
    /// Knowledge retrieval service with semantic search and fact verification
    /// </summary>
    public class KnowledgeRetrievalService
    {
        private readonly ILogger<KnowledgeRetrievalService> _logger;
        private readonly ConcurrentDictionary<string, KnowledgeEntry> _knowledgeBase = new();
        private readonly ConcurrentDictionary<string, DocumentIndex> _documentIndices = new();
        private readonly ConcurrentDictionary<string, KnowledgeGraph> _graphs = new();
        private readonly ConcurrentDictionary<string, FactVerification> _verifications = new();

        public KnowledgeRetrievalService(ILogger<KnowledgeRetrievalService> logger)
        {
            _logger = logger;
        }

        #region Knowledge Base Management

        public KnowledgeEntry AddKnowledge(
            string title,
            string content,
            List<string> sources,
            List<string>? tags = null)
        {
            var entry = new KnowledgeEntry
            {
                Title = title,
                Content = content,
                Sources = sources,
                Tags = tags ?? new List<string>(),
                IsVerified = false
            };

            // Extract citations from sources
            entry.Citations = sources.Select(s => new Citation
            {
                Source = s,
                Url = ExtractUrl(s),
                ConfidenceScore = 0.8,
                IsVerified = false
            }).ToList();

            _knowledgeBase[entry.Id] = entry;

            _logger.LogInformation("Added knowledge entry {EntryId}: {Title}", entry.Id, title);
            return entry;
        }

        public KnowledgeEntry? GetKnowledge(string entryId)
        {
            return _knowledgeBase.TryGetValue(entryId, out var entry) ? entry : null;
        }

        public List<KnowledgeEntry> GetAllKnowledge()
        {
            return _knowledgeBase.Values.OrderByDescending(e => e.CreatedAt).ToList();
        }

        public void UpdateKnowledge(string entryId, string content)
        {
            if (_knowledgeBase.TryGetValue(entryId, out var entry))
            {
                entry.Content = content;
                entry.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated knowledge entry {EntryId}", entryId);
            }
        }

        #endregion

        #region Semantic Search

        public async Task<List<SearchResult>> SemanticSearchAsync(SemanticSearchQuery query)
        {
            _logger.LogInformation("Performing {Mode} search for: {Query}", query.Mode, query.Query);

            var results = new List<SearchResult>();

            // Search in knowledge base
            foreach (var entry in _knowledgeBase.Values)
            {
                var relevance = CalculateRelevance(query.Query, entry.Content, query.Mode);

                if (relevance >= query.MinRelevanceScore)
                {
                    // Filter by tags if specified
                    if (query.FilterTags.Any() && !query.FilterTags.Any(t => entry.Tags.Contains(t)))
                    {
                        continue;
                    }

                    results.Add(new SearchResult
                    {
                        Id = entry.Id,
                        Content = entry.Content,
                        RelevanceScore = relevance,
                        Citations = entry.Citations,
                        Snippet = GenerateSnippet(entry.Content, query.Query),
                        Metadata = entry.Metadata
                    });
                }
            }

            // Sort by relevance and limit results
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(query.MaxResults)
                .ToList();

            _logger.LogInformation("Found {Count} results for query", results.Count);
            return results;
        }

        private double CalculateRelevance(string query, string content, SearchMode mode)
        {
            // Simplified relevance calculation
            var queryLower = query.ToLower();
            var contentLower = content.ToLower();

            double relevance = 0.0;

            switch (mode)
            {
                case SearchMode.Keyword:
                    relevance = CalculateKeywordRelevance(queryLower, contentLower);
                    break;
                case SearchMode.Semantic:
                    relevance = CalculateSemanticRelevance(queryLower, contentLower);
                    break;
                case SearchMode.Hybrid:
                    relevance = (CalculateKeywordRelevance(queryLower, contentLower) +
                                CalculateSemanticRelevance(queryLower, contentLower)) / 2.0;
                    break;
            }

            return relevance;
        }

        private double CalculateKeywordRelevance(string query, string content)
        {
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = queryWords.Count(word => content.Contains(word));
            return (double)matches / queryWords.Length;
        }

        private double CalculateSemanticRelevance(string query, string content)
        {
            // Simplified semantic similarity - in production would use embeddings
            var queryWords = new HashSet<string>(query.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var contentWords = new HashSet<string>(content.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var intersection = queryWords.Intersect(contentWords).Count();
            var union = queryWords.Union(contentWords).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }

        private string GenerateSnippet(string content, string query, int maxLength = 200)
        {
            var queryLower = query.ToLower();
            var contentLower = content.ToLower();

            var index = contentLower.IndexOf(queryLower);
            if (index >= 0)
            {
                var start = Math.Max(0, index - 50);
                var length = Math.Min(maxLength, content.Length - start);
                var snippet = content.Substring(start, length);

                if (start > 0) snippet = "..." + snippet;
                if (start + length < content.Length) snippet += "...";

                return snippet;
            }

            return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;
        }

        #endregion

        #region Citation Tracking

        public void AddCitation(string knowledgeId, Citation citation)
        {
            if (_knowledgeBase.TryGetValue(knowledgeId, out var entry))
            {
                entry.Citations.Add(citation);
                _logger.LogInformation("Added citation to knowledge entry {KnowledgeId}", knowledgeId);
            }
        }

        public List<Citation> GetCitations(string knowledgeId)
        {
            if (_knowledgeBase.TryGetValue(knowledgeId, out var entry))
            {
                return entry.Citations;
            }
            return new List<Citation>();
        }

        public void VerifyCitation(string knowledgeId, string citationId)
        {
            if (_knowledgeBase.TryGetValue(knowledgeId, out var entry))
            {
                var citation = entry.Citations.FirstOrDefault(c => c.Id == citationId);
                if (citation != null)
                {
                    citation.IsVerified = true;
                    _logger.LogInformation("Verified citation {CitationId}", citationId);
                }
            }
        }

        private string ExtractUrl(string source)
        {
            // Simple URL extraction
            var urlPattern = @"https?://[^\s]+";
            var match = System.Text.RegularExpressions.Regex.Match(source, urlPattern);
            return match.Success ? match.Value : string.Empty;
        }

        #endregion

        #region Knowledge Graph

        public KnowledgeGraph BuildKnowledgeGraph(List<string> knowledgeIds)
        {
            var graph = new KnowledgeGraph();

            // Create nodes for each knowledge entry
            foreach (var id in knowledgeIds)
            {
                if (_knowledgeBase.TryGetValue(id, out var entry))
                {
                    var node = new KnowledgeNode
                    {
                        Id = entry.Id,
                        Label = entry.Title,
                        Category = NodeCategory.Document,
                        Importance = entry.RelevanceScore
                    };
                    graph.Nodes.Add(node);
                }
            }

            // Create edges based on shared tags and citations
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                for (int j = i + 1; j < graph.Nodes.Count; j++)
                {
                    var node1 = graph.Nodes[i];
                    var node2 = graph.Nodes[j];

                    var entry1 = _knowledgeBase[node1.Id];
                    var entry2 = _knowledgeBase[node2.Id];

                    // Check for shared tags
                    var sharedTags = entry1.Tags.Intersect(entry2.Tags).Count();
                    if (sharedTags > 0)
                    {
                        graph.Edges.Add(new KnowledgeEdge
                        {
                            FromNodeId = node1.Id,
                            ToNodeId = node2.Id,
                            Type = RelationshipType.RelatedTo,
                            Strength = (double)sharedTags / Math.Max(entry1.Tags.Count, entry2.Tags.Count)
                        });
                    }

                    // Check for shared sources
                    var sharedSources = entry1.Sources.Intersect(entry2.Sources).Count();
                    if (sharedSources > 0)
                    {
                        graph.Edges.Add(new KnowledgeEdge
                        {
                            FromNodeId = node1.Id,
                            ToNodeId = node2.Id,
                            Type = RelationshipType.SimilarTo,
                            Strength = (double)sharedSources / Math.Max(entry1.Sources.Count, entry2.Sources.Count)
                        });
                    }
                }
            }

            var graphId = Guid.NewGuid().ToString();
            _graphs[graphId] = graph;

            _logger.LogInformation("Built knowledge graph with {NodeCount} nodes and {EdgeCount} edges",
                graph.Nodes.Count, graph.Edges.Count);

            return graph;
        }

        public KnowledgeGraph? GetKnowledgeGraph(string graphId)
        {
            return _graphs.TryGetValue(graphId, out var graph) ? graph : null;
        }

        #endregion

        #region Fact Verification

        public async Task<FactVerification> VerifyFactAsync(string claim)
        {
            _logger.LogInformation("Verifying fact: {Claim}", claim);

            var verification = new FactVerification
            {
                Claim = claim,
                Status = VerificationStatus.Insufficient
            };

            // Search for supporting and contradicting evidence
            var searchQuery = new SemanticSearchQuery
            {
                Query = claim,
                MaxResults = 10,
                MinRelevanceScore = 0.6,
                Mode = SearchMode.Semantic
            };

            var results = await SemanticSearchAsync(searchQuery);

            foreach (var result in results)
            {
                var evidence = new Evidence
                {
                    Source = result.Id,
                    Content = result.Snippet,
                    Reliability = result.RelevanceScore
                };

                // Simplified logic - in production would use NLP to determine support/contradiction
                if (IsSupporting(claim, result.Content))
                {
                    verification.SupportingEvidence.Add(evidence);
                }
                else if (IsContradicting(claim, result.Content))
                {
                    verification.ContradictingEvidence.Add(evidence);
                }
            }

            // Determine verification status
            var supportCount = verification.SupportingEvidence.Count;
            var contradictCount = verification.ContradictingEvidence.Count;

            if (supportCount > 0 && contradictCount == 0)
            {
                verification.Status = VerificationStatus.Verified;
                verification.ConfidenceScore = Math.Min(1.0, supportCount * 0.3);
                verification.Conclusion = "Claim is supported by available evidence";
            }
            else if (supportCount == 0 && contradictCount > 0)
            {
                verification.Status = VerificationStatus.Contradicted;
                verification.ConfidenceScore = Math.Min(1.0, contradictCount * 0.3);
                verification.Conclusion = "Claim is contradicted by available evidence";
            }
            else if (supportCount > 0 && contradictCount > 0)
            {
                verification.Status = VerificationStatus.PartiallyVerified;
                verification.ConfidenceScore = 0.5;
                verification.Conclusion = "Mixed evidence found for this claim";
            }
            else
            {
                verification.Status = VerificationStatus.Insufficient;
                verification.ConfidenceScore = 0.0;
                verification.Conclusion = "Insufficient evidence to verify claim";
            }

            _verifications[verification.Id] = verification;

            _logger.LogInformation("Fact verification completed with status {Status}", verification.Status);
            return verification;
        }

        private bool IsSupporting(string claim, string content)
        {
            // Simplified support detection - in production would use NLP
            var claimLower = claim.ToLower();
            var contentLower = content.ToLower();

            return contentLower.Contains(claimLower) ||
                   CalculateSemanticRelevance(claimLower, contentLower) > 0.7;
        }

        private bool IsContradicting(string claim, string content)
        {
            // Simplified contradiction detection - in production would use NLP
            var contradictionWords = new[] { "not", "no", "false", "incorrect", "wrong" };
            var contentLower = content.ToLower();

            return contradictionWords.Any(w => contentLower.Contains(w + " " + claim.ToLower()));
        }

        public FactVerification? GetVerification(string verificationId)
        {
            return _verifications.TryGetValue(verificationId, out var verification) ? verification : null;
        }

        #endregion

        #region Document Indexing

        public async Task<DocumentIndex> IndexDocumentAsync(string documentPath)
        {
            _logger.LogInformation("Indexing document: {DocumentPath}", documentPath);

            var index = new DocumentIndex
            {
                DocumentPath = documentPath,
                Title = Path.GetFileNameWithoutExtension(documentPath),
                Status = IndexStatus.Indexing
            };

            try
            {
                var content = await File.ReadAllTextAsync(documentPath);

                // Chunk the document
                index.Chunks = ChunkDocument(content);

                index.Status = IndexStatus.Completed;
                _documentIndices[index.Id] = index;

                _logger.LogInformation("Indexed document {DocumentPath} with {ChunkCount} chunks",
                    documentPath, index.Chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index document {DocumentPath}", documentPath);
                index.Status = IndexStatus.Failed;
            }

            return index;
        }

        private List<DocumentChunk> ChunkDocument(string content, int chunkSize = 500)
        {
            var chunks = new List<DocumentChunk>();
            var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var currentChunk = new StringBuilder();
            var position = 0;

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length > chunkSize && currentChunk.Length > 0)
                {
                    // Create chunk
                    var chunkContent = currentChunk.ToString().Trim();
                    chunks.Add(new DocumentChunk
                    {
                        Content = chunkContent,
                        StartPosition = position,
                        EndPosition = position + chunkContent.Length
                    });

                    position += chunkContent.Length;
                    currentChunk.Clear();
                }

                currentChunk.Append(sentence.Trim()).Append(". ");
            }

            // Add final chunk
            if (currentChunk.Length > 0)
            {
                var chunkContent = currentChunk.ToString().Trim();
                chunks.Add(new DocumentChunk
                {
                    Content = chunkContent,
                    StartPosition = position,
                    EndPosition = position + chunkContent.Length
                });
            }

            return chunks;
        }

        public DocumentIndex? GetDocumentIndex(string indexId)
        {
            return _documentIndices.TryGetValue(indexId, out var index) ? index : null;
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalKnowledgeEntries"] = _knowledgeBase.Count,
                ["verifiedEntries"] = _knowledgeBase.Values.Count(e => e.IsVerified),
                ["totalCitations"] = _knowledgeBase.Values.Sum(e => e.Citations.Count),
                ["verifiedCitations"] = _knowledgeBase.Values.SelectMany(e => e.Citations).Count(c => c.IsVerified),
                ["totalDocumentIndices"] = _documentIndices.Count,
                ["totalChunks"] = _documentIndices.Values.Sum(d => d.Chunks.Count),
                ["knowledgeGraphs"] = _graphs.Count,
                ["factVerifications"] = _verifications.Count,
                ["verifiedFacts"] = _verifications.Values.Count(v => v.Status == VerificationStatus.Verified)
            };
        }

        #endregion
    }
}
