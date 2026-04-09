using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseManagementSystem.Models.Ndc;

public class CoupEventLog : NdcEntityBase<int>
{
    [NotMapped]
    public int ID { get => Id; set => Id = value; }

    /// <summary>事件代码</summary>
    public int EventCode { get; set; }

    /// <summary>交互参数 1 (如站点、地址等)</summary>
    public int Parameter1 { get; set; }

    /// <summary>交互参数 2</summary>
    public int Parameter2 { get; set; }

    /// <summary>交互参数 3</summary>
    public int Parameter3 { get; set; }

    /// <summary>原始报文字符串</summary>
    public string? EventString { get; set; }

    /// <summary>事件产生时间</summary>
    public DateTime EventTime { get; set; }

    /// <summary>是否已处理执行完成</summary>
    public bool Excute { get; set; }

    /// <summary>执行时间</summary>
    public DateTime? ExcuteTime { get; set; }

    /// <summary>重试次数/统计计数</summary>
    public int Count { get; set; }
}


