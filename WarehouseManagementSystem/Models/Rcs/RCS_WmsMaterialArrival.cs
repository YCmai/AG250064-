namespace WarehouseManagementSystem.Models.Rcs;

/// <summary>
/// 物料到达生产线主表实体。
/// </summary>
public class RCS_WmsMaterialArrival
{
    public int ID { get; set; }
    public int RequestLogId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string PalletNumber { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 物料到达生产线子表实体。
/// 每一条记录代表一个 SSCC 条码。
/// </summary>
public class RCS_WmsMaterialArrivalItem
{
    public int ID { get; set; }
    public int MaterialArrivalId { get; set; }
    public string Barcode { get; set; } = string.Empty;
}

/// <summary>
/// 物料到达生产线入库请求模型。
/// 业务层插库时使用这个对象。
/// </summary>
public class RcsWmsMaterialArrivalCreateRequest
{
    public string OrderNumber { get; set; } = string.Empty;
    public string PalletNumber { get; set; } = string.Empty;
    public List<string> Barcodes { get; set; } = new();
    public string? RequestJson { get; set; }
}
