using Newtonsoft.Json;

namespace WarehouseManagementSystem.Models.Rcs;

/// <summary>
/// WMS 统一返回结构。
/// 目前 3 个接口都按 flag/errorMsg 解析。
/// </summary>
public class RcsWmsResponseDto
{
    [JsonProperty("flag")]
    public string Flag { get; set; } = "-1";

    [JsonProperty("errorMsg")]
    public string? ErrorMsg { get; set; }

    [JsonIgnore]
    public bool IsSuccess => string.Equals(Flag, "0", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 物料到达生产线请求 DTO。
/// </summary>
public sealed class RcsWmsMaterialArrivalRequestDto
{
    [JsonProperty("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonProperty("palletNumber")]
    public string PalletNumber { get; set; } = string.Empty;

    [JsonProperty("items")]
    public List<RcsWmsMaterialArrivalItemDto> Items { get; set; } = new();
}

/// <summary>
/// 物料到达生产线子表项。
/// </summary>
public sealed class RcsWmsMaterialArrivalItemDto
{
    [JsonProperty("barcode")]
    public string Barcode { get; set; } = string.Empty;
}

/// <summary>
/// 安全信号请求 DTO。
/// requestDate 按 yyyyMMddHHmmss 发送。
/// </summary>
public sealed class RcsWmsSafetySignalRequestDto
{
    [JsonProperty("taskNumber")]
    public string TaskNumber { get; set; } = string.Empty;

    [JsonProperty("requestDate")]
    public string RequestDate { get; set; } = string.Empty;

    [JsonProperty("room")]
    public string Room { get; set; } = string.Empty;
}

/// <summary>
/// 安全信号返回 DTO。
/// 除了 flag/errorMsg 外，还额外读取 safeFlag。
/// </summary>
public sealed class RcsWmsSafetySignalResponseDto : RcsWmsResponseDto
{
    [JsonProperty("safeFlag")]
    public string? SafeFlag { get; set; }
}

/// <summary>
/// 作业完成反馈请求 DTO。
/// status: 1=完成，2=终止/取消。
/// </summary>
public sealed class RcsWmsJobFeedbackRequestDto
{
    [JsonProperty("taskNumber")]
    public string TaskNumber { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 统一派发结果。
/// 业务层可直接根据 Success / ErrorMsg / NextRetryTime 做后续处理。
/// </summary>
public sealed class RcsWmsDispatchResult
{
    public bool Success { get; set; }
    public string Flag { get; set; } = "-1";
    public string? ErrorMsg { get; set; }
    public string? ResponseJson { get; set; }
    public string? SafeFlag { get; set; }
    public DateTime? NextRetryTime { get; set; }
}
