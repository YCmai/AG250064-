using Client100.Entity;

using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;

using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.PLC;

namespace WarehouseManagementSystem.Service.Plc
{
    public class PlcSignalService : IPlcSignalService
    {
        private const string DefaultSiemensBrand = "西门子";
        private const string DefaultDbBlock = "DB1";
        private const string DefaultPlcIpAddress = "192.168.0.100";
        private const string DefaultPlcRemark = "DB1数据交互PLC";
        private const string ManualWritableWriter = IPlcSignalService.ManualWritableWriter;
        private readonly IDatabaseService _db;
        private readonly ILogger<PlcSignalService> _logger;

        public PlcSignalService(IDatabaseService db, ILogger<PlcSignalService> logger)
        {
            _db = db;
            _logger = logger;
        }

        #region PLC设备相关操作
        public async Task<List<RCS_PlcDevice>> GetAllPlcDevicesAsync()
        {
            try
            {
                using var conn = _db.CreateConnection();
                var devices = await conn.QueryAsync<RCS_PlcDevice>(@"
                    SELECT *
                    FROM RCS_PlcDevice 
                    ORDER BY Id");

                var deviceList = devices.ToList();

                // 获取每个设备下的信号
                foreach (var device in deviceList)
                {
                    var signals = await conn.QueryAsync<RCS_PlcSignal>(@"
                        SELECT *
                        FROM RCS_PlcSignal
                        WHERE PlcDeviceId = @DeviceId And PLCTypeDb =@PLCTypeDb
                        ORDER BY Id", new { DeviceId = device.IpAddress, PLCTypeDb = device.ModuleAddress });

                    device.Signals = signals.ToList();
                }

                return deviceList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有PLC设备失败");
                throw;
            }
        }

        public async Task<RCS_PlcDevice> GetPlcDeviceByIdAsync(int id)
        {
            try
            {
                using var conn = _db.CreateConnection();
                var device = await conn.QueryFirstOrDefaultAsync<RCS_PlcDevice>(@"
                    SELECT Id, IpAddress, Port, IsEnabled, Brand, StationPoint, 
                    SignalRequestPoint, LeaveResetPoint, Remark, ModuleAddress, CreateTime, UpdateTime
                    FROM RCS_PlcDevice
                    WHERE Id = @Id", new { Id = id });

                if (device != null)
                {
                    var signals = await conn.QueryAsync<RCS_PlcSignal>(@"
                        SELECT Id, PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime, UpdateTime
                        FROM RCS_PlcSignal
                        WHERE PlcDeviceId = @DeviceId And PLCTypeDb =@PLCTypeDb
                        ORDER BY Id", new { DeviceId = device.IpAddress, PLCTypeDb = device.ModuleAddress });

                    device.Signals = signals.ToList();
                }

                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC设备失败，Id: {Id}", id);
                throw;
            }
        }

        public async Task<int> AddPlcDeviceAsync(RCS_PlcDevice device)
        {
            try
            {
                using var conn = _db.CreateConnection();

                device.CreateTime = DateTime.Now;

                int deviceId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO RCS_PlcDevice (IpAddress, Port, IsEnabled, Brand, StationPoint, 
                    SignalRequestPoint, LeaveResetPoint, Remark, ModuleAddress, CreateTime)
                    VALUES (@IpAddress, @Port, @IsEnabled, @Brand, @StationPoint, 
                    @SignalRequestPoint, @LeaveResetPoint, @Remark, @ModuleAddress, @CreateTime);
                    SELECT CAST(SCOPE_IDENTITY() as int)", device);

                // 添加设备后自动添加默认的PLC信号
                await AddDefaultPlcSignalsAsync(device.IpAddress, device.ModuleAddress, device.Brand);

                return deviceId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加PLC设备失败");
                throw;
            }
        }

        /// <summary>
        /// 初始化指定 PLC 设备与标准 DB1 信号模板。
        /// 采用幂等方式导入，是为了让现场可以重复点击初始化而不产生重复信号。
        /// </summary>
        /// <param name="request">初始化参数。</param>
        /// <returns>初始化结果。</returns>
        public async Task<PlcInitializationResult> InitializeDeviceSignalsAsync(PlcDeviceInitializationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedIpAddress = string.IsNullOrWhiteSpace(request.IpAddress)
                ? DefaultPlcIpAddress
                : request.IpAddress.Trim();
            var normalizedDbBlock = NormalizeDbBlock(request.ModuleAddress);

            using var conn = _db.CreateConnection();
            if (conn is SqlConnection sqlConnection)
            {
                await sqlConnection.OpenAsync();
            }
            else
            {
                conn.Open();
            }

            using var transaction = conn.BeginTransaction();

            try
            {
                var existingDevice = await conn.QueryFirstOrDefaultAsync<RCS_PlcDevice>(@"
                    SELECT TOP 1 *
                    FROM RCS_PlcDevice
                    WHERE IpAddress = @IpAddress
                      AND ModuleAddress = @ModuleAddress",
                    new
                    {
                        IpAddress = normalizedIpAddress,
                        ModuleAddress = normalizedDbBlock
                    },
                    transaction);

                var result = new PlcInitializationResult();
                int deviceId;

                if (existingDevice == null)
                {
                    var deviceToCreate = new RCS_PlcDevice
                    {
                        IpAddress = normalizedIpAddress,
                        Port = request.Port <= 0 ? 102 : request.Port,
                        IsEnabled = request.IsEnabled,
                        Brand = string.IsNullOrWhiteSpace(request.Brand) ? DefaultSiemensBrand : request.Brand.Trim(),
                        StationPoint = request.StationPoint?.Trim(),
                        SignalRequestPoint = request.SignalRequestPoint?.Trim(),
                        LeaveResetPoint = request.LeaveResetPoint?.Trim(),
                        Remark = string.IsNullOrWhiteSpace(request.Remark) ? DefaultPlcRemark : request.Remark.Trim(),
                        ModuleAddress = normalizedDbBlock,
                        CreateTime = DateTime.Now
                    };

                    deviceId = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO RCS_PlcDevice (IpAddress, Port, IsEnabled, Brand, StationPoint,
                        SignalRequestPoint, LeaveResetPoint, Remark, ModuleAddress, CreateTime)
                        VALUES (@IpAddress, @Port, @IsEnabled, @Brand, @StationPoint,
                        @SignalRequestPoint, @LeaveResetPoint, @Remark, @ModuleAddress, @CreateTime);
                        SELECT CAST(SCOPE_IDENTITY() as int);",
                        deviceToCreate,
                        transaction);

                    result.DeviceCreated = true;
                }
                else
                {
                    deviceId = existingDevice.Id;

                    await conn.ExecuteAsync(@"
                        UPDATE RCS_PlcDevice
                        SET Port = @Port,
                            IsEnabled = @IsEnabled,
                            Brand = @Brand,
                            StationPoint = @StationPoint,
                            SignalRequestPoint = @SignalRequestPoint,
                            LeaveResetPoint = @LeaveResetPoint,
                            Remark = @Remark,
                            UpdateTime = @UpdateTime
                        WHERE Id = @Id",
                        new
                        {
                            Id = existingDevice.Id,
                            Port = request.Port <= 0 ? existingDevice.Port : request.Port,
                            IsEnabled = request.IsEnabled,
                            Brand = string.IsNullOrWhiteSpace(request.Brand) ? existingDevice.Brand : request.Brand.Trim(),
                            StationPoint = request.StationPoint?.Trim() ?? existingDevice.StationPoint,
                            SignalRequestPoint = request.SignalRequestPoint?.Trim() ?? existingDevice.SignalRequestPoint,
                            LeaveResetPoint = request.LeaveResetPoint?.Trim() ?? existingDevice.LeaveResetPoint,
                            Remark = string.IsNullOrWhiteSpace(request.Remark) ? existingDevice.Remark : request.Remark.Trim(),
                            UpdateTime = DateTime.Now
                        },
                        transaction);
                }

                var existingSignals = (await conn.QueryAsync<RCS_PlcSignal>(@"
                    SELECT Id, PlcDeviceId, Offset, Name, PLCTypeDb
                    FROM RCS_PlcSignal
                    WHERE PlcDeviceId = @PlcDeviceId
                      AND PLCTypeDb = @PLCTypeDb",
                    new
                    {
                        PlcDeviceId = normalizedIpAddress,
                        PLCTypeDb = normalizedDbBlock
                    },
                    transaction)).ToList();

                var existingSignalKeys = new HashSet<string>(
                    existingSignals.Select(BuildSignalIdentityKey),
                    StringComparer.OrdinalIgnoreCase);

                var templateSignals = BuildDb1SignalTemplates(normalizedIpAddress, normalizedDbBlock);
                var signalsToInsert = new List<RCS_PlcSignal>();

                foreach (var signal in templateSignals)
                {
                    var signalKey = BuildSignalIdentityKey(signal);
                    if (existingSignalKeys.Contains(signalKey))
                    {
                        result.SkippedSignalCount++;
                        continue;
                    }

                    signal.CreateTime = DateTime.Now;
                    signalsToInsert.Add(signal);
                }

                if (signalsToInsert.Count > 0)
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO RCS_PlcSignal (PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime)
                        VALUES (@PlcDeviceId, @DataType, @Offset, @Name, @Writer, @CurrentValue, @Remark, @PLCTypeDb, @CreateTime);",
                        signalsToInsert,
                        transaction);
                }

                transaction.Commit();

                result.DeviceId = deviceId;
                result.InsertedSignalCount = signalsToInsert.Count;

                return result;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "初始化 PLC 设备与信号模板失败，Ip={IpAddress}, Db={DbBlock}", normalizedIpAddress, normalizedDbBlock);
                throw;
            }
        }

