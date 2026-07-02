namespace WarehouseManagementSystem.Models;

/// <summary>
/// AGV 安全交互状态表。
/// 仅用于记录当前任务在安全联锁点的最近一次请求/响应结果，
/// 便于 NDC 循环进入同步步骤时判断是否允许继续执行。
/// </summary>
public class RCS_AgvSafetyHandshake
{
    /// <summary>主键。</summary>
    public int ID { get; set; }

    /// <summary>任务号。</summary>
    public string TaskNumber { get; set; } = string.Empty;

    /// <summary>安全交互房间/区域。</summary>
    public string Room { get; set; } = string.Empty;

    /// <summary>业务请求时间。</summary>
    public DateTime RequestDate { get; set; }

    /// <summary>最近一次实际请求时间。</summary>
    public DateTime? LastRequestTime { get; set; }

    /// <summary>最近一次收到响应时间。</summary>
    public DateTime? LastResponseTime { get; set; }

    /// <summary>最近一次接口返回的安全标记，Y=安全，N=不安全。</summary>
    public string SafeFlag { get; set; } = string.Empty;

    /// <summary>业务状态：0=待确认，1=安全放行，2=不安全等待，3=接口失败。</summary>
    public int ProcessStatus { get; set; }

    /// <summary>累计请求次数。</summary>
    public int RetryCount { get; set; }

    /// <summary>最近一次错误信息。</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>最近一次响应报文。</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime UpdateTime { get; set; }
}
