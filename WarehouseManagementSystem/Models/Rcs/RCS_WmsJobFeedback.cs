namespace WarehouseManagementSystem.Models.Rcs;

/// <summary>
/// 作业完成反馈业务表实体。
/// </summary>
public class RCS_WmsJobFeedback
{
    public int ID { get; set; }
    public int RequestLogId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 作业完成反馈入库请求模型。
/// </summary>
public class RcsWmsJobFeedbackCreateRequest
{
    public string TaskNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RequestJson { get; set; }
}
