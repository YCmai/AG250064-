namespace WarehouseManagementSystem.Models.DTOs.Integrations;

/// <summary>
/// MES 下发给 AGV 的工单请求。
/// </summary>
public class AgvWorkOrderRequest
{
    /// <summary>亚批号，最大长度 14。</summary>
    public string? OrderNumber { get; set; }

    /// <summary>产品编码，最大长度 18。</summary>
    public string? MaterialNumber { get; set; }

    /// <summary>产品名称，最大长度 40。</summary>
    public string? MaterialName { get; set; }

    /// <summary>消息类型：1 生效，2 失效。</summary>
    public string? MsgType { get; set; }
}

/// <summary>
/// MES 下发给 AGV 的任务组请求。
/// </summary>
public class AgvCommandRequest
{
    /// <summary>任务号，固定长度 17。</summary>
    public string? TaskNumber { get; set; }

    /// <summary>优先级：1 高，2 中，3 低。</summary>
    public int? Priority { get; set; }

    /// <summary>任务明细列表，字段名固定为 items。</summary>
    public List<AgvCommandItem> Items { get; set; } = new();
}

/// <summary>
/// AGV 任务组中的单条明细（items 节点）。
/// </summary>
public class AgvCommandItem
{
    /// <summary>任务顺序。</summary>
    public int Seq { get; set; }

    /// <summary>托盘号。</summary>
    public string? PalletNumber { get; set; }

    /// <summary>Bin 编号。</summary>
    public string? BinNumber { get; set; }

    /// <summary>起点站点。</summary>
    public string? FromStation { get; set; }

    /// <summary>终点站点。</summary>
    public string? ToStation { get; set; }

    /// <summary>任务类型：1运料，2取空托，3送空bin，4取满bin，5退料。</summary>
    public int? TaskType { get; set; }
}
