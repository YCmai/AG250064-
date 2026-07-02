namespace WarehouseManagementSystem.Models;

/// <summary>
/// PDA 扫码绑定记录。
/// </summary>
public class RCS_PdaTaskBinding
{
    /// <summary>主键 ID。</summary>
    public int ID { get; set; }

    /// <summary>工单号。</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>物料编码。</summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>物料名称。</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>托盘号。</summary>
    public string PalletNumber { get; set; } = string.Empty;

    /// <summary>PDA 扫码值。</summary>
    public string ScanCode { get; set; } = string.Empty;

    /// <summary>SSCC 码，当前由 PDA 扫码获得。</summary>
    public string Barcode { get; set; } = string.Empty;

    /// <summary>绑定状态。</summary>
    public int BindingStatus { get; set; }

    /// <summary>平板任务外部业务类型。</summary>
    public int ExternalTaskType { get; set; }

    /// <summary>回传状态：0待回传，1已回传，2回传失败，3无需回传。</summary>
    public int FeedbackStatus { get; set; }

    /// <summary>回传时间。</summary>
    public DateTime? FeedbackTime { get; set; }

    /// <summary>回传错误信息。</summary>
    public string? FeedbackError { get; set; }

    /// <summary>任务来源分组号。</summary>
    public string TaskGroupNo { get; set; } = string.Empty;

    /// <summary>已创建的用户任务请求号。</summary>
    public string RequestCode { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>备注。</summary>
    public string? Remarks { get; set; }
}
