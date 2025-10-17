using System.ComponentModel.DataAnnotations.Schema;

namespace AI_AI_Agent.Domain.Entities;

public class Embedding : BaseEntity
{
    public Guid MessageId { get; set; }
    public virtual Message Message { get; set; } = null!;

    public float[]? Vector { get; set; }
}
