using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
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
    private readonly ConcurrentDictionary<string, bool> _activeDevices = new();
    private readonly ConcurrentDictionary<string, Task> _ipProcessingTasks = new();
    private readonly TimeSpan _taskProcessInterval = TimeSpan.FromMilliseconds(200);
    private readonly TimeSpan _deviceCheckInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _disabledCheckInterval = TimeSpan.FromSeconds(1);
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
    private int _retentionDays = 7;
    private bool _serviceEnabled;

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
        _logger.LogInformation("IO 处理服务已启动");
        _logger.LogInformation(
            "已加载 IO 任务清理配置。保留天数={RetentionDays}，清理间隔小时数={CleanupIntervalHours}",
            _retentionDays, _cleanupInterval.TotalHours);

        await _serviceToggleService.EnsureDefaultSettingsAsync(stoppingToken);
        _lastCleanupTime = DateTime.Now.AddMinutes(-1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await IsIoServiceEnabledAsync(stoppingToken))
                {
                    if (_serviceEnabled)
                    {
                        _serviceEnabled = false;
                        ClearActiveDevices();
                        _logger.LogInformation("IO 服务开关已关闭，设备处理循环将逐步停止");
                    }

                    await Task.Delay(_disabledCheckInterval, stoppingToken);
                    continue;
                }

                if (!_serviceEnabled)
                {
                    _serviceEnabled = true;
                    _logger.LogInformation("IO 服务开关已开启，恢复设备处理循环");
                }

                await CheckAndCleanupIOTasksAsync();

                var deviceIps = await _ioAgvTaskProcessor.GetAllDeviceIpsAsync();

                bool hasNewDevices = false;
                foreach (var ip in deviceIps)
                {
                    if (!_activeDevices.ContainsKey(ip))
                    {
                        _logger.LogInformation("发现 IO 设备：{DeviceIP}", ip);
                        _activeDevices[ip] = true;
                        hasNewDevices = true;
                    }
                }

                var inactiveDevices = _activeDevices.Keys.Except(deviceIps).ToList();
                foreach (var ip in inactiveDevices)
                {
                    _logger.LogInformation("IO 设备已移除或被禁用：{DeviceIP}", ip);
                    _activeDevices.TryRemove(ip, out _);
                }

                if (hasNewDevices || inactiveDevices.Any())
                {
                    _logger.LogInformation("当前活动的 IO 设备数量：{Count}", deviceIps.Count);
                }

                foreach (var ip in deviceIps)
                {
                    string deviceTaskKey = $"{ip}_device";

                    if (_ipProcessingTasks.TryGetValue(deviceTaskKey, out var existingTask))
                    {
                        if (existingTask.IsCompleted)
                        {
                            _ipProcessingTasks.TryRemove(deviceTaskKey, out _);

                            if (existingTask.IsFaulted && existingTask.Exception != null)
                            {
                                _logger.LogWarning(
                                    existingTask.Exception.InnerException ?? existingTask.Exception,
                                    "上一次设备任务异常结束，准备重新创建：{DeviceIP}",
                                    ip);
                            }

                            _logger.LogInformation("正在重新创建设备任务：{DeviceIP}", ip);
                            var processingTask = ProcessDeviceAsync(ip, stoppingToken);
                            _ipProcessingTasks[deviceTaskKey] = processingTask.ContinueWith(t =>
                            {
                                _ipProcessingTasks.TryRemove(deviceTaskKey, out _);

                                if (t.IsFaulted && t.Exception != null)
                                {
                                    _logger.LogError(
                                        t.Exception.InnerException ?? t.Exception,
                                        "设备任务中出现未处理异常：{DeviceIP}",
                                        ip);
                                }
                            }, TaskContinuationOptions.ExecuteSynchronously);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("正在创建设备任务：{DeviceIP}", ip);
                        var processingTask = ProcessDeviceAsync(ip, stoppingToken);

                        _ipProcessingTasks[deviceTaskKey] = processingTask.ContinueWith(t =>
                        {
                            _ipProcessingTasks.TryRemove(deviceTaskKey, out _);

                            if (t.IsFaulted && t.Exception != null)
                            {
                                _logger.LogError(
                                    t.Exception.InnerException ?? t.Exception,
                                    "设备任务中出现未处理异常：{DeviceIP}",
                                    ip);
                            }
                        }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                }

                var inactiveTaskKeys = _ipProcessingTasks.Keys
                    .Where(k => !deviceIps.Any(ip => $"{ip}_device" == k))
                    .ToList();

                foreach (var taskKey in inactiveTaskKeys)
                {
                    if (_ipProcessingTasks.TryRemove(taskKey, out _))
                    {
                        _logger.LogInformation("已清理过期的设备任务：{TaskKey}", taskKey);
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
                _logger.LogError(ex, "IO 处理服务主循环执行失败");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("IO 处理服务已停止");
    }

    private Task<bool> IsIoServiceEnabledAsync(CancellationToken cancellationToken)
    {
        return _serviceToggleService.IsEnabledAsync(
            ServiceSettingKeys.IOProcessorEnabled,
            true,
            cancellationToken);
    }

    private void ClearActiveDevices()
    {
        foreach (var ip in _activeDevices.Keys.ToList())
        {
            _activeDevices.TryRemove(ip, out _);
        }
    }

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

            var countSql = @"
                SELECT COUNT(1)
                FROM RCS_IOAGV_Tasks
                WHERE Status = 'Completed'
                  AND CompletedTime IS NOT NULL
                  AND CompletedTime < @CutoffDate";

            var count = await conn.ExecuteScalarAsync<int>(countSql, new { CutoffDate = cutoffDate });

            if (count == 0)
            {
                _logger.LogDebug("没有需要清理的 IO 任务。保留天数={RetentionDays}", _retentionDays);
                return;
            }

            _logger.LogInformation(
                "开始清理已完成的 IO 任务。保留天数={RetentionDays}，截止时间={CutoffDate}，预计数量={Count}",
                _retentionDays, cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"), count);

            const int batchSize = 1000;
            int totalDeleted = 0;

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

                await Task.Delay(100);
            }

            _logger.LogInformation(
                "IO 任务清理完成。删除数量={Count}，保留天数={RetentionDays}",
                totalDeleted, _retentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IO 任务清理失败");
        }
    }

    private async Task ProcessDeviceAsync(string deviceIp, CancellationToken stoppingToken)
    {
        _logger.LogInformation("设备 IO 循环已启动：{DeviceIP}", deviceIp);

        while (!stoppingToken.IsCancellationRequested && _activeDevices.ContainsKey(deviceIp))
        {
            var loopStopwatch = Stopwatch.StartNew();

            try
            {
                if (!await IsIoServiceEnabledAsync(stoppingToken))
                {
                    _activeDevices.TryRemove(deviceIp, out _);
                    _logger.LogInformation("IO 服务已关闭，停止设备 {DeviceIP} 的处理循环", deviceIp);
                    break;
                }

                _logger.LogDebug("设备 {DeviceIP} 开始执行 IO 周期", deviceIp);

                var taskProcessTask = _ioAgvTaskProcessor.ProcessTasksForDevice(deviceIp);
                var signalUpdateTask = _ioAgvTaskProcessor.UpdateIOSignalsForDevice(deviceIp);

                await Task.WhenAll(taskProcessTask, signalUpdateTask);

                loopStopwatch.Stop();
                _logger.LogDebug("设备 {DeviceIP} 的 IO 周期执行完成，耗时 {Duration}ms", deviceIp, loopStopwatch.ElapsedMilliseconds);

                await Task.Delay(_taskProcessInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                loopStopwatch.Stop();
                _logger.LogError(ex, "设备 {DeviceIP} 的 IO 周期执行失败", deviceIp);
                _logger.LogWarning("设备 {DeviceIP} 的 IO 周期异常结束，耗时 {Duration}ms", deviceIp, loopStopwatch.ElapsedMilliseconds);
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("设备 IO 循环已停止：{DeviceIP}", deviceIp);
    }
}
