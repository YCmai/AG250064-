using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.IO;

namespace WarehouseManagementSystem.Service.Io
{
    public class IOAGVTaskProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDatabaseService _db;
        private readonly ILogger<IOAGVTaskProcessor> _logger;
        private readonly IIOService _ioService;

        // 每台设备只允许一个调用链进入，确保所有 IO 访问严格串行。
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks = new();

        // 写入后验证不要使用过长的固定等待，而是用更短的间隔做有限次确认。
        private static readonly TimeSpan _signalVerifyInterval = TimeSpan.FromMilliseconds(80);
        private const int SignalVerifyAttempts = 5;

        public IOAGVTaskProcessor(
            IServiceProvider serviceProvider,
            IDatabaseService db,
            IIOService ioService,
            ILogger<IOAGVTaskProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _db = db;
            _ioService = ioService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 实际的设备处理由 IOProcessorService 按设备维度驱动。
            // 这里仅保留一个轻量空循环，满足 BackgroundService 生命周期要求。
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// 获取所有启用中的 IO 设备 IP。
        /// </summary>
        public async Task<List<string>> GetAllDeviceIpsAsync()
        {
            try
            {
                using var conn = _db.CreateConnection();
                var devices = await conn.QueryAsync<RCS_IODevices>(
                    "SELECT IP FROM RCS_IODevices WHERE IsEnabled = 1");

                return devices
                    .Select(d => d.IP)
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 IO 设备 IP 列表失败");
                return new List<string>();
            }
        }

        /// <summary>
        /// 刷新单台设备的信号缓存表。
        /// 该方法只负责监控缓存，不参与任务判定，因此在有待处理任务时应尽量少执行。
        /// </summary>
        public async Task UpdateIOSignalsForDevice(string deviceIp, CancellationToken cancellationToken = default)
        {
            var deviceLock = _deviceLocks.GetOrAdd(deviceIp, _ => new SemaphoreSlim(1, 1));
            await deviceLock.WaitAsync(cancellationToken);

            try
            {
                using var conn = _db.CreateConnection();
                var device = await GetDeviceByIpAsync(conn, deviceIp);
                if (device == null)
                {
                    return;
                }

                // 如果当前设备仍有待处理任务，优先交给任务链路处理，避免监控刷新抢占设备访问机会。
                var pendingTaskCount = await conn.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1)
                    FROM RCS_IOAGV_Tasks
                    WHERE Status = 'Pending' AND DeviceIP = @DeviceIP",
                    new { DeviceIP = deviceIp });

                if (pendingTaskCount > 0)
                {
                    return;
                }

                var signals = (await conn.QueryAsync<RCS_IOSignals>(@"
                    SELECT Id, DeviceId, Name, Address, Value, UpdatedTime
                    FROM RCS_IOSignals
                    WHERE DeviceId = @DeviceId",
                    new { DeviceId = device.Id })).ToList();

                foreach (var signal in signals)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!Enum.TryParse<EIOAddress>(signal.Address, out var addressEnum))
                        {
                            _logger.LogWarning("设备 {DeviceIP} 存在无效信号地址：{Address}", deviceIp, signal.Address);
                            continue;
                        }

                        var latestValue = await _ioService.ReadSignal(device.IP, addressEnum);
                        var latestValueInt = latestValue ? 1 : 0;

                        // 只有信号值变化时才更新数据库，减少无意义的写入压力。
                        if (signal.Value == latestValueInt)
                        {
                            continue;
                        }

                        signal.Value = latestValueInt;
                        signal.UpdatedTime = DateTime.Now;