        // 添加默认的PLC信号
        private async Task AddDefaultPlcSignalsAsync(string ipAddress, string dbAddress, string brand)
        {
            try
            {
                using var conn = _db.CreateConnection();


                // 获取设备的ModuleAddress作为PLCTypeDb和设备ID
                string moduleAddress = dbAddress;
                string deviceIp = ipAddress;

                // 根据提供的规范定义默认信号列表
                var defaultSignals = new List<RCS_PlcSignal>
                {
                    // 进站信号
                     new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "0.0", Name = "进站心跳", Writer = "AGV", Remark = "进站心跳", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                     new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4.4", Name = "进站AGV请求进入", Writer = "AGV", Remark = "进站AGV请求进入", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4.6", Name = "进站AGV已到达", Writer = "AGV", Remark = "进站AGV已到达", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                      new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "122.7", Name = "出站离开中", Writer = "AGV", Remark = "出站离开中", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "123", Name = "出站已离开", Writer = "AGV", Remark = "出站已离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "248.5", Name = "允许进入", Writer = "PLC", Remark = "允许进入", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "248.7", Name = "允许离开", Writer = "PLC", Remark = "允许离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                      new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "249.2", Name = "工位状态", Writer = "PLC", Remark = "工位状态", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "249.1", Name = "已收到离开", Writer = "PLC", Remark = "已收到离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                          new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "240.0", Name = "反馈已清除信号", Writer = "AGV", Remark = "反馈已清除信号", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                            new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Int", Offset = "250", Name = "指定AGV路线1容量测试2进六面检", Writer = "PLC", Remark = "指定AGV路线1容量测试2进六面检", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4", Name = "进站AGV离线模式", Writer = "AGV", Remark = "进站AGV离线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4.1", Name = "进站AGV在线模式", Writer = "AGV", Remark = "进站AGV在线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4.2", Name = "进站AGV故障", Writer = "AGV", Remark = "进站AGV故障", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "4.3", Name = "进站AGV急停", Writer = "AGV", Remark = "进站AGV急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                    // 出站信号
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "122", Name = "出站AGV离线模式", Writer = "AGV", Remark = "出站AGV离线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "122.1", Name = "出站AGV在线模式", Writer = "AGV", Remark = "出站AGV在线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "122.2", Name = "出站AGV故障", Writer = "AGV", Remark = "出站AGV故障", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "122.3", Name = "出站AGV急停", Writer = "AGV", Remark = "出站AGV急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                    // PLC侧信号
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "248.2", Name = "PLC报警", Writer = "PLC", Remark = "PLC报警", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "248.3", Name = "PLC急停", Writer = "PLC", Remark = "PLC急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "String", Offset = "258", Name = "写入PACKID", Writer = "PLC", Remark = "写入PACKID", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                  
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "7.1", Name = "进站PACKID写入应答", Writer = "AGV", Remark = "进站PACKID写入应答", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                    new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "String", Offset = "14", Name = "进站AGV_PackID", Writer = "AGV", Remark = "进站AGV_PackID", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                      new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "118.0", Name = "出站心跳", Writer = "AGV", Remark = "出站心跳", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                     
                };

                if (brand == "欧姆龙")
                {
                    defaultSignals = new List<RCS_PlcSignal>
                    {

                          new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.0", Name = "进站心跳", Writer = "AGV", Remark = "进站心跳", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                            new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.5", Name = "进站AGV请求进入", Writer = "AGV", Remark = "进站AGV请求进入", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.7", Name = "进站AGV已到达", Writer = "AGV", Remark = "进站AGV已到达", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                         new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.8", Name = "出站离开中", Writer = "AGV", Remark = "出站离开中", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.9", Name = "出站已离开", Writer = "AGV", Remark = "出站已离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                          new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D300.6", Name = "允许进入", Writer = "PLC", Remark = "允许进入", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D300.8", Name = "允许离开", Writer = "PLC", Remark = "允许离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                          new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D300.10", Name = "已收到离开", Writer = "PLC", Remark = "已收到离开", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                          new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D257.0", Name = "反馈已清除信号", Writer = "AGV", Remark = "反馈已清除信号", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                            new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Int", Offset = "D302", Name = "指定AGV路线1容量测试2进六面检", Writer = "PLC", Remark = "指定AGV路线1容量测试2进六面检", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },


                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.1", Name = "进站AGV离线模式", Writer = "AGV", Remark = "进站AGV离线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.2", Name = "进站AGV在线模式", Writer = "AGV", Remark = "进站AGV在线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.3", Name = "进站AGV故障", Writer = "AGV", Remark = "进站AGV故障", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D100.4", Name = "进站AGV急停", Writer = "AGV", Remark = "进站AGV急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                      

                        // 出站信号
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.1", Name = "出站AGV离线模式", Writer = "AGV", Remark = "出站AGV离线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.2", Name = "出站AGV在线模式", Writer = "AGV", Remark = "出站AGV在线模式", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.3", Name = "出站AGV故障", Writer = "AGV", Remark = "出站AGV故障", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.4", Name = "出站AGV急停", Writer = "AGV", Remark = "出站AGV急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                        // PLC侧信号
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D300.3", Name = "PLC报警", Writer = "PLC", Remark = "PLC报警", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D300.4", Name = "PLC急停", Writer = "PLC", Remark = "PLC急停", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                      
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "String", Offset = "D306", Name = "写入PACKID", Writer = "PLC", Remark = "写入PACKID", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                       // new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "249.2", Name = "工位状态", Writer = "PLC", Remark = "工位状态", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now }
                         // 进站信号
                         new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D102.1", Name = "进站PACKID写入应答", Writer = "AGV", Remark = "进站PACKID写入应答", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                        new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "String", Offset = "D106", Name = "进站AGV_PackID", Writer = "AGV", Remark = "进站AGV_PackID", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },
                           new RCS_PlcSignal { PlcDeviceId = ipAddress, DataType = "Bool", Offset = "D200.0", Name = "出站心跳", Writer = "AGV", Remark = "出站心跳", PLCTypeDb = moduleAddress, CreateTime = DateTime.Now },

                          
                    };
                }


                // 批量插入默认信号
                var sql = @"
                    INSERT INTO RCS_PlcSignal (PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime)
                    VALUES (@PlcDeviceId, @DataType, @Offset, @Name, @Writer, @CurrentValue, @Remark, @PLCTypeDb, @CreateTime);";

                await conn.ExecuteAsync(sql, defaultSignals);
                _logger.LogInformation("成功为设备 IP={IpAddress} (ID={DeviceId}) 添加 {Count} 个默认PLC信号", ipAddress, deviceIp, defaultSignals.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "为设备 IP={IpAddress} 添加默认PLC信号失败", ipAddress);
                // 不抛出异常，确保即使添加默认信号失败，也不影响设备的创建
            }
        }

