namespace WarehouseManagementSystem.Models;

/// <summary>
/// AGV 主动上报统一出站队列表。
/// 存储物料达到生产线、安全信号、作业完成反馈三类待发送数据。
/// </summary>
public class RCS_AgvOutboundQueue
{
    /// <summary>主键。</summary>
    public int ID { get; set; }

    /// <summary>事件类型：1=物料达到生产线，2=安全信号，3=作业完成反馈。</summary>
    public int EventType { get; set; }

    /// <summary>任务号/业务号。</summary>
    public string TaskNumber { get; set; } = string.Empty;

    /// <summary>幂等业务键（防重复）。</summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>请求体 JSON。</summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>处理状态：0=待处理，1=成功，2=失败待重试，3=失败终态（超重试上限）。</summary>
    public int ProcessStatus { get; set; }

    /// <summary>重试次数。</summary>
    public int RetryCount { get; set; }

    /// <summary>最近错误信息。</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>下次重试时间。</summary>
    public DateTime? NextRetryTime { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>处理完成时间。</summary>
    public DateTime? ProcessTime { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime UpdateTime { get; set; }
}
