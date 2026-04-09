using System.Collections.Concurrent;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Dapper;
using NModbus;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.IO;
using static WarehouseManagementSystem.Service.Io.IOAGVTaskProcessor;

namespace WarehouseManagementSystem.Service.Io
{
    public interface IIOService
    {
        Task<bool> Conn(string ip);
        Task<bool> ReadSignal(string ip, EIOAddress address);
        Task<bool> WriteSignal(string ip, EIOAddress address, bool value);
        List<Remote_IO_Info> GetConnectedClients();
        Task UpdateDeviceMonitoring(int deviceId, bool isEnabled);
        Task StartDeviceMonitoring();

        /// <summary>
        /// 向 IO 任务表中插入一条待处理任务。
        /// </summary>
        Task<int> AddIOTask(string taskType, string deviceIP, string signalAddress, bool value, string taskId);
    }

    /// <summary>
    /// 已连接的远程 IO 设备信息。
    /// </summary>
    public class Remote_IO_Info
    {
        public string IP { get; set; }
        public ModbusFactory NModbus { get; set; }
        public TcpClient Master_TcpClient { get; set; }
        public IModbusMaster Master { get; set; }
    }

    public class IOService : IIOService
    {
        private readonly ILogger<IOService> _logger;
        private readonly List<Remote_IO_Info> io_List = new();
        private readonly IDatabaseService _db;
        private readonly IServiceProvider _serviceProvider;
        private readonly IIODeviceService _ioDeviceService;

        // 记录按设备维度启动的监控任务，便于启停控制。
        private readonly ConcurrentDictionary<string, (Task Task, CancellationTokenSource Cts)> _monitoringTasks = new();

        // 每个设备 IP 一个连接锁，防止并发重连导致连接对象被互相覆盖。
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new();

        // 每个设备 IP 一个操作锁，保证对同一设备的读写严格串行。
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceOperationLocks = new();

        // 通讯参数适度收紧，避免故障时单次调用被超时拖得过长。
        private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(3);
        private const int ReadTimeoutMilliseconds = 800;
        private const int ModbusRetries = 1;
        private const int OperationRetryCount = 2;

        public IOService(
            ILogger<IOService> logger,
            IDatabaseService db,
            IIODeviceService ioDeviceService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _db = db;
            _serviceProvider = serviceProvider;
            _ioDeviceService = ioDeviceService;
        }

        public List<Remote_IO_Info> GetConnectedClients()
        {
            return io_List;
        }

