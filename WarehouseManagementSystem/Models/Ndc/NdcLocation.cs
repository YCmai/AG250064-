namespace WarehouseManagementSystem.Models.Ndc;

/// <summary>
/// NDC 储位/工位模型。对应数据库中的 RCS_Locations 表。
/// </summary>
public class NdcLocation : NdcEntityBase<int>
{
    /// <summary>储位名称</summary>
    public string? Name { get; set; }

    /// <summary>节点备注/地标编号 (核心定位标识)</summary>
    public string? NodeRemark { get; set; }

    /// <summary>物料编码 (当前储位存放的物料)</summary>
    public string? MaterialCode { get; set; }

    /// <summary>托盘 ID / 载具编号</summary>
    public string? PalletID { get; set; } = "0";

    /// <summary>重量数据</summary>
    public string? Weight { get; set; } = "0";

    /// <summary>数量</summary>
    public string? Quanitity { get; set; }

    /// <summary>入库日期</summary>
    public string? EntryDate { get; set; }

    /// <summary>区域/分组 (Group)</summary>
    public string? Group { get; set; }

    /// <summary>取货/举升高度</summary>
    public int LiftingHeight { get; set; }

    /// <summary>卸货/放置高度</summary>
    public int UnloadHeight { get; set; }

    /// <summary>是否锁定 (1: 锁定, 0: 解锁)</summary>
    public bool Lock { get; set; }

    /// <summary>是否启用 (1: 启用, 0: 禁用)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>等待节点/排队点地标</summary>
    public string? WattingNode { get; set; }

    /// <summary>进站方向 (0: 默认, 其他: 特定方向)</summary>
    public int direction { get; set; }
}


