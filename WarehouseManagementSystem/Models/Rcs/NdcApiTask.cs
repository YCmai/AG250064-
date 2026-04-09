namespace WarehouseManagementSystem.Models.Rcs;

public class NdcApiTask
{
    public int ID { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public bool Excute { get; set; }
    public DateTime CreateTime { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TaskType { get; set; }
}