        /// <summary>
        /// 建立或重建与指定设备的 Modbus TCP 连接。
        /// </summary>
        public async Task<bool> Conn(string ip)
        {
            var connectionLock = _connectionLocks.GetOrAdd(ip, _ => new SemaphoreSlim(1, 1));
            await connectionLock.WaitAsync();

            try
            {
                var existingInfo = io_List.FirstOrDefault(m => m.IP == ip);
                if (existingInfo?.Master_TcpClient?.Client?.Connected == true)
                {
                    _logger.LogDebug("IO_【{IP}】连接已存在，跳过重复连接", ip);
                    return true;
                }

                _logger.LogInformation("IO_【{IP}】开始建立连接", ip);

                var remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                if (remoteInfo != null)
                {
                    CleanupRemoteInfo(remoteInfo);
                    io_List.Remove(remoteInfo);
                }

                remoteInfo = new Remote_IO_Info { IP = ip };
                io_List.Add(remoteInfo);

                try
                {
                    remoteInfo.NModbus = new ModbusFactory();
                    remoteInfo.Master_TcpClient = new TcpClient();

                    using var cts = new CancellationTokenSource(_connectTimeout);
                    await remoteInfo.Master_TcpClient.ConnectAsync(ip, 502).WaitAsync(cts.Token);

                    if (!remoteInfo.Master_TcpClient.Connected)
                    {
                        _logger.LogWarning("IO_【{IP}】连接未建立成功", ip);
                        CleanupRemoteInfo(remoteInfo);
                        io_List.Remove(remoteInfo);
                        return false;
                    }

                    remoteInfo.Master = remoteInfo.NModbus.CreateMaster(remoteInfo.Master_TcpClient);
                    remoteInfo.Master.Transport.ReadTimeout = ReadTimeoutMilliseconds;
                    remoteInfo.Master.Transport.Retries = ModbusRetries;

                    _logger.LogInformation("IO_【{IP}】连接成功", ip);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IO_【{IP}】连接失败", ip);
                    CleanupRemoteInfo(remoteInfo);
                    io_List.Remove(remoteInfo);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IO_【{IP}】连接过程发生异常", ip);
                return false;
            }
            finally
            {
                connectionLock.Release();
            }
        }

        /// <summary>
        /// 读取设备信号。
        /// 由于设备明确要求单线程访问，这里按 IP 进行串行化。
        /// </summary>
        public async Task<bool> ReadSignal(string ip, EIOAddress address)
        {
            var deviceLock = _deviceOperationLocks.GetOrAdd(ip, _ => new SemaphoreSlim(1, 1));
            await deviceLock.WaitAsync();

            try
            {
                return await ReadSignalImpl(ip, address);
            }
            finally
            {
                deviceLock.Release();
            }
        }

        /// <summary>
        /// 写入设备信号。
        /// 同样通过设备级操作锁保证同一时刻只有一个读写操作进入设备。
        /// </summary>
        public async Task<bool> WriteSignal(string ip, EIOAddress address, bool value)
        {
            var deviceLock = _deviceOperationLocks.GetOrAdd(ip, _ => new SemaphoreSlim(1, 1));
            await deviceLock.WaitAsync();

            try
            {
                return await WriteSignalImpl(ip, address, value);
            }
            finally
            {
                deviceLock.Release();
            }
        }

        /// <summary>
        /// 实际执行读取逻辑。
        /// 如果连接失效会先重连，通讯失败时做有限次快速重试。
        /// </summary>
        private async Task<bool> ReadSignalImpl(string ip, EIOAddress address)
        {
            try
            {
                var remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                if (remoteInfo?.Master_TcpClient?.Client?.Connected != true ||
                    !CheckConnection(remoteInfo.Master_TcpClient))
                {
                    var isConnected = await Conn(ip);
                    if (!isConnected)
                    {
                        _logger.LogWarning("IO_【{IP}】读取前重连失败", ip);
                        return false;
                    }

                    remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                }

                for (var retry = 0; retry < OperationRetryCount; retry++)
                {
                    try
                    {
                        remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                        if (remoteInfo?.Master == null)
                        {
                            throw new InvalidOperationException("连接对象不存在");
                        }

                        var result = await remoteInfo.Master.ReadCoilsAsync(1, (ushort)address, 1);
                        return result[0];
                    }
                    catch (Exception ex)
                    {
                        var isConnectionError = IsConnectionException(ex);
                        if (retry == OperationRetryCount - 1)
                        {
                            _logger.LogError(ex, "IO_【{IP}】读取地址 {Address} 失败", ip, address);
                            if (isConnectionError)
                            {
                                await Conn(ip);
                            }
                            return false;
                        }

                        _logger.LogWarning(ex, "IO_【{IP}】读取地址 {Address} 第 {Retry} 次失败，准备重试", ip, address, retry + 1);

                        if (isConnectionError)
                        {
                            var reconnected = await Conn(ip);
                            if (!reconnected)
                            {
                                return false;
                            }

                            await Task.Delay(50);
                        }
                        else
                        {
                            await Task.Delay(80 * (retry + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IO_【{IP}】读取地址 {Address} 时发生异常", ip, address);
            }

            return false;
        }

        /// <summary>
        /// 实际执行写入逻辑。
        /// 写入失败时同样做有限次重试，但不再使用过重的等待策略。
        /// </summary>
        private async Task<bool> WriteSignalImpl(string ip, EIOAddress address, bool value)
        {
            try
            {
                var remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                if (remoteInfo?.Master_TcpClient?.Client?.Connected != true ||
                    !CheckConnection(remoteInfo.Master_TcpClient))
                {
                    var isConnected = await Conn(ip);
                    if (!isConnected)
                    {
                        _logger.LogWarning("IO_【{IP}】写入前重连失败", ip);
                        return false;
                    }

                    remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                }

                for (var retry = 0; retry < OperationRetryCount; retry++)
                {
                    try
                    {
                        remoteInfo = io_List.FirstOrDefault(m => m.IP == ip);
                        if (remoteInfo?.Master == null)
                        {
                            throw new InvalidOperationException("连接对象不存在");
                        }

                        await remoteInfo.Master.WriteSingleCoilAsync(1, (ushort)address, value);
                        _logger.LogInformation("IO_【{IP}】地址 {Address} 写入 {Value} 成功", ip, address, value);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        var isConnectionError = IsConnectionException(ex);
                        if (retry == OperationRetryCount - 1)
                        {
                            _logger.LogError(ex, "IO_【{IP}】写入地址 {Address} 失败", ip, address);
                            if (isConnectionError)
                            {
                                await Conn(ip);
                            }
                            return false;
                        }

                        _logger.LogWarning(ex, "IO_【{IP}】写入地址 {Address} 第 {Retry} 次失败，准备重试", ip, address, retry + 1);

                        if (isConnectionError)
                        {
                            var reconnected = await Conn(ip);
                            if (!reconnected)
                            {
                                return false;
                            }

                            await Task.Delay(50);
                        }
                        else
                        {
                            await Task.Delay(80 * (retry + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IO_【{IP}】写入地址 {Address} 时发生异常", ip, address);
            }

            return false;
        }

        /// <summary>
        /// 检查 Socket 是否仍然有效。
        /// </summary>
        private bool CheckConnection(TcpClient client)
        {
            try
            {
                if (client?.Client == null)
                {
                    return false;
                }

                if (!client.Connected)
                {
                    return false;
                }

                return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 开始全局监控 IO 设备。
        /// 该方法保留原有能力，但内部按设备串行读取，避免与设备单线程要求冲突。
        /// </summary>
        public Task StartDeviceMonitoring()
        {
            _logger.LogInformation("开始监控 IO 设备");

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using var conn = _db.CreateConnection();
                        var devices = await conn.QueryAsync<RCS_IODevices>(
                            "SELECT * FROM RCS_IODevices WHERE IsEnabled = 1");

                        foreach (var device in devices)
                        {
                            if (string.IsNullOrWhiteSpace(device.IP))
                            {
                                _logger.LogWarning("设备 {DeviceId} 未配置有效 IP，跳过监控刷新", device.Id);
                                continue;
                            }

                            var signals = await conn.QueryAsync<RCS_IOSignals>(@"
                                SELECT *
                                FROM RCS_IOSignals
                                WHERE DeviceId = @DeviceId",
                                new { DeviceId = device.Id });

                            foreach (var signal in signals)
                            {
                                try
                                {
                                    if (!Enum.TryParse<EIOAddress>(signal.Address, out var addressEnum))
                                    {
                                        continue;
                                    }

                                    var value = Convert.ToInt32(await ReadSignal(device.IP, addressEnum));
                                    if (signal.Value == value)
                                    {
                                        continue;
                                    }

                                    signal.UpdatedTime = DateTime.Now;
                                    signal.Value = value;

                                    await conn.ExecuteAsync(@"
                                        UPDATE RCS_IOSignals
                                        SET Value = @Value,
                                            UpdatedTime = @UpdatedTime
                                        WHERE Id = @Id",
                                        new { signal.Value, signal.UpdatedTime, signal.Id });
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "读取设备 {DeviceName}({DeviceIP}) 信号 {Address} 失败", device.Name, device.IP, signal.Address);
                                }
                            }
                        }

                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "监控 IO 设备时发生异常");
                        await Task.Delay(1000);
                    }
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 按设备启动专属监控任务。
        /// 监控流程改为顺序读取，不再对单台设备做并发点位采集。
        /// </summary>
        private void StartMonitoringDevice(RCS_IODevices device)
        {
            if (string.IsNullOrWhiteSpace(device.IP))
            {
                _logger.LogWarning("设备 {DeviceId} 未配置有效 IP，无法启动监控", device.Id);
                return;
            }

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var monitoringTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var conn = _db.CreateConnection();
                        var signals = await conn.QueryAsync<RCS_IOSignals>(@"
                            SELECT *
                            FROM RCS_IOSignals
                            WHERE DeviceId = @DeviceId",
                            new { DeviceId = device.Id });

                        foreach (var signal in signals)
                        {
                            token.ThrowIfCancellationRequested();

                            try
                            {
                                if (!Enum.TryParse<EIOAddress>(signal.Address, out var addressEnum))
                                {
                                    _logger.LogWarning("设备 {DeviceId} 存在无效信号地址：{Address}", device.Id, signal.Address);
                                    continue;
                                }

                                var value = Convert.ToInt32(await ReadSignal(device.IP, addressEnum));
                                if (signal.Value == value)
                                {
                                    continue;
                                }

                                signal.UpdatedTime = DateTime.Now;
                                signal.Value = value;
                                await _ioDeviceService.UpdateSignalAsync(signal);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                _logger.LogError(ex, "读取设备 {DeviceId} 信号 {Address} 失败", device.Id, signal.Address);
                            }
                        }

                        await Task.Delay(500, token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "监控设备 {DeviceId} 时发生异常", device.Id);
                        await Task.Delay(1000, token);
                    }
                }

                _logger.LogInformation("设备 {DeviceId} 的监控任务已停止", device.Id);
            }, token);

            _monitoringTasks.TryAdd(device.IP, (monitoringTask, cts));
        }

        /// <summary>
        /// 根据设备启停状态启动或停止监控任务。
        /// </summary>
        public async Task UpdateDeviceMonitoring(int deviceId, bool isEnabled)
        {
            using var conn = _db.CreateConnection();
            var device = await conn.QueryFirstOrDefaultAsync<RCS_IODevices>(
                "SELECT * FROM RCS_IODevices WHERE Id = @Id",
                new { Id = deviceId });

            if (device == null)
            {
                return;
            }

            if (isEnabled)
            {
                StartMonitoringDevice(device);
                _logger.LogInformation(
                    "设备监控已启动：ID={ID}, IP={IP}, Time={Time}, User={User}",
                    device.Id,
                    device.IP,
                    DateTime.UtcNow,
                    "YCmai");
                return;
            }

            if (!string.IsNullOrWhiteSpace(device.IP) && _monitoringTasks.TryRemove(device.IP, out var taskInfo))
            {
                var (task, cts) = taskInfo;
                cts.Cancel();

                try
                {
                    await task;
                    cts.Dispose();
                }
                catch (OperationCanceledException)
                {
                    // 监控任务被取消时属于正常流程，不额外记录错误。
                }

                _logger.LogInformation(
                    "设备监控已停止：ID={ID}, IP={IP}, Time={Time}, User={User}",
                    device.Id,
                    device.IP,
                    DateTime.UtcNow,
                    "YCmai");
            }
        }

        /// <summary>
        /// 创建一条 IO 任务，供设备处理服务异步消费。
        /// </summary>
        public async Task<int> AddIOTask(string taskType, string deviceIP, string signalAddress, bool value, string taskId)
        {
            try
            {
                using var conn = _db.CreateConnection();
                var task = new RCS_IOAGV_Tasks
                {
                    TaskType = taskType,
                    Status = "Pending",
                    DeviceIP = deviceIP,
                    SignalAddress = signalAddress,
                    Value = value,
                    TaskId = taskId,
                    CreatedTime = DateTime.Now,
                    LastUpdatedTime = DateTime.Now
                };

                var sql = @"
                    INSERT INTO RCS_IOAGV_Tasks
                        (TaskType, Status, DeviceIP, SignalAddress, Value, TaskId, CreatedTime, LastUpdatedTime)
                    VALUES
                        (@TaskType, @Status, @DeviceIP, @SignalAddress, @Value, @TaskId, @CreatedTime, @LastUpdatedTime);
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                var newTaskId = await conn.ExecuteScalarAsync<int>(sql, task);
                _logger.LogInformation(
                    "创建 IO 任务成功：ID={TaskId}, Type={TaskType}, Device={DeviceIP}",
                    newTaskId,
                    taskType,
                    deviceIP);
                return newTaskId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建 IO 任务失败");
                throw;
            }
        }

        /// <summary>
        /// 释放连接资源。
        /// </summary>
        private void CleanupRemoteInfo(Remote_IO_Info remoteInfo)
        {
            try
            {
                remoteInfo.Master?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放 Modbus Master 资源时发生异常：{IP}", remoteInfo.IP);
            }

            try
            {
                remoteInfo.Master_TcpClient?.Close();
                remoteInfo.Master_TcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放 TCP 连接资源时发生异常：{IP}", remoteInfo.IP);
            }
        }

        /// <summary>
        /// 识别是否属于典型的连接异常。
        /// </summary>
        private static bool IsConnectionException(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
                || message.Contains("non-connected", StringComparison.OrdinalIgnoreCase)
                || message.Contains("远程主机强迫关闭", StringComparison.OrdinalIgnoreCase)
                || ex is SocketException
                || ex is IOException;
        }
    }
}
