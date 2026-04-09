using Microsoft.Extensions.Hosting;

namespace WarehouseManagementSystem.Services.Ndc;

/// <summary>
/// ACI 连接守护后台服务。
/// 应用启动后持续确保 ACI 与 NDC 的底层连接存在，断线时主动触发重连。
/// </summary>
public sealed class AciConnectionHostedService : BackgroundService
{
    private readonly AciAppManager _aciAppManager;
    private readonly ILogger<AciConnectionHostedService> _logger;

    public AciConnectionHostedService(
        AciAppManager aciAppManager,
        ILogger<AciConnectionHostedService> logger)
    {
        _aciAppManager = aciAppManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ACI 连接守护服务已启动。");

        // 启动时先触发一次连接，确保 AciAppManager 不只是被注册而是实际进入工作状态。
        TryEnsureConnected();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            TryEnsureConnected();
        }
    }

    private void TryEnsureConnected()
    {
        try
        {
            if (_aciAppManager.AciClient.Connected)
            {
                return;
            }

            _logger.LogWarning("检测到 ACI 当前未连接，开始触发重连。");
            _aciAppManager.EnsureConnected();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACI 连接守护服务执行重连时发生异常。");
        }
    }
}
