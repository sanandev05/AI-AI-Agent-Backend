namespace AI_AI_Agent.Domain.Knowledge
{
    /// <summary>
    /// Knowledge base entry with semantic search capabilities
    /// </summary>
    public class KnowledgeEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
        public List<Citation> Citations { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public double RelevanceScore { get; set; }
        public bool IsVerified { get; set; }
    }

    /// <summary>
    /// Citation information for knowledge sources
    /// </summary>
    public class Citation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime? PublishedDate { get; set; }
        public string Excerpt { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public bool IsVerified { get; set; }
    }

    /// <summary>
    /// Semantic search query and results
    /// </summary>
    public class SemanticSearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 10;
        public double MinRelevanceScore { get; set; } = 0.7;
        public List<string> FilterTags { get; set; } = new();
        public SearchMode Mode { get; set; }
    }

    public enum SearchMode
    {
        Semantic,
        Keyword,
        Hybrid
    }

    public class SearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double RelevanceScore { get; set; }
        public List<Citation> Citations { get; set; } = new();
        public string Snippet { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Knowledge graph for relationship visualization
    /// </summary>
    public class KnowledgeGraph
    {
        public List<KnowledgeNode> Nodes { get; set; } = new();
        public List<KnowledgeEdge> Edges { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class KnowledgeNode
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public NodeCategory Category { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public double Importance { get; set; }
    }

    public enum NodeCategory
    {
        Concept,
        Entity,
        Topic,
        Document,
        Person,
        Organization,
        Location
    }

    public class KnowledgeEdge
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public RelationshipType Type { get; set; }
        public double Strength { get; set; }
        public string? Label { get; set; }
    }

    public enum RelationshipType
    {
        RelatedTo,
        PartOf,
        CausedBy,
        DependsOn,
        SimilarTo,
        OppositeOf,
        DefinedBy
    }

    /// <summary>
    /// Fact verification result
    /// </summary>
    public class FactVerification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Claim { get; set; } = string.Empty;
        public VerificationStatus Status { get; set; }
        public double ConfidenceScore { get; set; }
        public List<Evidence> SupportingEvidence { get; set; } = new();
        public List<Evidence> ContradictingEvidence { get; set; } = new();
        public string Conclusion { get; set; } = string.Empty;
        public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
    }

    public enum VerificationStatus
    {
        Verified,
        PartiallyVerified,
        Unverified,
        Contradicted,
        Insufficient
    }

    public class Evidence
    {
        public string Source { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public double Reliability { get; set; }
        public DateTime? PublishedDate { get; set; }
    }

    /// <summary>
    /// Document indexing for knowledge base
    /// </summary>
    public class DocumentIndex
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DocumentPath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<DocumentChunk> Chunks { get; set; } = new();
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
        public IndexStatus Status { get; set; }
    }

    public class DocumentChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public float[]? Embedding { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public enum IndexStatus
    {
        Pending,
        Indexing,
        Completed,
        Failed
    }
}
