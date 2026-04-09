using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.PLC;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Service.Plc
{
    /// <summary>
    /// 心跳服务。
    /// 用于按固定周期向 PLC 心跳信号写入交替值，并支持根据 PLC 总开关动态启停。
    /// </summary>
    public class HeartbeatService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HeartbeatService> _logger;
        private readonly IServiceToggleService _serviceToggleService;
        private readonly CancellationTokenSource _cts = new();
        private Task? _heartbeatTask;
        private bool _currentHeartbeatState = true;
        private readonly Dictionary<string, DateTime> _lastHeartbeatTime = new();
        private const int HeartbeatIntervalSeconds = 1;
        private readonly IPlcCommunicationService _heartbeatPlcService;

        public HeartbeatService(
            IServiceProvider serviceProvider,
            ILogger<HeartbeatService> logger,
            IServiceToggleService serviceToggleService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _serviceToggleService = serviceToggleService;

            // 单独创建一个心跳专用的 PLC 通讯服务实例，避免和主通讯链路互相影响。
            var plcSignalService = serviceProvider.GetRequiredService<IPlcSignalService>();
            var plcSignalUpdater = serviceProvider.GetRequiredService<PlcSignalUpdater>();
            var plcLogger = serviceProvider.GetRequiredService<ILogger<PlcCommunicationService>>();
            var dbService = serviceProvider.GetRequiredService<IDatabaseService>();

            _heartbeatPlcService = new PlcCommunicationService(
                plcSignalService,
                plcSignalUpdater,
                plcLogger,
                dbService);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("心跳服务正在启动");
            _heartbeatTask = ProcessHeartbeatsAsync();
            _logger.LogInformation("心跳服务已启动");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("心跳服务正在停止");
            _cts.Cancel();

            if (_heartbeatTask != null)
            {
                await Task.WhenAny(_heartbeatTask, Task.Delay(5000, cancellationToken));
            }

            _logger.LogInformation("心跳服务已停止");
        }

        private async Task ProcessHeartbeatsAsync()
        {
            await _serviceToggleService.EnsureDefaultSettingsAsync(_cts.Token);
            _logger.LogInformation("心跳处理循环已启动");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var enabled = await _serviceToggleService.IsEnabledAsync(
                        ServiceSettingKeys.PlcCommunicationEnabled,
                        true,
                        _cts.Token);

                    if (!enabled)
                    {
                        await Task.Delay(1000, _cts.Token);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                    var deviceSignals = await GetHeartbeatDeviceSignalsAsync(dbService);
                    var now = DateTime.Now;

                    foreach (var (device, signal) in deviceSignals)
                    {
                        try
                        {
                            var deviceKey = $"{device.IpAddress}_{device.ModuleAddress}";
                            if (_lastHeartbeatTime.TryGetValue(deviceKey, out var lastTime) &&
                                (now - lastTime).TotalSeconds < HeartbeatIntervalSeconds)
                            {
                                continue;
                            }

                            await _heartbeatPlcService.WriteSignalHeatValueAsync(device.Id, signal.Id, _currentHeartbeatState);
                            _lastHeartbeatTime[deviceKey] = now;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "处理设备 {IpAddress} 的心跳信号时发生错误", device.IpAddress);
                        }
                    }

                    _currentHeartbeatState = !_currentHeartbeatState;
                    await Task.Delay(HeartbeatIntervalSeconds * 1000, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "心跳处理循环发生错误");
                    await Task.Delay(1000, _cts.Token);
                }
            }
        }

        private async Task<List<(RCS_PlcDevice Device, RCS_PlcSignal Signal)>> GetHeartbeatDeviceSignalsAsync(IDatabaseService dbService)
        {
            using var conn = dbService.CreateConnection();
            var result = new List<(RCS_PlcDevice, RCS_PlcSignal)>();

            foreach (var config in HeartbeatConfig.Devices)
            {
                var device = await conn.QueryFirstOrDefaultAsync<RCS_PlcDevice>(@"
                    SELECT * FROM RCS_PlcDevice
                    WHERE IsEnabled = 1
                      AND IpAddress = @IpAddress
                      AND ModuleAddress = @ModuleAddress",
                    new { config.IpAddress, config.ModuleAddress });

                if (device == null)
                {
                    continue;
                }

                var signal = await conn.QueryFirstOrDefaultAsync<RCS_PlcSignal>(@"
                    SELECT * FROM RCS_PlcSignal
                    WHERE PlcDeviceId = @PlcDeviceId
                      AND PLCTypeDb = @PLCTypeDb
                      AND Remark = '进站心跳'",
                    new
                    {
                        PlcDeviceId = device.IpAddress,
                        PLCTypeDb = config.ModuleAddress
                    });

                if (signal != null)
                {
                    result.Add((device, signal));
                }
            }

            return result;
        }
    }
}
