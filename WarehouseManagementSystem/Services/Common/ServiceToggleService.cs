using System.Collections.Concurrent;
using Dapper;
using WarehouseManagementSystem.Db;

namespace WarehouseManagementSystem.Services
{
    /// <summary>
    /// 后台服务开关键名定义。
    /// 统一放在这里，避免前后端和各个服务中散落硬编码字符串。
    /// </summary>
    public static class ServiceSettingKeys
    {
        public const string IOProcessorEnabled = "IOProcessorEnabled";
        public const string PlcCommunicationEnabled = "PlcCommunicationEnabled";
        public const string ApiTaskProcessorEnabled = "ApiTaskProcessorEnabled";
    }

    public interface IServiceToggleService
    {
        Task EnsureDefaultSettingsAsync(CancellationToken cancellationToken = default);
        Task<bool> IsEnabledAsync(string key, bool defaultValue = true, CancellationToken cancellationToken = default);
        void Invalidate(string key);
    }

    /// <summary>
    /// 提供系统设置中的后台服务开关读取能力。
    /// 这里做了一个很短的本地缓存，避免多个后台循环高频访问数据库。
    /// </summary>
    public class ServiceToggleService : IServiceToggleService
    {
        private static readonly IReadOnlyDictionary<string, (string Value, string Description)> DefaultSettings =
            new Dictionary<string, (string Value, string Description)>
            {
                ["SystemName"] = ("仓库管理系统", "系统显示名称"),
                ["SystemType"] = ("Heartbeat", "系统类型 (Heartbeat/NDC)"),
                ["TaskTimeout"] = ("300", "任务超时时间(秒)"),
                ["RefreshInterval"] = ("5", "自动刷新间隔(秒)"),
                ["MaxRetries"] = ("3", "最大重试次数"),
                ["Theme"] = ("light", "系统主题"),
                ["Language"] = ("zh-CN", "系统语言"),
                [ServiceSettingKeys.IOProcessorEnabled] = ("true", "是否启用 IO 服务"),
                [ServiceSettingKeys.PlcCommunicationEnabled] = ("true", "是否启用 PLC 服务（含通讯、任务处理、心跳）"),
                [ServiceSettingKeys.ApiTaskProcessorEnabled] = ("true", "是否启用接口任务处理服务")
            };

        private readonly IDatabaseService _databaseService;
        private readonly ILogger<ServiceToggleService> _logger;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private readonly ConcurrentDictionary<string, (bool Value, DateTime ExpireAt)> _cache = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(2);
        private volatile bool _initialized;

        public ServiceToggleService(IDatabaseService databaseService, ILogger<ServiceToggleService> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        /// <summary>
        /// 确保系统设置表以及默认配置存在。
        /// 即使前端还没有打开过设置页，后台服务也能安全读取开关。
        /// </summary>
        public async Task EnsureDefaultSettingsAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            await _initializationLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                {
                    return;
                }

                using var conn = _databaseService.CreateConnection();
                await conn.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'SystemSettings' AND xtype = 'U')
                    BEGIN
                        CREATE TABLE [dbo].[SystemSettings] (
                            [Key] NVARCHAR(50) NOT NULL PRIMARY KEY,
                            [Value] NVARCHAR(MAX) NULL,
                            [Description] NVARCHAR(200) NULL,
                            [UpdatedAt] DATETIME2 DEFAULT GETDATE()
                        );
                    END");

                foreach (var setting in DefaultSettings)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await conn.ExecuteAsync(@"
                        IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE [Key] = @Key)
                        BEGIN
                            INSERT INTO SystemSettings ([Key], [Value], [Description], [UpdatedAt])
                            VALUES (@Key, @Value, @Description, GETDATE())
                        END",
                        new
                        {
                            Key = setting.Key,
                            Value = setting.Value.Value,
                            Description = setting.Value.Description
                        });
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化系统设置默认值失败");
                throw;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        /// 读取指定服务开关。
        /// 支持 true/false、1/0、yes/no、on/off 等常见配置值。
        /// </summary>
        public async Task<bool> IsEnabledAsync(string key, bool defaultValue = true, CancellationToken cancellationToken = default)
        {
            await EnsureDefaultSettingsAsync(cancellationToken);

            if (_cache.TryGetValue(key, out var cached) && cached.ExpireAt > DateTime.UtcNow)
            {
                return cached.Value;
            }

            try
            {
                using var conn = _databaseService.CreateConnection();
                var rawValue = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT [Value] FROM SystemSettings WHERE [Key] = @Key",
                    new { Key = key });

                var enabled = ParseBoolean(rawValue, defaultValue);
                _cache[key] = (enabled, DateTime.UtcNow.Add(_cacheDuration));
                return enabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取服务开关 {SettingKey} 失败，将使用默认值 {DefaultValue}", key, defaultValue);
                return defaultValue;
            }
        }

        /// <summary>
        /// 主动清理指定开关的缓存。
        /// 当设置页刚保存完开关时，后台服务下次读取将立即命中新值，而不是继续使用旧缓存。
        /// </summary>
        public void Invalidate(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _cache.TryRemove(key, out _);
        }

        private static bool ParseBoolean(string? rawValue, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            return rawValue.Trim().ToLowerInvariant() switch
            {
                "1" => true,
                "true" => true,
                "yes" => true,
                "on" => true,
                "0" => false,
                "false" => false,
                "no" => false,
                "off" => false,
                _ => defaultValue
            };
        }
    }
}
