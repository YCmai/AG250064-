using Microsoft.Extensions.Options;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Services.Rcs;

/// <summary>
/// 安全信号后台重试服务。
/// 这里只负责定时触发，真正的查询、发送、回写都交给 IRcsWmsService。
/// </summary>
public sealed class RcsWmsSafetySignalRetryHostedService : BackgroundService
{
    private readonly IRcsWmsService _rcsWmsService;
    private readonly ILogger<RcsWmsSafetySignalRetryHostedService> _logger;
    private readonly IOptions<RcsWmsOptions> _options;
    private readonly IServiceToggleService _serviceToggleService;

    public RcsWmsSafetySignalRetryHostedService(
        IRcsWmsService rcsWmsService,
        ILogger<RcsWmsSafetySignalRetryHostedService> logger,
        IOptions<RcsWmsOptions> options,
        IServiceToggleService serviceToggleService)
    {
        _rcsWmsService = rcsWmsService;
        _logger = logger;
        _options = options;
        _serviceToggleService = serviceToggleService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serviceToggleService.EnsureDefaultSettingsAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var retryEnabled = await _serviceToggleService.IsEnabledAsync(
                ServiceSettingKeys.RcsWmsSafetyRetryEnabled,
                true,
                stoppingToken);

            if (!_options.Value.Enabled || !retryEnabled)
            {
                continue;
            }

            try
            {
                await _rcsWmsService.ProcessDueSafetySignalsAsync(_options.Value.SafetyBatchSize, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 WMS 安全信号重试任务时发生异常");
            }
        }
    }
}
