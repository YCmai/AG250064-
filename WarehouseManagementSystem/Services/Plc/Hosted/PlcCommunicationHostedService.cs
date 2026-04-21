using Microsoft.Extensions.Hosting;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Service.Plc
{
    /// <summary>
    /// PLC 通讯托管服务。
    /// 该服务不再在启动时直接强行拉起 PLC 通讯，而是持续监听设置开关：
    /// 开启时启动底层 PLC 通讯服务，关闭时安全停止，避免异常日志持续刷屏。
    /// </summary>
    public class PlcCommunicationHostedService : IHostedService, IDisposable
    {
        private readonly IPlcCommunicationService _plcCommunicationService;
        private readonly ILogger<PlcCommunicationHostedService> _logger;
        private readonly IServiceToggleService _serviceToggleService;
        private readonly TimeSpan _monitorInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _settingCheckInterval = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _minServiceRestartInterval = TimeSpan.FromMinutes(5);

        private Task? _managerTask;
        private CancellationTokenSource? _managerCts;
        private Task? _monitorTask;
        private CancellationTokenSource? _monitorCts;
        private DateTime _lastServiceRestartTime = DateTime.MinValue;
        private bool _serviceStarted;

        public PlcCommunicationHostedService(
            IPlcCommunicationService plcCommunicationService,
            ILogger<PlcCommunicationHostedService> logger,
            IServiceToggleService serviceToggleService)
        {
            _plcCommunicationService = plcCommunicationService ?? throw new ArgumentNullException(nameof(plcCommunicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceToggleService = serviceToggleService;
        }

        /// <summary>
        /// 启动托管服务本身。
        /// 实际的 PLC 通讯是否运行，由配置开关决定。
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 通讯托管服务正在启动");
            _managerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _managerTask = ManageServiceStateAsync(_managerCts.Token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止托管服务并安全关闭 PLC 通讯与监控任务。
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 通讯托管服务正在停止");

            if (_managerCts != null)
            {
                _managerCts.Cancel();
            }

            if (_managerTask != null)
            {
                await Task.WhenAny(_managerTask, Task.Delay(5000, cancellationToken));
            }

            await StopPlcCommunicationAsync();
            _logger.LogInformation("PLC 通讯托管服务已停止");
        }

        /// <summary>
        /// 持续观察设置开关，按需启动或停止 PLC 通讯服务。
        /// </summary>
        private async Task ManageServiceStateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 通讯状态管理循环已启动");
            await _serviceToggleService.EnsureDefaultSettingsAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var enabled = await _serviceToggleService.IsEnabledAsync(
                        ServiceSettingKeys.PlcCommunicationEnabled,
                        true,
                        cancellationToken);

                    if (enabled && !_serviceStarted)
                    {
                        await StartPlcCommunicationAsync(cancellationToken);
                    }
                    else if (!enabled && _serviceStarted)
                    {
                        await StopPlcCommunicationAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PLC 通讯状态管理循环发生异常");
                }

                await Task.Delay(_settingCheckInterval, cancellationToken);
            }

            _logger.LogInformation("PLC 通讯状态管理循环已停止");
        }

        /// <summary>
        /// 启动底层 PLC 通讯服务，并拉起状态监控任务。
        /// </summary>
        private async Task StartPlcCommunicationAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("检测到 PLC 通讯开关已开启，准备启动底层通讯服务");

                try
                {
                    await _plcCommunicationService.ResetServiceLockAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "重置 PLC 通讯服务锁状态失败，将继续尝试启动");
                }

                await _plcCommunicationService.StartServiceAsync();

                _monitorCts?.Cancel();
                _monitorCts = new CancellationTokenSource();
                _monitorTask = MonitorConnectionStatusAsync(_monitorCts.Token);
                _serviceStarted = true;

                _logger.LogInformation("PLC 通讯服务已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动 PLC 通讯服务失败");
                _serviceStarted = false;
                _lastServiceRestartTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 停止底层 PLC 通讯服务。
        /// 关闭开关后会走这条路径，从源头上停止不断报错的后台通讯逻辑。
        /// </summary>
        private async Task StopPlcCommunicationAsync()
        {
            if (!_serviceStarted && _monitorCts == null)
            {
                return;
            }

            try
            {
                if (_monitorCts != null)
                {
                    _monitorCts.Cancel();
                }

                if (_monitorTask != null)
                {
                    await Task.WhenAny(_monitorTask, Task.Delay(5000));
                }

                await _plcCommunicationService.StopServiceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止 PLC 通讯服务失败");
            }
            finally
            {
                _serviceStarted = false;
            }
        }

        /// <summary>
        /// 监控 PLC 连接状态。
        /// 仅当通讯服务本身处于启用状态时，该循环才会持续运行。
        /// </summary>
        private async Task MonitorConnectionStatusAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 连接状态监控任务已启动");
            var deviceErrorCounts = new Dictionary<int, int>();
            const int maxErrorsBeforeRestart = 3;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    try
                    {
                        var statusDict = await _plcCommunicationService.GetServiceStatusAsync().WaitAsync(linkedCts.Token);
                        if (statusDict == null || statusDict.Count == 0)
                        {
                            await Task.Delay(_monitorInterval, cancellationToken);
                            continue;
                        }

                        var offlineDevices = statusDict.Where(kv => !kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                        if (!offlineDevices.Any())
                        {
                            deviceErrorCounts.Clear();
                            await Task.Delay(_monitorInterval, cancellationToken);
                            continue;
                        }

                        _logger.LogWarning("检测到 {Count} 个 PLC 设备离线", offlineDevices.Count);
                        var failedResetDevices = new List<int>();
                        var deviceNeedingReset = 0;

                        foreach (var deviceId in offlineDevices.Keys)
                        {
                            if (!deviceErrorCounts.ContainsKey(deviceId))
                            {
                                deviceErrorCounts[deviceId] = 0;
                            }

                            deviceErrorCounts[deviceId]++;
                            if (deviceErrorCounts[deviceId] < maxErrorsBeforeRestart)
                            {
                                continue;
                            }

                            deviceNeedingReset++;
                            try
                            {
                                await _plcCommunicationService.ManualReadSignalsAsync(deviceId);
                                _logger.LogInformation("设备 {DeviceId} 的 PLC 连接已重置", deviceId);
                                deviceErrorCounts[deviceId] = 0;
                            }
                            catch (Exception resetEx)
                            {
                                _logger.LogError(resetEx, "重置设备 {DeviceId} 的 PLC 连接失败", deviceId);
                                failedResetDevices.Add(deviceId);
                            }
                        }

                        var totalDevices = statusDict.Count;
                        var needServiceRestart = failedResetDevices.Count > 0 &&
                                                 failedResetDevices.Count >= Math.Max(2, totalDevices / 2);

                        if (needServiceRestart && ShouldRestartService())
                        {
                            try
                            {
                                _logger.LogWarning(
                                    "检测到严重 PLC 连接问题（{FailedCount}/{TotalCount} 台设备重置失败），准备重启 PLC 通讯服务",
                                    failedResetDevices.Count,
                                    totalDevices);

                                await _plcCommunicationService.RestartServiceAsync();
                                deviceErrorCounts.Clear();
                                _lastServiceRestartTime = DateTime.Now;
                                _logger.LogInformation("PLC 通讯服务重启成功");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "重启 PLC 通讯服务失败");
                            }
                        }
                        else if (deviceNeedingReset > 0 && failedResetDevices.Count == 0)
                        {
                            _logger.LogInformation("已成功重置 {Count} 台 PLC 设备，无需重启整个服务", deviceNeedingReset);
                        }
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogWarning("获取 PLC 设备状态超时，跳过本轮检查");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "监控 PLC 连接状态时发生错误");
                }

                try
                {
                    await Task.Delay(_monitorInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("PLC 连接状态监控任务已停止");
        }

        /// <summary>
        /// 判断是否满足再次重启整个 PLC 通讯服务的最小时间间隔。
        /// </summary>
        private bool ShouldRestartService()
        {
            return (DateTime.Now - _lastServiceRestartTime) > _minServiceRestartInterval;
        }

        public void Dispose()
        {
            _managerCts?.Dispose();
            _monitorCts?.Dispose();
        }
    }
}
