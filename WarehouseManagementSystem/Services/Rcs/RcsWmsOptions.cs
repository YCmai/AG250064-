namespace WarehouseManagementSystem.Services.Rcs;

/// <summary>
/// WMS 交互配置。
/// </summary>
public sealed class RcsWmsOptions
{
    public const string SectionName = "RcsWmsOutbound";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
    public string MaterialArrivalEndpoint { get; set; } = string.Empty;
    public string SafetySignalEndpoint { get; set; } = string.Empty;
    public string JobFeedbackEndpoint { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int SafetyRetryIntervalSeconds { get; set; } = 30;
    public int SafetyBatchSize { get; set; } = 20;
}
