using System;
using System.ComponentModel.DataAnnotations;

namespace AI_AI_Agent.Domain.Entities;

public class Artifact : BaseEntity
{
    [Key]
    public new Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Run Run { get; set; } = null!;
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
}
