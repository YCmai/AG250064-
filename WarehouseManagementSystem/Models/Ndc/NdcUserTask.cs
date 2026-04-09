using System.ComponentModel.DataAnnotations.Schema;
using WarehouseManagementSystem.Shared.Ndc;
using NdcTaskStatuEnum = WarehouseManagementSystem.Shared.Ndc.TaskStatuEnum;

namespace WarehouseManagementSystem.Models.Ndc;

public class NdcUserTask : NdcEntityBase<int>
{

    public NdcTaskStatuEnum taskStatus { get; set; }
    public DateTime? executedTime { get; set; }
    public string? runTaskId { get; set; }
    public DateTime? startTime { get; set; }
    public bool executed { get; set; }
    public DateTime? creatTime { get; set; }
    public DateTime? endTime { get; set; }
    public string? requestCode { get; set; }
    public TaskTypeEnum taskType { get; set; }
    public int? priority { get; set; }
    public string? robotCode { get; set; } = "0";
    public string? sourcePosition { get; set; }
    public string? targetPosition { get; set; }
    public string? taskGroupNo { get; set; }
    public string? palletNo { get; set; }
    public string? binNumber { get; set; }
    public int? weight { get; set; }
    public string? remarks { get; set; }
    public int? userPriority { get; set; }
    public string? taskCode { get; set; }
    public bool IsCancelled { get; set; }
}



