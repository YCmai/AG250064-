using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseManagementSystem.Models.Ndc;

public class NdcWmsInteraction : NdcEntityBase<int>
{
    [NotMapped]
    public int ID { get => Id; set => Id = value; }

    /// <summary>交互类型</summary>
    public int InteractionType { get; set; }

    /// <summary>请求唯一 ID</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>分段编号</summary>
    public string SegmentNo { get; set; } = string.Empty;

    /// <summary>托盘号</summary>
    public string? PalletNo { get; set; }

    /// <summary>重量</summary>
    public int? Weight { get; set; }

    /// <summary>库位/站点编号</summary>
    public string LocationNo { get; set; } = string.Empty;

    /// <summary>出库任务单号</summary>
    public string? OutboundTaskNo { get; set; }

    /// <summary>底盘/基础托盘号</summary>
    public string? BasePalletNo { get; set; }

    /// <summary>业务任务单号</summary>
    public string TaskNo { get; set; } = string.Empty;

    /// <summary>关联逻辑库位 ID</summary>
    public int? LocationId { get; set; }

    /// <summary>任务类型标识</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>异常/错误详细描述</summary>
    public string? ErrorDescription { get; set; }

    /// <summary>交互记录状态</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>备注说明</summary>
    public string? Remarks { get; set; }

    /// <summary>货物条码</summary>
    public string? CargoBarcode { get; set; }
}


