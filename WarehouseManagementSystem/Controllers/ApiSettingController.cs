using System.Diagnostics;
using System.IO;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Services;
using WarehouseManagementSystem.Services.Ndc;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/setting")]
    /// <summary>
    /// 系统设置与系统状态接口（含数据库备份、NDC 状态查询等）。
    /// </summary>
    public class ApiSettingController : ControllerBase
    {
        private readonly IDatabaseService _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiSettingController> _logger;
        private readonly IServiceToggleService _serviceToggleService;
        private readonly AciAppManager _aciAppManager;

        public ApiSettingController(
            IDatabaseService db,
            IConfiguration configuration,
            ILogger<ApiSettingController> logger,
            IServiceToggleService serviceToggleService,
            AciAppManager aciAppManager)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
            _serviceToggleService = serviceToggleService;
            _aciAppManager = aciAppManager;
        }

        private async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
        {
            // 启动时或首次访问时确保默认设置项存在。
            await _serviceToggleService.EnsureDefaultSettingsAsync(cancellationToken);
        }

        [HttpGet]
        /// <summary>
        /// 获取全部系统设置项。
        /// </summary>
        public async Task<IActionResult> GetAllSettings(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                using var connection = _db.CreateConnection();
                var settings = await connection.QueryAsync<SystemSettingDto>(
                    "SELECT [Key], [Value], [Description], [UpdatedAt] FROM SystemSettings ORDER BY [Key]");

                return Ok(new { success = true, data = settings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统设置失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{key}")]
        /// <summary>
        /// 根据 Key 获取单个系统设置项。
        /// </summary>
        public async Task<IActionResult> GetSetting(string key, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                using var connection = _db.CreateConnection();
                var setting = await connection.QueryFirstOrDefaultAsync<SystemSettingDto>(
                    "SELECT [Key], [Value], [Description], [UpdatedAt] FROM SystemSettings WHERE [Key] = @Key",
                    new { Key = key });

                if (setting == null)
                {
                    return NotFound(new { success = false, message = "设置不存在" });
                }

                return Ok(new { success = true, data = setting });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取设置 {Key} 失败", key);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{key}")]
        /// <summary>
        /// 更新（或新增）指定系统设置项。
        /// </summary>
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                var value = request.Value ?? string.Empty;

                using var connection = _db.CreateConnection();
                var affected = await connection.ExecuteAsync(@"
                    UPDATE SystemSettings
                    SET [Value] = @Value,
                        [UpdatedAt] = GETDATE()
                    WHERE [Key] = @Key",
                    new { Key = key, Value = value });

                // 若不存在则执行插入，形成简单 Upsert。
                if (affected == 0)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO SystemSettings ([Key], [Value], [Description], [UpdatedAt])
                        VALUES (@Key, @Value, '用户自定义设置', GETDATE())",
                        new { Key = key, Value = value });
                }

                // 设置变更后主动失效缓存，避免读取到旧值。
                _serviceToggleService.Invalidate(key);

                _logger.LogInformation("系统设置已更新：{Key} = {Value}", key, value);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新设置 {Key} 失败", key);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("system-info")]
        /// <summary>
        /// 获取系统运行信息（系统名称、运行时长、内存占用等）。
        /// </summary>
        public async Task<IActionResult> GetSystemInfo(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                var systemName = "仓库管理系统";

                try
                {
                    using var connection = _db.CreateConnection();
                    systemName = await connection.QueryFirstOrDefaultAsync<string>(
                        "SELECT [Value] FROM SystemSettings WHERE [Key] = 'SystemName'") ?? systemName;
                }
                catch
                {
                }

                var systemInfo = new
                {
                    systemName,
                    version = "1.0.0",
                    uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss"),
                    cpuUsage = "N/A",
                    memoryUsage = $"{GC.GetTotalMemory(false) / 1024 / 1024}MB"
                };

                return Ok(new { success = true, data = systemInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统信息失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("ndc-status")]
        /// <summary>
        /// 获取 NDC 通讯状态（仅 SystemType = NDC 时显示并判断连接状态）。
        /// </summary>
        public async Task<IActionResult> GetNdcStatus(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);

                using var connection = _db.CreateConnection();
                var systemType = await connection.QueryFirstOrDefaultAsync<string>(
                    new CommandDefinition(
                        "SELECT [Value] FROM SystemSettings WHERE [Key] = 'SystemType'",
                        cancellationToken: cancellationToken)) ?? "Heartbeat";

                var isNdc = systemType.Trim().Equals("NDC", StringComparison.OrdinalIgnoreCase);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        systemType,
                        visible = isNdc,
                        connected = isNdc && _aciAppManager.AciClient.Connected
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 NDC 通讯状态失败");
                return Ok(new
                {
                    success = false,
                    message = ex.Message,
                    data = new
                    {
                        systemType = "Unknown",
                        visible = false,
                        connected = false
                    }
                });
            }
        }

        [HttpPost("backup")]
        /// <summary>
        /// 触发数据库备份并返回备份文件路径。
        /// </summary>
        public async Task<IActionResult> BackupDatabase(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _db.CreateConnection();
                var dbName = connection.Database;

                var backupFolder = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT [Value] FROM SystemSettings WHERE [Key] = 'BackupPath'");

                if (string.IsNullOrWhiteSpace(backupFolder))
                {
                    backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                }

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                var fileName = $"{dbName}_{DateTime.Now:yyyyMMddHHmmss}.bak";
                var backupPath = Path.Combine(backupFolder, fileName);

                var sql = $"BACKUP DATABASE [{dbName}] TO DISK = @BackupPath";
                await connection.ExecuteAsync(new CommandDefinition(sql, new { BackupPath = backupPath }, cancellationToken: cancellationToken));

                return Ok(new { success = true, message = "数据库备份成功", path = backupPath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库备份失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("open-backup-folder")]
        /// <summary>
        /// 打开数据库备份目录（服务端执行 explorer.exe）。
        /// </summary>
        public async Task<IActionResult> OpenBackupFolder(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _db.CreateConnection();
                var backupFolder = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT [Value] FROM SystemSettings WHERE [Key] = 'BackupPath'");

                if (string.IsNullOrWhiteSpace(backupFolder))
                {
                    backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                }

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                Process.Start("explorer.exe", backupFolder);
                return Ok(new { success = true, message = "已打开备份文件夹" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开备份文件夹失败");
                return StatusCode(500, new { success = false, message = "无法打开备份文件夹: " + ex.Message });
            }
        }

        [HttpGet("database-status")]
        /// <summary>
        /// 获取数据库状态：连接、容量、表数量和最近备份时间等。
        /// </summary>
        public async Task<IActionResult> GetDatabaseStatus(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _db.CreateConnection();
                var dbName = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME()", cancellationToken: cancellationToken));

                var sizeQuery = @"
                    SELECT CAST(SUM(size) * 8.0 / 1024 AS DECIMAL(18,2))
                    FROM sys.master_files
                    WHERE database_id = DB_ID(@DbName)";
                var size = await connection.QueryFirstOrDefaultAsync<decimal?>(new CommandDefinition(sizeQuery, new { DbName = dbName }, cancellationToken: cancellationToken)) ?? 0;

                var tableCount = await connection.QueryFirstOrDefaultAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM sys.tables", cancellationToken: cancellationToken));

                var lastBackupQuery = @"
                    SELECT MAX(backup_finish_date)
                    FROM msdb.dbo.backupset
                    WHERE database_name = @DbName";
                var lastBackup = await connection.QueryFirstOrDefaultAsync<DateTime?>(new CommandDefinition(lastBackupQuery, new { DbName = dbName }, cancellationToken: cancellationToken));

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                var builder = new SqlConnectionStringBuilder(connStr);

                var backupPath = await connection.QueryFirstOrDefaultAsync<string>(
                    new CommandDefinition("SELECT [Value] FROM SystemSettings WHERE [Key] = 'BackupPath'", cancellationToken: cancellationToken));
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                }

                var status = new
                {
                    connected = true,
                    databaseName = dbName,
                    server = builder.DataSource,
                    user = builder.UserID,
                    backupPath,
                    size = $"{size} MB",
                    tableCount,
                    lastBackup = lastBackup?.ToString("yyyy-MM-dd HH:mm:ss") ?? "无备份记录"
                };

                return Ok(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取数据库状态失败");
                return Ok(new
                {
                    success = false,
                    message = ex.Message,
                    data = new
                    {
                        connected = false,
                        databaseName = "Unknown",
                        server = "Unknown",
                        user = "Unknown",
                        backupPath = string.Empty,
                        size = "Unknown",
                        tableCount = 0,
                        lastBackup = "Unknown"
                    }
                });
            }
        }

        [HttpGet("connection")]
        /// <summary>
        /// 返回连接配置展示信息（前端展示用）。
        /// </summary>
        public IActionResult GetConnectionSettings()
        {
            return Ok(new { success = true, data = new { ipAddress = "127.0.0.1", port = 1433, database = "WMS", username = "sa" } });
        }

        [HttpPost("connection")]
        /// <summary>
        /// 出于安全策略，禁止 Web 端修改数据库连接配置。
        /// </summary>
        public IActionResult SaveConnectionSettings([FromBody] dynamic request)
        {
            return BadRequest(new { success = false, message = "出于安全考虑，Web 端不再支持修改数据库连接设置，请联系管理员修改配置文件。" });
        }

        [HttpPost("test-connection")]
        /// <summary>
        /// 连接测试占位接口（当前固定返回成功）。
        /// </summary>
        public IActionResult TestDatabaseConnection([FromBody] dynamic request)
        {
            return Ok(new { success = true, data = true });
        }

        public class SystemSettingDto
        {
            /// <summary>设置键。</summary>
            public string Key { get; set; }
            /// <summary>设置值。</summary>
            public string Value { get; set; }
            /// <summary>设置描述。</summary>
            public string Description { get; set; }
            /// <summary>最后更新时间。</summary>
            public DateTime UpdatedAt { get; set; }
        }

        public class UpdateSettingRequest
        {
            /// <summary>待更新的设置值。</summary>
            public string Value { get; set; }
        }
    }
}
