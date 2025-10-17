using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Domain.Entities;

public class Run : BaseEntity
{
    [Key]
    public new Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public RunStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public string? PlanJson { get; set; }
    public ICollection<StepLog> StepLogs { get; set; } = new List<StepLog>();
    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