                        await conn.ExecuteAsync(@"
                            UPDATE RCS_IOSignals
                            SET Value = @Value,
                                UpdatedTime = @UpdatedTime
                            WHERE Id = @Id",
                            new { signal.Value, signal.UpdatedTime, signal.Id });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "刷新设备 {DeviceName}({DeviceIP}) 的信号 {Address} 失败",
                            device.Name,
                            device.IP,
                            signal.Address);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新设备 {DeviceIP} 的 IO 信号失败", deviceIp);
            }
            finally
            {
                deviceLock.Release();
            }
        }

        /// <summary>
        /// 处理指定设备的待执行任务。
        /// 返回值表示当前轮是否发现了待处理任务，供上层决定是否马上继续下一轮。
        /// </summary>
        public async Task<bool> ProcessTasksForDevice(string deviceIp, CancellationToken cancellationToken = default)
        {
            var deviceLock = _deviceLocks.GetOrAdd(deviceIp, _ => new SemaphoreSlim(1, 1));
            await deviceLock.WaitAsync(cancellationToken);

            try
            {
                using var conn = _db.CreateConnection();
                var taskList = (await conn.QueryAsync<RCS_IOAGV_Tasks>(@"
                    SELECT Id,
                           TaskType,
                           Status,
                           DeviceIP,
                           SignalAddress,
                           CAST(CreatedTime AS DATETIME) AS CreatedTime,
                           CAST(CompletedTime AS DATETIME) AS CompletedTime,
                           CAST(LastUpdatedTime AS DATETIME) AS LastUpdatedTime,
                           TaskId,
                           Value
                    FROM RCS_IOAGV_Tasks
                    WHERE Status = 'Pending' AND DeviceIP = @DeviceIP
                    ORDER BY CreatedTime ASC",
                    new { DeviceIP = deviceIp })).ToList();

                if (!taskList.Any())
                {
                    return false;
                }

                _logger.LogInformation("开始处理设备 {DeviceIP} 的任务队列，共 {Count} 条", deviceIp, taskList.Count);

                foreach (var task in taskList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!Enum.TryParse<EIOAddress>(task.SignalAddress, out var addressEnum))
                        {
                            _logger.LogWarning(
                                "任务 {TaskId} 的信号地址无效：DeviceIP={DeviceIP}, Address={Address}",
                                task.Id,
                                task.DeviceIP,
                                task.SignalAddress);
                            continue;
                        }

                        var success = await ExecuteTaskAsync(conn, task, addressEnum, cancellationToken);
                        if (!success)
                        {
                            _logger.LogWarning(
                                "任务处理失败，停止继续执行该设备后续任务：TaskId={TaskId}, Type={TaskType}, Device={DeviceIP}, Address={Address}",
                                task.Id,
                                task.TaskType,
                                task.DeviceIP,
                                task.SignalAddress);
                            break;
                        }

                        await UpdateTaskStatus(conn, task.Id, true);
                        _logger.LogInformation(
                            "任务处理成功：TaskId={TaskId}, Type={TaskType}, Device={DeviceIP}, Address={Address}, 排队耗时={Duration}ms",
                            task.Id,
                            task.TaskType,
                            task.DeviceIP,
                            task.SignalAddress,
                            (DateTime.Now - task.CreatedTime).TotalMilliseconds);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "处理任务时发生异常，停止继续执行该设备后续任务：TaskId={TaskId}, Type={TaskType}, Device={DeviceIP}, Address={Address}",
                            task.Id,
                            task.TaskType,
                            task.DeviceIP,
                            task.SignalAddress);
                        break;
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理设备 {DeviceIP} 的 IO 任务失败", deviceIp);
                return false;
            }
            finally
            {
                deviceLock.Release();
            }
        }

        /// <summary>
        /// 保留原有的公共入口，供其他调用方按批触发任务处理。
        /// 实际串行控制仍然由设备级锁保证。
        /// </summary>
        public async Task ProcessTasks()
        {
            try
            {
                var deviceIps = await GetAllDeviceIpsAsync();
                var tasks = deviceIps.Select(ip => ProcessTasksForDevice(ip));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量处理 IO 任务失败");
            }
        }

        /// <summary>
        /// 保留原有的公共入口，供其他调用方按批刷新监控缓存。
        /// </summary>
        public async Task UpdateIOSignals()
        {
            try
            {
                var deviceIps = await GetAllDeviceIpsAsync();
                var tasks = deviceIps.Select(ip => UpdateIOSignalsForDevice(ip));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量刷新 IO 信号失败");
            }
        }

        /// <summary>
        /// 执行单条任务。
        /// ArrivalNotify / PassComplete 会确保设备信号被写到目标值；
        /// PassCheck 则直接读设备当前值，不再经过数据库缓存表。
        /// </summary>
        private async Task<bool> ExecuteTaskAsync(
            IDbConnection conn,
            RCS_IOAGV_Tasks task,
            EIOAddress address,
            CancellationToken cancellationToken)
        {
            bool success;

            switch (task.TaskType)
            {
                case "ArrivalNotify":
                case "PassComplete":
                    success = await EnsureSignalStateAsync(task.DeviceIP, address, task.Value, cancellationToken);
                    break;
                case "PassCheck":
                    success = await VerifySignalByDeviceAsync(task.DeviceIP, address, task.Value, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("发现未知任务类型：TaskId={TaskId}, TaskType={TaskType}", task.Id, task.TaskType);
                    success = false;
                    break;
            }

            if (success)
            {
                // 任务成功后顺手同步一次缓存表，保证监控界面和其他读缓存的逻辑尽快看到最新值。
                await UpdateSignalCacheAsync(conn, task.DeviceIP, task.SignalAddress, task.Value ? 1 : 0);
            }

            return success;
        }

        /// <summary>
        /// 确保设备信号达到指定状态。
        /// 如果当前值已经正确，则做短周期稳定性确认；如果不正确，则先写入再快速验证。
        /// </summary>
        private async Task<bool> EnsureSignalStateAsync(
            string deviceIp,
            EIOAddress address,
            bool expectedValue,
            CancellationToken cancellationToken)
        {
            var currentValue = await _ioService.ReadSignal(deviceIp, address);
            if (currentValue == expectedValue)
            {
                return await WaitForExpectedSignalAsync(deviceIp, address, expectedValue, cancellationToken);
            }

            var writeSuccess = await _ioService.WriteSignal(deviceIp, address, expectedValue);
            if (!writeSuccess)
            {
                return false;
            }

            return await WaitForExpectedSignalAsync(deviceIp, address, expectedValue, cancellationToken);
        }

        /// <summary>
        /// 直接读取设备当前值并做有限次快速确认。
        /// 这条路径用于替代原先通过数据库缓存表判断的做法，减少一个完整轮询周期的延迟。
        /// </summary>
        private async Task<bool> VerifySignalByDeviceAsync(
            string deviceIp,
            EIOAddress address,
            bool expectedValue,
            CancellationToken cancellationToken)
        {
            return await WaitForExpectedSignalAsync(deviceIp, address, expectedValue, cancellationToken);
        }

        /// <summary>
        /// 使用短周期、有限次数的读取来确认信号值。
        /// 这样比固定睡眠 300ms 更适合实时交互，能够在设备响应快时尽早返回。
        /// </summary>
        private async Task<bool> WaitForExpectedSignalAsync(
            string deviceIp,
            EIOAddress address,
            bool expectedValue,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < SignalVerifyAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentValue = await _ioService.ReadSignal(deviceIp, address);
                if (currentValue == expectedValue)
                {
                    return true;
                }

                if (attempt < SignalVerifyAttempts - 1)
                {
                    await Task.Delay(_signalVerifyInterval, cancellationToken);
                }
            }

            return false;
        }

        /// <summary>
        /// 同步单个信号到缓存表，避免任务成功后还要等下一轮监控刷新才能看到结果。
        /// </summary>
        private async Task UpdateSignalCacheAsync(IDbConnection conn, string deviceIp, string signalAddress, int signalValue)
        {
            await conn.ExecuteAsync(@"
                UPDATE s
                SET s.Value = @Value,
                    s.UpdatedTime = @UpdatedTime
                FROM RCS_IOSignals s
                INNER JOIN RCS_IODevices d ON d.Id = s.DeviceId
                WHERE d.IP = @DeviceIP
                  AND d.IsEnabled = 1
                  AND s.Address = @Address",
                new
                {
                    Value = signalValue,
                    UpdatedTime = DateTime.Now,
                    DeviceIP = deviceIp,
                    Address = signalAddress
                });
        }

        /// <summary>
        /// 按 IP 获取设备基础信息。
        /// </summary>
        private async Task<RCS_IODevices?> GetDeviceByIpAsync(IDbConnection conn, string deviceIp)
        {
            return await conn.QueryFirstOrDefaultAsync<RCS_IODevices>(@"
                SELECT Id, Name, IP
                FROM RCS_IODevices
                WHERE IP = @IP AND IsEnabled = 1",
                new { IP = deviceIp });
        }

        /// <summary>
        /// 更新任务状态。
        /// 当前重构阶段仍然保留原有表结构和状态字段，尽量减少对外部系统的影响。
        /// </summary>
        private async Task UpdateTaskStatus(IDbConnection conn, int taskId, bool isCompleted)
        {
            await conn.ExecuteAsync(@"
                UPDATE RCS_IOAGV_Tasks
                SET Status = @Status,
                    CompletedTime = @CompletedTime,
                    LastUpdatedTime = @LastUpdatedTime
                WHERE Id = @Id",
                new
                {
                    Id = taskId,
                    Status = isCompleted ? "Completed" : "Pending",
                    CompletedTime = isCompleted ? DateTime.Now : (DateTime?)null,
                    LastUpdatedTime = DateTime.Now
                });
        }

        /// <summary>
        /// AGV 与 IO 交互任务实体。
        /// </summary>
        public class RCS_IOAGV_Tasks
        {
            public int Id { get; set; }

            /// <summary>
            /// 任务类型：ArrivalNotify（到达通知）、PassCheck（通行检测）、PassComplete（通行完成）。
            /// </summary>
            public string TaskType { get; set; }

            /// <summary>
            /// 任务状态：Pending（待处理）、Completed（已完成）。
            /// </summary>
            public string Status { get; set; }

            /// <summary>
            /// IO 设备 IP 地址。
            /// </summary>
            public string DeviceIP { get; set; }

            /// <summary>
            /// 信号地址。
            /// </summary>
            public string SignalAddress { get; set; }

            /// <summary>
            /// 任务创建时间。
            /// </summary>
            public DateTime CreatedTime { get; set; }

            /// <summary>
            /// 任务完成时间。
            /// </summary>
            public DateTime? CompletedTime { get; set; }

            /// <summary>
            /// 最后更新时间。
            /// </summary>
            public DateTime? LastUpdatedTime { get; set; }

            /// <summary>
            /// 外部业务任务编号。
            /// </summary>
            public string TaskId { get; set; }

            /// <summary>
            /// 目标信号值。
            /// </summary>
            public bool Value { get; set; }
        }

        public enum TaskType
        {
            ArrivalNotify,
            PassCheck,
            PassComplete
        }

        public enum TaskStatus
        {
            Pending,
            Completed,
            Failed
        }
    }
}
