using WarehouseManagementSystem.Services.Integrations;

namespace WarehouseManagementSystem.Services.Integrations.Hosted;

/// <summary>
/// 已停用的 AGV 指令收件箱后台消费者。
/// Why: 当前项目已改为“接口接收后同步拆分并直接写入 RCS_UserTasks”的主线，
/// 不再通过后台轮询二次消费收件箱，保留该类仅用于历史追溯，默认不再注册到 DI。
/// </summary>
public class AgvCommandInboxProcessorService : BackgroundService
{
    private readonly ILogger<AgvCommandInboxProcessorService> _logger;

    public AgvCommandInboxProcessorService(
        ILogger<AgvCommandInboxProcessorService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgvCommandInboxProcessorService 已停用，当前不再执行后台收件箱消费。");
        await Task.CompletedTask;
    }
}
