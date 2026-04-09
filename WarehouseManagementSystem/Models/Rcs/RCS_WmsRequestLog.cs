namespace WarehouseManagementSystem.Models.Rcs;

/// <summary>
/// WMS 业务类型。
/// </summary>
public enum RcsWmsBusinessType
{
    MaterialArrival = 1,
    SafetySignal = 2,
    JobFeedback = 3
}

/// <summary>
/// WMS 请求日志状态。
/// </summary>
public enum RcsWmsRequestStatus
{
    Pending = 0,
    Success = 1,
    PendingRetry = 2,
    Failed = -1
}

/// <summary>
/// WMS 请求日志主表实体。
/// 所有对 WMS 的发送都会先落一条日志在这里。
/// </summary>
public class RCS_WmsRequestLog
{
    public int ID { get; set; }
    public int BusinessType { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string? TaskNumber { get; set; }
    public string? OrderNumber { get; set; }
    public string? RequestUrl { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public int RequestStatus { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRequestTime { get; set; }
    public DateTime? LastResponseTime { get; set; }
    public DateTime? NextRetryTime { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}
