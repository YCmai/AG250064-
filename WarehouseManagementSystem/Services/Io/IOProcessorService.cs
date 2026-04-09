using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Service.Io;
using WarehouseManagementSystem.Services;

public class IOProcessorService : BackgroundService
{
    private readonly ILogger<IOProcessorService> _logger;
    private readonly IOAGVTaskProcessor _ioAgvTaskProcessor;
    private readonly IDatabaseService _databaseService;
    private readonly IConfiguration _configuration;
    private readonly IServiceToggleService _serviceToggleService;

    // 记录当前仍然启用、并且应该继续处理的设备 IP。
    private readonly ConcurrentDictionary<string, bool> _activeDevices = new();

    // 为每台设备维护一个独立的后台处理任务，避免重复创建循环。
    private readonly ConcurrentDictionary<string, Task> _ipProcessingTasks = new();

    // 有任务时使用更短的轮询间隔，尽量压缩任务被拾取的等待时间。
    private readonly TimeSpan _taskProcessInterval = TimeSpan.FromMilliseconds(100);

    // 设备列表的变化不需要太高频，但 5 秒会让新设备接入和停用感知偏慢。
    private readonly TimeSpan _deviceCheckInterval = TimeSpan.FromSeconds(2);

    // 信号刷新只作为监控缓存更新，不再和任务处理抢同一轮执行机会。
    private readonly TimeSpan _signalRefreshInterval = TimeSpan.FromMilliseconds(800);

    // IO 任务清理配置。
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
    private int _retentionDays = 7;

    public IOProcessorService(
        ILogger<IOProcessorService> logger,
        IOAGVTaskProcessor ioAgvTaskProcessor,
        IDatabaseService databaseService,
        IConfiguration configuration,
        IServiceToggleService serviceToggleService)
    {
        _logger = logger;
        _ioAgvTaskProcessor = ioAgvTaskProcessor;
        _databaseService = databaseService;
        _configuration = configuration;
        _serviceToggleService = serviceToggleService;

        // 读取已完成任务的保留策略，避免任务表无限增长。
        var config = _configuration.GetSection("IOTaskCleanup").Get<CleanupConfig>() ?? new CleanupConfig();
        _cleanupInterval = TimeSpan.FromHours(config.CleanupIntervalHours);
        _retentionDays = config.RetentionDays;
    }

    private class CleanupConfig
    {
        public int RetentionDays { get; set; } = 7;
        public int CleanupIntervalHours { get; set; } = 24;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _serviceToggleService.EnsureDefaultSettingsAsync(stoppingToken);

        _logger.LogInformation("IO 处理服务已启动");
        _logger.LogInformation(
            "IO 任务清理配置：保留 {RetentionDays} 天，清理间隔 {CleanupIntervalHours} 小时",
            _retentionDays,
            _cleanupInterval.TotalHours);

