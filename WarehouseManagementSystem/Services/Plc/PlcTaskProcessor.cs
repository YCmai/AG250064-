using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client100.Entity;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.PLC;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Service.Plc
{
    /// <summary>
    /// PLC 任务处理器。
    /// 负责从 RCS_AutoPlcTasks 表中轮询待发送任务，
    /// 按“设备 IP -> DB 块 -> 创建时间”的顺序串行写入 PLC，
    /// 成功后再把任务状态回写到数据库。
    /// </summary>
    public class PlcTaskProcessor : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PlcTaskProcessor> _logger;
        private readonly IServiceToggleService _serviceToggleService;

        /// <summary>
        /// 后台循环使用的取消令牌。
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// 记录每个 PLC 设备 IP 当前是否已有任务批次在执行。
        /// 同一台设备不允许并行写入，避免 PLC 写入顺序被打乱。
        /// </summary>
        private readonly ConcurrentDictionary<string, Task> _ipProcessingTasks = new();

        /// <summary>
        /// 信号 ID 本地缓存，减少每条任务都重复查 RCS_PlcSignal 的开销。
        /// Key 格式：IP|DB块|信号名称。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _signalIdCache = new();

        private Task? _processingTask;

        /// <summary>
        /// 无任务时的轮询间隔。
        /// </summary>
        private const int ProcessingIntervalMilliseconds = 200;

        /// <summary>
        /// 出现异常后的退避等待时间。
        /// </summary>
        private const int ErrorRetryIntervalMilliseconds = 1000;

        /// <summary>
        /// PLC 总开关关闭时的待机检查间隔。
        /// </summary>
        private const int DisabledRetryIntervalMilliseconds = 1000;

        /// <summary>
        /// 每次从数据库最多抓取的待处理任务数量。
        /// </summary>
        private const int MaxTasksPerBatch = 50;

        /// <summary>
        /// 过期任务清理间隔。
        /// </summary>
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);

        public PlcTaskProcessor(
            IServiceProvider serviceProvider,
            ILogger<PlcTaskProcessor> logger,
            IServiceToggleService serviceToggleService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _serviceToggleService = serviceToggleService;
        }

        /// <summary>
        /// 启动 PLC 任务后台轮询。
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 任务处理器正在启动");
            _processingTask = ProcessTasksAsync();
            _logger.LogInformation("PLC 任务处理器已启动");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止后台轮询，并等待当前任务尽量优雅退出。
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PLC 任务处理器正在停止");
            _cts.Cancel();

            if (_processingTask != null)
            {
                await Task.WhenAny(_processingTask, Task.Delay(5000, cancellationToken));
            }

            _logger.LogInformation("PLC 任务处理器已停止");
        }

        /// <summary>
        /// 主轮询循环。
        /// 整体流程如下：
        /// 1. 检查 PLC 服务总开关。
        /// 2. 定时清理已发送的过期任务。
        /// 3. 抓取一批待处理任务。
        /// 4. 按设备 IP 分发，同一 IP 只允许一个执行批次。
        /// </summary>
        private async Task ProcessTasksAsync()
        {
            await _serviceToggleService.EnsureDefaultSettingsAsync(_cts.Token);
            _logger.LogInformation("PLC 任务处理循环已启动");

            var lastCleanupTime = DateTime.MinValue;

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!await IsPlcServiceEnabledAsync())
                    {
                        await Task.Delay(DisabledRetryIntervalMilliseconds, _cts.Token);
                        continue;
                    }

                    if (DateTime.Now - lastCleanupTime >= CleanupInterval)
                    {
                        using var cleanupScope = _serviceProvider.CreateScope();
                        var cleanupDbService = cleanupScope.ServiceProvider.GetRequiredService<IDatabaseService>();
                        await CleanupExpiredTasksAsync(cleanupDbService);
                        lastCleanupTime = DateTime.Now;
                    }

                    List<AutoPlcTaskWithIp> pendingTasks;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                        pendingTasks = await GetPendingTasksWithIpAsync(dbService);
                    }

                    if (!pendingTasks.Any())
                    {
                        await Task.Delay(ProcessingIntervalMilliseconds, _cts.Token);
                        continue;
                    }

                    var tasksByIp = pendingTasks
                        .Where(task => !string.IsNullOrWhiteSpace(task.PlcType))
                        .GroupBy(task => task.PlcType)
                        .ToList();

                    foreach (var ipGroup in tasksByIp)
                    {
                        var ip = ipGroup.Key;

                        // 同一台 PLC 如果已有批次正在执行，就让当前轮询跳过，等待下一轮再捞。
                        if (_ipProcessingTasks.TryGetValue(ip, out var runningTask) && !runningTask.IsCompleted)
                        {
                            continue;
                        }

                        var deviceTasks = ipGroup.ToList();
                        var processingTask = Task.Run(async () =>
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                            var plcService = scope.ServiceProvider.GetRequiredService<IPlcCommunicationService>();
                            await ProcessIpTasksAsync(ip, deviceTasks, dbService, plcService);
                        }, _cts.Token);

                        _ipProcessingTasks[ip] = processingTask.ContinueWith(task =>
                        {
                            _ipProcessingTasks.TryRemove(ip, out _);

                            if (task.IsFaulted && task.Exception != null)
                            {
                                _logger.LogError(task.Exception.InnerException ?? task.Exception, "处理 IP {IpAddress} 的 PLC 任务时发生异常", ip);
                            }
                        }, TaskContinuationOptions.ExecuteSynchronously);
                    }

                    await Task.Delay(ProcessingIntervalMilliseconds, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PLC 任务处理循环发生错误");
                    await Task.Delay(ErrorRetryIntervalMilliseconds, _cts.Token);
                }
            }

            _logger.LogInformation("PLC 任务处理循环已停止");
        }

        /// <summary>
        /// 处理单个设备 IP 下的任务批次。
        /// 同一设备内再按 DB 块分组，并按创建时间顺序逐条执行，
        /// 保证设备写入的先后顺序稳定。
        /// </summary>
        private async Task ProcessIpTasksAsync(
            string ip,
            List<AutoPlcTaskWithIp> ipTasks,
            IDatabaseService dbService,
            IPlcCommunicationService plcService)
        {
            try
            {
                if (!await IsPlcServiceEnabledAsync())
                {
                    _logger.LogInformation("PLC 服务已关闭，停止处理设备 {IpAddress} 的任务批次", ip);
                    return;
                }

                var tasksByDbBlock = ipTasks
                    .GroupBy(task => task.PLCTypeDb ?? string.Empty)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderBy(task => task.CreatingTime).Cast<AutoPlcTask>().ToList());

                foreach (var group in tasksByDbBlock)
                {
                    if (!await IsPlcServiceEnabledAsync())
                    {
                        _logger.LogInformation("PLC 服务已关闭，停止处理设备 {IpAddress} 的剩余任务组", ip);
                        return;
                    }

                    var plcTypeDb = group.Key;
                    var orderedTasks = group.Value;

                    if (orderedTasks.Count > 0)
                    {
                        _logger.LogInformation(
                            "开始处理 PLC 任务组：Ip={Ip}, PLCTypeDb={PLCTypeDb}, Count={Count}",
                            ip,
                            plcTypeDb,
                            orderedTasks.Count);
                    }

                    foreach (var task in orderedTasks)
                    {
                        if (!await IsPlcServiceEnabledAsync())
                        {
                            _logger.LogInformation("PLC 服务已关闭，停止处理设备 {IpAddress} 的剩余任务", ip);
                            return;
                        }

                        await ProcessSingleTaskAsync(dbService, plcService, ip, plcTypeDb, task);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 IP {IpAddress} 的 PLC 任务组时发生错误", ip);
                throw;
            }
        }

        /// <summary>
        /// 处理单条 PLC 任务。
        /// 这里把“查设备 -> 查信号 -> 写 PLC -> 回写任务状态”收在一个方法里，
        /// 这样阅读主链路时不需要在多个小方法之间频繁跳转。
        /// </summary>
        private async Task ProcessSingleTaskAsync(
            IDatabaseService dbService,
            IPlcCommunicationService plcService,
            string plcType,
            string plcTypeDb,
            AutoPlcTask task)
        {
            if (!await IsPlcServiceEnabledAsync())
            {
                return;
            }

            var taskStart = DateTime.Now;
            _logger.LogInformation(
                "开始处理 PLC 任务：Ip={PlcType}, DB={PLCTypeDb}, Signal={Signal}, Status={Status}, TaskId={Id}",
                plcType,
                plcTypeDb,
                task.Signal,
                task.Status,
                task.Id);

            try
            {
                using var conn = dbService.CreateConnection();

                // 第一步：根据设备 IP 查对应的 PLC 设备主键。
                var deviceId = await conn.ExecuteScalarAsync<int?>(@"
                    SELECT Id
                    FROM RCS_PlcDevice
                    WHERE IpAddress = @IpAddress",
                    new { IpAddress = plcType });

                if (!deviceId.HasValue || deviceId.Value <= 0)
                {
                    _logger.LogWarning("未找到 PlcType={PlcType} 对应的设备，跳过任务 {TaskId}", plcType, task.Id);
                    return;
                }

                // 第二步：根据设备、DB 块和信号名称找到信号定义。
                var signalId = await GetSignalIdAsync(dbService, plcType, plcTypeDb, task.Signal);
                if (signalId <= 0)
                {
                    _logger.LogWarning("未找到信号定义：Ip={PlcType}, DB={PLCTypeDb}, Signal={Signal}", plcType, plcTypeDb, task.Signal);
                    return;
                }

                // 第三步：根据任务类型执行真正的 PLC 写入动作。
                await WriteTaskValueAsync(plcService, task, deviceId.Value, signalId);

                // 第四步：写入成功后立刻把任务标记为已发送，防止重复执行。
                await conn.ExecuteAsync(@"
                    UPDATE RCS_AutoPlcTasks
                    SET IsSend = 1,
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id",
                    new
                    {
                        Id = task.Id,
                        UpdateTime = DateTime.Now
                    });

                _logger.LogInformation(
                    "完成 PLC 任务：Ip={PlcType}, DB={PLCTypeDb}, Signal={Signal}, TaskId={Id}, 耗时={Duration}ms",
                    plcType,
                    plcTypeDb,
                    task.Signal,
                    task.Id,
                    (DateTime.Now - taskStart).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行 PLC 任务 {TaskId} 失败", task.Id);
                throw;
            }
        }

        /// <summary>
        /// 根据设备 IP、DB 块和信号名称获取信号 ID，并缓存查询结果。
        /// 这个方法保留独立存在，是因为它具备明显的缓存职责，
        /// 从主执行链路中抽离出来会更清晰。
        /// </summary>
        private async Task<int> GetSignalIdAsync(IDatabaseService dbService, string ipAddress, string plcTypeDb, string signalName)
        {
            var cacheKey = $"{ipAddress}|{plcTypeDb}|{signalName}";
            if (_signalIdCache.TryGetValue(cacheKey, out var cachedId))
            {
                return cachedId;
            }

            using var conn = dbService.CreateConnection();
            var signalId = await conn.ExecuteScalarAsync<int?>(@"
                SELECT Id
                FROM RCS_PlcSignal
                WHERE PlcDeviceId = @IpAddress
                  AND PLCTypeDb = @PLCTypeDb
                  AND Name = @SignalName",
                new
                {
                    IpAddress = ipAddress,
                    PLCTypeDb = plcTypeDb,
                    SignalName = signalName
                });

            if (!signalId.HasValue || signalId.Value <= 0)
            {
                return 0;
            }

            _signalIdCache[cacheKey] = signalId.Value;
            return signalId.Value;
        }

        /// <summary>
        /// 根据任务状态码执行实际的 PLC 写入。
        /// 当前状态码的含义是：
        /// 1=写入 bool true
        /// 2=写入 bool false
        /// 3=写入整数
        /// 4=整数清零
        /// 5=写入字符串
        /// 6=清空字符串
        /// </summary>
        private async Task WriteTaskValueAsync(IPlcCommunicationService plcService, AutoPlcTask task, int deviceId, int signalId)
        {
            switch (task.Status)
            {
                case 1:
                    await plcService.WriteSignalValueAsync(deviceId, signalId, true);
                    break;
                case 2:
                    await plcService.WriteSignalValueAsync(deviceId, signalId, false);
                    break;
                case 3:
                    // 注意：这里沿用现有业务含义，状态 3 时仍然从 task.Signal 中解析整数值。
                    // 这在设计上有歧义，但属于既有业务规则，本次重构不改行为。
                    if (!int.TryParse(task.Signal, out var intValue))
                    {
                        throw new InvalidOperationException($"无法将信号值 '{task.Signal}' 解析为整数");
                    }
                    await plcService.WriteSignalValueAsync(deviceId, signalId, intValue);
                    break;
                case 4:
                    await plcService.WriteSignalValueAsync(deviceId, signalId, 0);
                    break;
                case 5:
                    await plcService.WriteSignalValueAsync(deviceId, signalId, task.Remark ?? string.Empty);
                    break;
                case 6:
                    await plcService.WriteSignalValueAsync(deviceId, signalId, string.Empty);
                    break;
                default:
                    throw new NotSupportedException($"不支持的 PLC 任务类型: {task.Status}");
            }
        }

        /// <summary>
        /// 清理已发送且超过 1 天的旧任务，防止任务表持续膨胀。
        /// </summary>
        private async Task CleanupExpiredTasksAsync(IDatabaseService dbService)
        {
            try
            {
                using var conn = dbService.CreateConnection();
                var deletedCount = await conn.ExecuteAsync(@"
                    DELETE FROM RCS_AutoPlcTasks
                    WHERE CreatingTime < @ExpirationDate
                      AND IsSend = 1",
                    new { ExpirationDate = DateTime.Now.AddDays(-1) });

                if (deletedCount > 0)
                {
                    _logger.LogInformation("已清理 {DeletedCount} 条过期 PLC 任务", deletedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期 PLC 任务失败");
            }
        }

        /// <summary>
        /// 查询一批待发送任务。
        /// 当前只看 IsSend = 0 的记录，并按创建时间升序处理最早的任务。
        /// </summary>
        private async Task<List<AutoPlcTaskWithIp>> GetPendingTasksWithIpAsync(IDatabaseService dbService)
        {
            using var conn = dbService.CreateConnection();
            var tasks = await conn.QueryAsync<AutoPlcTaskWithIp>(@"
                SELECT TOP (@TakeCount) t.*, d.IpAddress
                FROM RCS_AutoPlcTasks t
                LEFT JOIN RCS_PlcDevice d ON t.PlcType = d.IpAddress AND t.PLCTypeDb = d.ModuleAddress
                WHERE t.IsSend = 0
                ORDER BY t.CreatingTime ASC",
                new { TakeCount = MaxTasksPerBatch });

            return tasks.ToList();
        }

        /// <summary>
        /// 统一判断 PLC 总开关是否启用。
        /// 在主循环、设备批次、单任务执行时都会重复检查，
        /// 这样用户关闭服务开关后可以尽快停下来。
        /// </summary>
        private Task<bool> IsPlcServiceEnabledAsync()
        {
            return _serviceToggleService.IsEnabledAsync(
                ServiceSettingKeys.PlcCommunicationEnabled,
                true,
                _cts.Token);
        }

        /// <summary>
        /// 为任务查询结果补充设备 IP 信息。
        /// 继承 AutoPlcTask 是为了复用现有任务字段，避免重复映射。
        /// </summary>
        private sealed class AutoPlcTaskWithIp : AutoPlcTask
        {
            public string IpAddress { get; set; } = string.Empty;
        }
    }
}


