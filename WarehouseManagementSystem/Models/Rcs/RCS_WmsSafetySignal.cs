namespace WarehouseManagementSystem.Models.Rcs;

/// <summary>
/// 安全信号业务表实体。
/// </summary>
public class RCS_WmsSafetySignal
{
    public int ID { get; set; }
    public int RequestLogId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string Room { get; set; } = string.Empty;
    public string? SafeFlag { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 安全信号入库请求模型。
/// </summary>
public class RcsWmsSafetySignalCreateRequest
{
    public string TaskNumber { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string Room { get; set; } = string.Empty;
    public string? RequestJson { get; set; }
}
