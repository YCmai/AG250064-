namespace WarehouseManagementSystem.Models;

/// <summary>
/// AGV 指令收件箱主表实体。
/// </summary>
public class RCS_AgvCommandInbox
{
    /// <summary>主键ID。</summary>
    public int ID { get; set; }
    /// <summary>任务号（幂等键，对应接口 taskNumber）。</summary>
    public string TaskNumber { get; set; } = string.Empty;
    /// <summary>优先级（1高/2中/3低）。</summary>
    public int Priority { get; set; }
    /// <summary>原始请求 JSON，用于追溯和补偿。</summary>
    public string? RawJson { get; set; }
    /// <summary>处理状态：0待处理，1已处理，2处理失败。</summary>
    public int ProcessStatus { get; set; }
    /// <summary>处理错误信息。</summary>
    public string? ErrorMsg { get; set; }
    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }
    /// <summary>更新时间。</summary>
    public DateTime? UpdateTime { get; set; }
    /// <summary>处理完成时间。</summary>
    public DateTime? ProcessTime { get; set; }
}
