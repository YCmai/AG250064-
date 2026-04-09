using System.Diagnostics;
using System.IO;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WarehouseManagementSystem.Data;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/setting")]
    public class ApiSettingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiSettingController> _logger;
        private readonly IServiceToggleService _serviceToggleService;

        public ApiSettingController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<ApiSettingController> logger,
            IServiceToggleService serviceToggleService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _serviceToggleService = serviceToggleService;
        }

        private async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
        {
            await _serviceToggleService.EnsureDefaultSettingsAsync(cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSettings(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                using var connection = _context.GetConnection();
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
        public async Task<IActionResult> GetSetting(string key, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                using var connection = _context.GetConnection();
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
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                var value = request.Value ?? string.Empty;

                using var connection = _context.GetConnection();
                var affected = await connection.ExecuteAsync(@"
                    UPDATE SystemSettings
                    SET [Value] = @Value,
                        [UpdatedAt] = GETDATE()
                    WHERE [Key] = @Key",
                    new { Key = key, Value = value });

                if (affected == 0)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO SystemSettings ([Key], [Value], [Description], [UpdatedAt])
                        VALUES (@Key, @Value, '用户自定义设置', GETDATE())",
                        new { Key = key, Value = value });
                }

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
        public async Task<IActionResult> GetSystemInfo(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureTableExistsAsync(cancellationToken);
                var systemName = "仓库管理系统";

                try
                {
                    using var connection = _context.GetConnection();
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

        [HttpPost("backup")]
        public async Task<IActionResult> BackupDatabase(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _context.GetConnection();
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
        public async Task<IActionResult> OpenBackupFolder(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _context.GetConnection();
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
        public async Task<IActionResult> GetDatabaseStatus(CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _context.GetConnection();
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
        public IActionResult GetConnectionSettings()
        {
            return Ok(new { success = true, data = new { ipAddress = "127.0.0.1", port = 1433, database = "WMS", username = "sa" } });
        }

        [HttpPost("connection")]
        public IActionResult SaveConnectionSettings([FromBody] dynamic request)
        {
            return BadRequest(new { success = false, message = "出于安全考虑，Web 端不再支持修改数据库连接设置，请联系管理员修改配置文件。" });
        }

        [HttpPost("test-connection")]
        public IActionResult TestDatabaseConnection([FromBody] dynamic request)
        {
            return Ok(new { success = true, data = true });
        }

        public class SystemSettingDto
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public string Description { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        public class UpdateSettingRequest
        {
            public string Value { get; set; }
        }
    }
}
