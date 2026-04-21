using WarehouseManagementSystem.Services.Integrations;

namespace WarehouseManagementSystem.Services.Integrations.Hosted;

/// <summary>
/// AGV 主动上报出站队列消费服务。
/// 周期扫描统一出站表中的待处理记录并异步发送到上位机。
/// </summary>
public class AgvOutboundProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgvOutboundProcessorService> _logger;

    public AgvOutboundProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgvOutboundProcessorService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAgvOutboundInteractionService>();
                await service.ProcessPendingAsync(20, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 AGV 主动上报出站队列异常");
            }
        }
    }
}
