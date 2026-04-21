namespace WarehouseManagementSystem.Models;

/// <summary>
/// MES 下发给 AGV 的工单记录。
/// </summary>
public class RCS_WorkOrder
{
    public int ID { get; set; }

    /// <summary>
    /// 亚批号
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// 产品编码
    /// </summary>
    public string MaterialNumber { get; set; } = string.Empty;

    /// <summary>
    /// 产品名称
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型 (1: 生效; 2: 失效)
    /// </summary>
    public string MsgType { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 处理状态
    /// </summary>
    public int ProcessStatus { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }
}
