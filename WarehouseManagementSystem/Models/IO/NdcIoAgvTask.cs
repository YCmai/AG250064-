namespace WarehouseManagementSystem.Models.Rcs;

public class NdcIoAgvTask
{
    public int Id { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DeviceIP { get; set; } = string.Empty;
    public string SignalAddress { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public bool Value { get; set; }
}


