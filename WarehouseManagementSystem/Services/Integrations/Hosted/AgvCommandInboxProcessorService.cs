using WarehouseManagementSystem.Services.Integrations;

namespace WarehouseManagementSystem.Services.Integrations.Hosted;

/// <summary>
/// 消费 AGV 指令收件箱，将已接收的外部指令转换为 RCS_UserTasks 可执行任务。
/// </summary>
public class AgvCommandInboxProcessorService : BackgroundService
{
    private readonly IAgvIntegrationService _agvIntegrationService;
    private readonly ILogger<AgvCommandInboxProcessorService> _logger;

    public AgvCommandInboxProcessorService(
        IAgvIntegrationService agvIntegrationService,
        ILogger<AgvCommandInboxProcessorService> logger)
    {
        _agvIntegrationService = agvIntegrationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _agvIntegrationService.ProcessPendingCommandInboxAsync(20, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理AGV指令收件箱失败");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
