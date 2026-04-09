using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseManagementSystem.Models.Ndc;

public abstract class NdcEntityBase<TKey>
{
    public TKey Id { get; set; } = default!;
}

public abstract class TaskBaseEntity : NdcEntityBase<Guid>
{
    [Column("ParentId")]
    public Guid FatherTaskId { get; set; }

    [Column("ParentTaskTypeFullName")]
    public string? FatherTaskType { get; set; }

    public DateTime CreationTime { get; set; } = DateTime.Now;

    public DateTime? CloseTime { get; set; }
}