        public async Task UpdatePlcDeviceAsync(RCS_PlcDevice device)
        {
            try
            {
                using var conn = _db.CreateConnection();

                // 设置更新时间
                device.UpdateTime = DateTime.Now;

                // 查询设备是否存在
                var existingDevice = await conn.QueryFirstOrDefaultAsync<RCS_PlcDevice>(
                    "SELECT Id FROM RCS_PlcDevice WHERE Id = @Id", new { Id = device.Id });

                if (existingDevice == null)
                {
                    throw new Exception($"设备ID {device.Id} 不存在");
                }

                // 执行更新
                int rowsAffected = await conn.ExecuteAsync(@"
                    UPDATE RCS_PlcDevice
                    SET IpAddress = @IpAddress,
                        Port = @Port,
                        IsEnabled = @IsEnabled,
                        Brand = @Brand,
                        StationPoint = @StationPoint,
                        SignalRequestPoint = @SignalRequestPoint,
                        LeaveResetPoint = @LeaveResetPoint,
                        Remark = @Remark,
                        ModuleAddress = @ModuleAddress,
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id", device);

                if (rowsAffected == 0)
                {
                    throw new Exception($"更新设备失败，设备ID {device.Id} 可能已被删除");
                }

                _logger.LogInformation("成功更新PLC设备: ID={Id}", device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新PLC设备失败，Id: {Id}", device.Id);
                throw;
            }
        }

        public async Task DeletePlcDeviceAsync(int id)
        {
            try
            {
                using var conn = _db.CreateConnection();

                // 先获取设备的IpAddress
                var device = await conn.QueryFirstOrDefaultAsync<RCS_PlcDevice>(
                    "SELECT * FROM RCS_PlcDevice WHERE Id = @Id", new { Id = id });

                if (device != null)
                {
                    // 通过IpAddress删除设备下所有信号
                    await conn.ExecuteAsync("DELETE FROM RCS_PlcSignal WHERE PlcDeviceId = @IpAddress And PLCTypeDb = @PLCTypeDb",
                        new { IpAddress = device.IpAddress, PLCTypeDb = device.ModuleAddress });

                    // 再删除设备
                    await conn.ExecuteAsync("DELETE FROM RCS_PlcDevice WHERE Id = @Id", new { Id = id });
                }
                else
                {
                    _logger.LogWarning("尝试删除不存在的PLC设备，Id: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除PLC设备失败，Id: {Id}", id);
                throw;
            }
        }
        #endregion

        #region PLC信号相关操作
        public async Task<List<RCS_PlcSignal>> GetAllPlcSignalsAsync()
        {
            try
            {
                using var conn = _db.CreateConnection();
                var signals = await conn.QueryAsync<RCS_PlcSignal>(@"
                    SELECT Id, PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime, UpdateTime
                    FROM RCS_PlcSignal
                    ORDER BY PlcDeviceId, Id");

                return signals.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有PLC信号失败");
                throw;
            }
        }

        public async Task<List<RCS_PlcSignal>> GetPlcSignalsByDeviceIdAsync(string deviceId, string dbBlock = null)
        {
            try
            {
                using var conn = _db.CreateConnection();
                
                string sql = @"
                    SELECT Id, PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime, UpdateTime
                    FROM RCS_PlcSignal
                    WHERE PlcDeviceId = @DeviceId";

                if (!string.IsNullOrEmpty(dbBlock))
                {
                    sql += " AND PLCTypeDb = @DbBlock";
                }
                
                sql += " ORDER BY Id";

                var signals = await conn.QueryAsync<RCS_PlcSignal>(sql, new { DeviceId = deviceId, DbBlock = dbBlock });

                return signals.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC设备下的信号失败，设备Id: {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<RCS_PlcSignal> GetPlcSignalByIdAsync(int id)
        {
            try
            {
                using var conn = _db.CreateConnection();
                return await conn.QueryFirstOrDefaultAsync<RCS_PlcSignal>(@"
                    SELECT *
                    FROM RCS_PlcSignal
                    WHERE Id = @Id", new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC信号失败，Id: {Id}", id);
                throw;
            }
        }

        public async Task<int> AddPlcSignalAsync(RCS_PlcSignal signal)
        {
            try
            {
                using var conn = _db.CreateConnection();

                signal.CreateTime = DateTime.Now;

                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO RCS_PlcSignal (PlcDeviceId, DataType, Offset, Name, Writer, CurrentValue, Remark, PLCTypeDb, CreateTime)
                    VALUES (@PlcDeviceId, @DataType, @Offset, @Name, @Writer, @CurrentValue, @Remark, @PLCTypeDb, @CreateTime);
                    SELECT CAST(SCOPE_IDENTITY() as int)", signal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加PLC信号失败");
                throw;
            }
        }

        public async Task UpdatePlcSignalAsync(RCS_PlcSignal signal)
        {
            try
            {
                using var conn = _db.CreateConnection();

                signal.UpdateTime = DateTime.Now;

                await conn.ExecuteAsync(@"
                    UPDATE RCS_PlcSignal
                    SET 
                        PlcDeviceId = @PlcDeviceId,
                        DataType = @DataType,
                        Offset = @Offset,
                        Name = @Name,
                        Writer = @Writer,
                        CurrentValue = @CurrentValue,
                        Remark = @Remark,
                        PLCTypeDb = @PLCTypeDb,
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id", signal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新PLC信号失败，Id: {Id}", signal.Id);
                throw;
            }
        }

        public async Task DeletePlcSignalAsync(int id)
        {
            try
            {
                using var conn = _db.CreateConnection();
                await conn.ExecuteAsync("DELETE FROM RCS_PlcSignal WHERE Id = @Id", new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除PLC信号失败，Id: {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// 重置PLC信号
        /// </summary>
        public async Task ResetPlcSignalAsync(int signalId)
        {
            try
            {
                using var conn = _db.CreateConnection();

                // 获取信号信息
                var signal = await conn.QueryFirstOrDefaultAsync<RCS_PlcSignal>(
                    "SELECT * FROM RCS_PlcSignal WHERE Id = @Id",
                    new { Id = signalId });

                if (signal == null)
                {
                    throw new Exception($"信号ID {signalId} 不存在");
                }

                ValidateManualWritableSignal(signal);

                // 重置信号值为默认值 (false/0)
                await conn.ExecuteAsync(@"
                    UPDATE RCS_PlcSignal
                    SET CurrentValue = 0, 
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id",
                    new
                    {
                        Id = signalId,
                        UpdateTime = DateTime.Now
                    });

                _logger.LogInformation("成功重置PLC信号: Id={Id}", signalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置PLC信号失败，Id: {Id}", signalId);
                throw;
            }
        }

        /// <summary>
        /// 手动触发PLC信号
        /// </summary>
        public async Task ManualTriggerSignalAsync(int signalId, bool value)
        {
            try
            {
                using var conn = _db.CreateConnection();

                // 获取信号信息
                var signal = await conn.QueryFirstOrDefaultAsync<RCS_PlcSignal>(
                    "SELECT * FROM RCS_PlcSignal WHERE Id = @Id",
                    new { Id = signalId });

                if (signal == null)
                {
                    throw new Exception($"信号ID {signalId} 不存在");
                }

                ValidateManualWritableSignal(signal);

                // 更新信号值
                await conn.ExecuteAsync(@"
                    UPDATE RCS_PlcSignal
                    SET CurrentValue = @CurrentValue, 
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id",
                    new
                    {
                        Id = signalId,
                        CurrentValue = value ? 1 : 0,
                        UpdateTime = DateTime.Now
                    });

                _logger.LogInformation("成功手动触发PLC信号: Id={Id}, Value={Value}", signalId, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手动触发PLC信号失败，Id: {Id}, Value: {Value}", signalId, value);
                throw;
            }
        }

        public async Task<AutoPlcTask> GetAutoTask(string PlcType, string PLCTypeDb, string Signal, int Status)
        {
            try
            {
               // _logger.LogInformation($"查找交互任务PlcType-{PlcType}-PLCTypeDb-{PLCTypeDb}-Signal{Signal}-Status-{Status}");

                using var conn = _db.CreateConnection();

                // 获取信号信息
                var task = await conn.QueryFirstOrDefaultAsync<AutoPlcTask>(
                    "SELECT * FROM RCS_AutoPlcTasks WHERE Signal = @Signal And PlcType = @PlcType And PLCTypeDb = @PLCTypeDb And Status=@Status And IsSend = 0",
                    new { Signal = Signal, PlcType = PlcType, PLCTypeDb = PLCTypeDb, Status= Status });

                return task; // 返回查询结果，可能为null
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询PLC交互任务失败");
                throw;
            }
        }

        public async Task UpdateAutoTask(int Id)
        {
            try
            {
                using var conn = _db.CreateConnection();

                // 查询设备是否存在
                var existingDevice = await conn.QueryFirstOrDefaultAsync<AutoPlcTask>(
                    "SELECT * FROM RCS_AutoPlcTasks WHERE Id = @Id", new { Id = Id });

                if (existingDevice == null)
                {
                    throw new Exception($"PLC交互任务ID {Id} 不存在");
                }

                await conn.ExecuteAsync(@"
                    UPDATE RCS_AutoPlcTasks
                    SET IsSend = 1, 
                        UpdateTime = @UpdateTime
                    WHERE Id = @Id",
                   new
                   {
                       Id = Id,
                       UpdateTime = DateTime.Now
                   });

               // _logger.LogInformation("成功更新PLC任务成功: ID={Id}", Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新PLC任务成功，Id: {Id}", Id);
                throw;
            }
        }

        /// <summary>
        /// 构造 DB1 标准信号模板。
        /// Name 使用“区域/方向/信号名”的唯一命名格式，避免多个 IO 区域下出现重名信号后页面串列。
        /// Remark 保留原始现场中文名称，方便界面展示与后续排障。
        /// </summary>
        private static List<RCS_PlcSignal> BuildDb1SignalTemplates(string ipAddress, string dbBlock)
        {
            return new List<RCS_PlcSignal>
            {
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "0.0", "CPU-IO/中控读PLC/人员通行按钮", "PLC", "人员通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "0.1", "CPU-IO/中控读PLC/AGV通行按钮", "PLC", "AGV通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "2.0", "CPU-IO/中控写PLC/射灯-绿", ManualWritableWriter, "射灯-绿"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "2.1", "CPU-IO/中控写PLC/射灯-红", ManualWritableWriter, "射灯-红"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "2.2", "CPU-IO/中控写PLC/预留KA3", ManualWritableWriter, "预留KA3"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "2.3", "CPU-IO/中控写PLC/人员通行按钮指示灯", ManualWritableWriter, "人员通行按钮指示灯"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "2.4", "CPU-IO/中控写PLC/AGV通行按钮指示灯", ManualWritableWriter, "AGV通行按钮指示灯"),

                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "4.0", "IO1/中控读PLC/人员通行按钮", "PLC", "人员通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "4.1", "IO1/中控读PLC/AGV通行按钮", "PLC", "AGV通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "6.0", "IO1/中控写PLC/射灯-绿", ManualWritableWriter, "射灯-绿"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "6.1", "IO1/中控写PLC/射灯-红", ManualWritableWriter, "射灯-红"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "6.2", "IO1/中控写PLC/预留KA3", ManualWritableWriter, "预留KA3"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "6.3", "IO1/中控写PLC/人员通行按钮指示灯", ManualWritableWriter, "人员通行按钮指示灯"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "6.4", "IO1/中控写PLC/AGV通行按钮指示灯", ManualWritableWriter, "AGV通行按钮指示灯"),

                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "8.0", "IO2/中控读PLC/人员通行按钮", "PLC", "人员通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "8.1", "IO2/中控读PLC/AGV通行按钮", "PLC", "AGV通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "10.0", "IO2/中控写PLC/射灯-绿", ManualWritableWriter, "射灯-绿"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "10.1", "IO2/中控写PLC/射灯-红", ManualWritableWriter, "射灯-红"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "10.2", "IO2/中控写PLC/预留KA3", ManualWritableWriter, "预留KA3"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "10.3", "IO2/中控写PLC/人员通行按钮指示灯", ManualWritableWriter, "人员通行按钮指示灯"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "10.4", "IO2/中控写PLC/AGV通行按钮指示灯", ManualWritableWriter, "AGV通行按钮指示灯"),

                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "12.0", "IO3/中控读PLC/人员通行按钮", "PLC", "人员通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "12.1", "IO3/中控读PLC/AGV通行按钮", "PLC", "AGV通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "14.0", "IO3/中控写PLC/射灯-绿", ManualWritableWriter, "射灯-绿"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "14.1", "IO3/中控写PLC/射灯-红", ManualWritableWriter, "射灯-红"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "14.2", "IO3/中控写PLC/预留KA3", ManualWritableWriter, "预留KA3"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "14.3", "IO3/中控写PLC/人员通行按钮指示灯", ManualWritableWriter, "人员通行按钮指示灯"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "14.4", "IO3/中控写PLC/AGV通行按钮指示灯", ManualWritableWriter, "AGV通行按钮指示灯"),

                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "16.0", "IO4/中控读PLC/人员通行按钮", "PLC", "人员通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "16.1", "IO4/中控读PLC/AGV通行按钮", "PLC", "AGV通行按钮"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "18.0", "IO4/中控写PLC/射灯-绿", ManualWritableWriter, "射灯-绿"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "18.1", "IO4/中控写PLC/射灯-红", ManualWritableWriter, "射灯-红"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "18.2", "IO4/中控写PLC/预留KA3", ManualWritableWriter, "预留KA3"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "18.3", "IO4/中控写PLC/人员通行按钮指示灯", ManualWritableWriter, "人员通行按钮指示灯"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "18.4", "IO4/中控写PLC/AGV通行按钮指示灯", ManualWritableWriter, "AGV通行按钮指示灯"),

                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "20.0", "心跳/PLC心跳", "PLC", "PLC心跳"),
                CreateTemplateSignal(ipAddress, dbBlock, "Bool", "20.1", "心跳/中控心跳", ManualWritableWriter, "中控心跳")
            };
        }

        /// <summary>
        /// 创建模板信号实体。
        /// </summary>
        private static RCS_PlcSignal CreateTemplateSignal(
            string ipAddress,
            string dbBlock,
            string dataType,
            string offset,
            string name,
            string writer,
            string remark)
        {
            return new RCS_PlcSignal
            {
                PlcDeviceId = ipAddress,
                DataType = dataType,
                Offset = offset,
                Name = name,
                Writer = writer,
                CurrentValue = "0",
                Remark = remark,
                PLCTypeDb = dbBlock
            };
        }

        /// <summary>
        /// 统一规范化 DB 块地址，确保 DB1/1 这两种写法最终落成同一个值。
        /// </summary>
        private static string NormalizeDbBlock(string moduleAddress)
        {
            var normalizedValue = string.IsNullOrWhiteSpace(moduleAddress)
                ? DefaultDbBlock
                : moduleAddress.Trim().ToUpperInvariant();

            return normalizedValue.StartsWith("DB", StringComparison.OrdinalIgnoreCase)
                ? normalizedValue
                : $"DB{normalizedValue}";
        }

        /// <summary>
        /// 仅允许“中控写PLC”信号做人工写入或重置。
        /// 这样即便前端被绕过，后端也能兜底拦截只读信号的误操作。
        /// </summary>
        private static void ValidateManualWritableSignal(RCS_PlcSignal signal)
        {
            if (signal == null)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            if (!string.Equals(signal.Writer, ManualWritableWriter, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("仅允许对“中控写PLC”信号执行写入或重置操作。");
            }
        }

        /// <summary>
        /// 生成信号幂等导入的唯一键。
        /// </summary>
        private static string BuildSignalIdentityKey(RCS_PlcSignal signal)
        {
            if (signal == null)
            {
                return string.Empty;
            }

            return $"{signal.PLCTypeDb}|{signal.Offset}|{signal.Name}";
        }
        #endregion
    }
}
