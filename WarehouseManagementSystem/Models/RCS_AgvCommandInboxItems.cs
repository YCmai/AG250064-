namespace WarehouseManagementSystem.Models;

/// <summary>
/// AGV 指令收件箱子表实体（items）。
/// </summary>
public class RCS_AgvCommandInboxItems
{
    /// <summary>主键ID。</summary>
    public int ID { get; set; }
    /// <summary>收件箱主表ID。</summary>
    public int InboxId { get; set; }
    /// <summary>任务顺序（对应接口 items[].seq）。</summary>
    public int Seq { get; set; }
    /// <summary>托盘号。</summary>
    public string? PalletNumber { get; set; }
    /// <summary>Bin编号。</summary>
    public string? BinNumber { get; set; }
    /// <summary>起点站点。</summary>
    public string? FromStation { get; set; }
    /// <summary>终点站点。</summary>
    public string ToStation { get; set; } = string.Empty;
    /// <summary>任务类型（1运料/2取空托/3送空bin/4取满bin/5退料）。</summary>
    public int TaskType { get; set; }
    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }
}
