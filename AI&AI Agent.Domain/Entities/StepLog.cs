using System;
using System.ComponentModel.DataAnnotations;

namespace AI_AI_Agent.Domain.Entities;

public class StepLog : BaseEntity
{
    [Key]
    public new Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Run Run { get; set; } = null!;
    public string Level { get; set; } = "info";
    public int? StepId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
}