        // 启动后尽快执行一次清理检查，但仍然保留一个很短的缓冲时间。
        _lastCleanupTime = DateTime.Now.AddMinutes(-1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enabled = await _serviceToggleService.IsEnabledAsync(
                    ServiceSettingKeys.IOProcessorEnabled,
                    true,
                    stoppingToken);

                if (!enabled)
                {
                    // 关闭开关后主动清空活跃设备集合，让每个设备循环自然退出。
                    _activeDevices.Clear();
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                await CheckAndCleanupIOTasksAsync();

                var deviceIps = await _ioAgvTaskProcessor.GetAllDeviceIpsAsync();

                bool hasDeviceListChanged = false;
                foreach (var ip in deviceIps)
                {
                    if (_activeDevices.TryAdd(ip, true))
                    {
                        hasDeviceListChanged = true;
                        _logger.LogInformation("发现新的 IO 设备：{DeviceIP}", ip);
                    }
                }

                var inactiveDevices = _activeDevices.Keys.Except(deviceIps).ToList();
                foreach (var ip in inactiveDevices)
                {
                    hasDeviceListChanged = true;
                    _activeDevices.TryRemove(ip, out _);
                    _logger.LogInformation("IO 设备已移除或被禁用：{DeviceIP}", ip);
                }

                if (hasDeviceListChanged)
                {
                    _logger.LogInformation("当前活跃 IO 设备数量：{Count}", deviceIps.Count);
                }

                foreach (var ip in deviceIps)
                {
                    var deviceTaskKey = $"{ip}_device";
                    if (_ipProcessingTasks.TryGetValue(deviceTaskKey, out var existingTask) && !existingTask.IsCompleted)
                    {
                        continue;
                    }

                    if (existingTask != null)
                    {
                        _ipProcessingTasks.TryRemove(deviceTaskKey, out _);
                        if (existingTask.IsFaulted && existingTask.Exception != null)
                        {
                            _logger.LogWarning(
                                existingTask.Exception.InnerException ?? existingTask.Exception,
                                "设备 {DeviceIP} 的处理任务异常结束，准备重新创建",
                                ip);
                        }
                    }

                    _logger.LogInformation("为设备 {DeviceIP} 创建独立处理任务", ip);
                    var processingTask = ProcessDeviceAsync(ip, stoppingToken);
                    _ipProcessingTasks[deviceTaskKey] = processingTask.ContinueWith(t =>
                    {
                        _ipProcessingTasks.TryRemove(deviceTaskKey, out _);
                        if (t.IsFaulted && t.Exception != null)
                        {
                            _logger.LogError(
                                t.Exception.InnerException ?? t.Exception,
                                "处理设备 {DeviceIP} 的后台任务发生未捕获异常",
                                ip);
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }

                var inactiveTaskKeys = _ipProcessingTasks.Keys
                    .Where(k => !deviceIps.Any(ip => $"{ip}_device" == k))
                    .ToList();

                foreach (var taskKey in inactiveTaskKeys)
                {
                    if (_ipProcessingTasks.TryRemove(taskKey, out _))
                    {
                        _logger.LogInformation("清理已下线设备的处理任务：{TaskKey}", taskKey);
                    }
                }

                await Task.Delay(_deviceCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IO 处理服务主循环发生异常");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("IO 处理服务已停止");
    }

    /// <summary>
    /// 定期清理已经完成且超过保留期限的 IO 任务。
    /// 这里只清理 Completed 状态，避免把仍在等待的业务任务误删。
    /// </summary>
    private async Task CheckAndCleanupIOTasksAsync()
    {
        if (DateTime.Now - _lastCleanupTime < _cleanupInterval)
        {
            return;
        }

        try
        {
            _lastCleanupTime = DateTime.Now;
            using var conn = _databaseService.CreateConnection();

            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            const int batchSize = 1000;

            var countSql = @"
                SELECT COUNT(1)
                FROM RCS_IOAGV_Tasks
                WHERE Status = 'Completed'
                  AND CompletedTime IS NOT NULL
                  AND CompletedTime < @CutoffDate";

            var count = await conn.ExecuteScalarAsync<int>(countSql, new { CutoffDate = cutoffDate });
            if (count == 0)
            {
                _logger.LogDebug("未找到需要清理的 IO 任务，当前保留天数为 {RetentionDays}", _retentionDays);
                return;
            }

            _logger.LogInformation(
                "开始清理 IO 任务，保留 {RetentionDays} 天，截止时间 {CutoffDate}，预计删除 {Count} 条记录",
                _retentionDays,
                cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                count);

            var totalDeleted = 0;
            while (true)
            {
                var deleteSql = $@"
                    DELETE TOP ({batchSize}) FROM RCS_IOAGV_Tasks
                    WHERE Status = 'Completed'
                      AND CompletedTime IS NOT NULL
                      AND CompletedTime < @CutoffDate";

                var deletedInBatch = await conn.ExecuteAsync(deleteSql, new { CutoffDate = cutoffDate });
                totalDeleted += deletedInBatch;

                if (deletedInBatch < batchSize)
                {
                    break;
                }

                // 分批删除之间留一个很短的间隔，避免持续占用数据库资源。
                await Task.Delay(100);
            }

            _logger.LogInformation(
                "IO 任务清理完成，共删除 {Count} 条记录，保留天数 {RetentionDays}",
                totalDeleted,
                _retentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理 IO 任务时发生异常");
        }
    }

    /// <summary>
    /// 为单台设备维持一个独立的处理循环。
    /// 这里遵循两个原则：
    /// 1. 任务处理优先，因为它直接影响外部系统的实时交互。
    /// 2. 信号刷新退到次优先级，只在当前没有待处理任务时执行，用于更新监控缓存表。
    /// </summary>
    private async Task ProcessDeviceAsync(string deviceIp, CancellationToken stoppingToken)
    {
        _logger.LogInformation("开始处理设备 {DeviceIP} 的 IO 操作", deviceIp);

        var lastSignalRefreshTime = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested && _activeDevices.ContainsKey(deviceIp))
        {
            try
            {
                var enabled = await _serviceToggleService.IsEnabledAsync(
                    ServiceSettingKeys.IOProcessorEnabled,
                    true,
                    stoppingToken);
                if (!enabled)
                {
                    break;
                }

                var hasPendingTasks = await _ioAgvTaskProcessor.ProcessTasksForDevice(deviceIp, stoppingToken);

                if (!hasPendingTasks && DateTime.UtcNow - lastSignalRefreshTime >= _signalRefreshInterval)
                {
                    await _ioAgvTaskProcessor.UpdateIOSignalsForDevice(deviceIp, stoppingToken);
                    lastSignalRefreshTime = DateTime.UtcNow;
                }

                var delay = hasPendingTasks
                    ? TimeSpan.FromMilliseconds(20)
                    : _taskProcessInterval;

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理设备 {DeviceIP} 的 IO 操作时发生异常", deviceIp);
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("设备 {DeviceIP} 的 IO 处理任务已停止", deviceIp);
    }
}
