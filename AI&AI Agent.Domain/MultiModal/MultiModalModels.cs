namespace AI_AI_Agent.Domain.MultiModal
{
    /// <summary>
    /// Image understanding and generation
    /// </summary>
    public class ImageAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ImagePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DetectedObject> Objects { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<TextRegion> TextRegions { get; set; } = new();
        public ImageMetadata Metadata { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    public class DetectedObject
    {
        public string Label { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public BoundingBox BoundingBox { get; set; } = new();
    }

    public class BoundingBox
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class TextRegion
    {
        public string Text { get; set; } = string.Empty;
        public BoundingBox BoundingBox { get; set; } = new();
        public double Confidence { get; set; }
    }

    public class ImageMetadata
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string? ColorSpace { get; set; }
    }

    /// <summary>
    /// Image generation request
    /// </summary>
    public class ImageGenerationRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Prompt { get; set; } = string.Empty;
        public string? NegativePrompt { get; set; }
        public ImageStyle Style { get; set; }
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public int Steps { get; set; } = 50;
        public double GuidanceScale { get; set; } = 7.5;
        public string? Seed { get; set; }
    }

    public enum ImageStyle
    {
        Realistic,
        Artistic,
        Cartoon,
        Anime,
        Abstract,
        Photography
    }

    /// <summary>
    /// Document understanding with OCR and layout analysis
    /// </summary>
    public class DocumentUnderstanding
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DocumentPath { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public string ExtractedText { get; set; } = string.Empty;
        public DocumentLayout Layout { get; set; } = new();
        public List<DocumentPage> Pages { get; set; } = new();
        public List<DocumentTable> Tables { get; set; } = new();
        public List<DocumentForm> Forms { get; set; } = new();
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    public enum DocumentType
    {
        Invoice,
        Receipt,
        Form,
        Contract,
        Report,
        Letter,
        Other
    }

    public class DocumentLayout
    {
        public List<LayoutRegion> Regions { get; set; } = new();
        public ReadingOrder ReadingOrder { get; set; } = new();
    }

    public class LayoutRegion
    {
        public string Type { get; set; } = string.Empty; // title, paragraph, table, image
        public BoundingBox BoundingBox { get; set; } = new();
        public string? Content { get; set; }
        public int Confidence { get; set; }
    }

    public class ReadingOrder
    {
        public List<string> RegionIds { get; set; } = new();
    }

    public class DocumentPage
    {
        public int PageNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DocumentTable
    {
        public int PageNumber { get; set; }
        public List<TableRow> Rows { get; set; } = new();
        public int ColumnCount { get; set; }
        public BoundingBox BoundingBox { get; set; } = new();
    }

    public class TableRow
    {
        public List<TableCell> Cells { get; set; } = new();
    }

    public class TableCell
    {
        public string Content { get; set; } = string.Empty;
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;
    }

    public class DocumentForm
    {
        public List<FormField> Fields { get; set; } = new();
    }

    public class FormField
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public FieldType Type { get; set; }
        public BoundingBox BoundingBox { get; set; } = new();
    }

    public enum FieldType
    {
        Text,
        Checkbox,
        RadioButton,
        Dropdown,
        Signature,
        Date
    }

    /// <summary>
    /// Diagram and chart interpretation
    /// </summary>
    public class DiagramInterpretation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ImagePath { get; set; } = string.Empty;
        public DiagramType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<DiagramElement> Elements { get; set; } = new();
        public List<DiagramRelationship> Relationships { get; set; } = new();
        public string? TextualRepresentation { get; set; }
        public DateTime InterpretedAt { get; set; } = DateTime.UtcNow;
    }

    public enum DiagramType
    {
        Flowchart,
        UML,
        ER,
        NetworkDiagram,
        Chart,
        Timeline,
        MindMap,
        Other
    }

    public class DiagramElement
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public BoundingBox BoundingBox { get; set; } = new();
    }

    public class DiagramRelationship
    {
        public string FromElementId { get; set; } = string.Empty;
        public string ToElementId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Label { get; set; }
    }

    /// <summary>
    /// Audio transcription (future capability)
    /// </summary>
    public class AudioTranscription
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AudioPath { get; set; } = string.Empty;
        public string Transcript { get; set; } = string.Empty;
        public List<TranscriptSegment> Segments { get; set; } = new();
        public AudioMetadata Metadata { get; set; } = new();
        public DateTime TranscribedAt { get; set; } = DateTime.UtcNow;
    }

    public class TranscriptSegment
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? Speaker { get; set; }
    }

    public class AudioMetadata
    {
        public double DurationSeconds { get; set; }
        public string Format { get; set; } = string.Empty;
        public int SampleRate { get; set; }
        public int Channels { get; set; }
    }
}
