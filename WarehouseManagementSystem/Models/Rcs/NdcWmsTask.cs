namespace WarehouseManagementSystem.Models.Rcs;

public class NdcWmsTask
{
    /// <summary>任务 ID</summary>
    public int ID { get; set; }
    /// <summary>请求唯一编码</summary>
    public string? RequestId { get; set; }
    /// <summary>目标库位地标</summary>
    public string? ToLocation { get; set; }
}


