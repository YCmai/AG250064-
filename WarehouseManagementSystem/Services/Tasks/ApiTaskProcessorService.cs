using WarehouseManagementSystem.Models.Rcs;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Concurrent;

namespace WarehouseManagementSystem.Services
{
    /// <summary>
    /// API 任务处理服务。
    /// 负责轮询待执行的接口任务，并在目标接口可达时发起请求。
    /// </summary>
    public class ApiTaskProcessorService : BackgroundService
    {
        private readonly ILogger<ApiTaskProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IServiceToggleService _serviceToggleService;
        private readonly TimeSpan _pollingInterval;
        private readonly TimeSpan _networkCheckInterval = TimeSpan.FromSeconds(10);
        private readonly ConcurrentDictionary<string, bool> _endpointNetworkStatus = new();
        private string _defaultEndpoint;

        public ApiTaskProcessorService(
            ILogger<ApiTaskProcessorService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IServiceToggleService serviceToggleService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _serviceToggleService = serviceToggleService;

            var pollingIntervalSeconds = _configuration.GetValue<int>("ApiTask:PollingIntervalSeconds", 5);
            _pollingInterval = TimeSpan.FromSeconds(pollingIntervalSeconds);
            _defaultEndpoint = _configuration["ApiTask:DefaultEndpoint"] ?? string.Empty;

            _logger.LogInformation("API 任务处理服务已初始化，轮询间隔 {Seconds} 秒", _pollingInterval.TotalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _serviceToggleService.EnsureDefaultSettingsAsync(stoppingToken);

            _logger.LogInformation("API 任务处理服务已启动");
            _ = NetworkMonitoringAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var enabled = await _serviceToggleService.IsEnabledAsync(
                        ServiceSettingKeys.ApiTaskProcessorEnabled,
                        true,
                        stoppingToken);

                    if (!enabled)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    await ProcessPendingTasksAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 API 任务时发生异常");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("API 任务处理服务已停止");
        }

        /// <summary>
        /// 后台监控各接口端点的网络可达性。
        /// 如果服务本身被关闭，则只保留轻量等待，不继续做 ping 检查。
        /// </summary>
        private async Task NetworkMonitoringAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("API 服务网络监控任务已启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var enabled = await _serviceToggleService.IsEnabledAsync(
                        ServiceSettingKeys.ApiTaskProcessorEnabled,
                        true,
                        stoppingToken);

                    if (!enabled)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(_defaultEndpoint))
                    {
                        await Task.Delay(_networkCheckInterval, stoppingToken);
                        continue;
                    }

                    var defaultUri = new Uri(_defaultEndpoint);
                    var defaultHost = defaultUri.Host;
                    if (!_endpointNetworkStatus.ContainsKey(defaultHost))
                    {
                        _endpointNetworkStatus[defaultHost] = await CheckNetworkConnectionAsync(defaultHost);
                        _logger.LogInformation(
                            "默认 API 端点 {Host} 的初始网络状态：{Status}",
                            defaultHost,
                            _endpointNetworkStatus[defaultHost] ? "在线" : "离线");
                    }

                    foreach (var endpoint in _endpointNetworkStatus.Keys.ToList())
                    {
                        var previousStatus = _endpointNetworkStatus.GetValueOrDefault(endpoint, false);
                        var currentStatus = await CheckNetworkConnectionAsync(endpoint);
                        _endpointNetworkStatus[endpoint] = currentStatus;

                        if (previousStatus == currentStatus)
                        {
                            continue;
                        }

                        if (currentStatus)
                        {
                            _logger.LogInformation("API 端点 {Host} 的网络连接已恢复", endpoint);
                        }
                        else
                        {
                            _logger.LogWarning("API 端点 {Host} 的网络连接已断开", endpoint);
                        }
                    }

                    await Task.Delay(_networkCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "网络连接监控任务发生异常");
                    await Task.Delay(_networkCheckInterval, stoppingToken);
                }
            }

            _logger.LogInformation("API 服务网络监控任务已停止");
        }

        private async Task<bool> CheckNetworkConnectionAsync(string hostNameOrAddress)
        {
            try
            {
                using var pinger = new Ping();
                var reply = await pinger.SendPingAsync(hostNameOrAddress, 1000);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查主机 {Host} 的网络连接时出错", hostNameOrAddress);
                return false;
            }
        }

        private async Task ProcessPendingTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            using var connection = dbService.CreateConnection();

            var pendingTasks = await connection.QueryAsync<NdcApiTask>(@"
                SELECT *
                FROM NdcApiTask
                WHERE Excute = 0
                ORDER BY CreateTime ASC");

            var taskList = pendingTasks.ToList();
            if (taskList.Count == 0)
            {
                return;
            }

            _logger.LogInformation("发现 {Count} 个待处理的 API 任务", taskList.Count);

            foreach (var task in taskList)
            {
                try
                {
                    var endpoint = GetTaskEndpoint(task);
                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        _logger.LogWarning("任务 {TaskId} 未配置 API 端点，跳过处理", task.ID);
                        continue;
                    }

                    var uri = new Uri(endpoint);
                    var host = uri.Host;

                    if (!_endpointNetworkStatus.ContainsKey(host))
                    {
                        var isConnected = await CheckNetworkConnectionAsync(host);
                        _endpointNetworkStatus[host] = isConnected;
                        _logger.LogInformation("新 API 端点 {Host} 的网络状态：{Status}", host, isConnected ? "在线" : "离线");
                    }

                    if (!_endpointNetworkStatus[host])
                    {
                        _logger.LogWarning("API 端点 {Host} 不可达，跳过任务 {TaskId}", host, task.ID);
                        continue;
                    }

                    var success = await ProcessTaskAsync(task);
                    await UpdateTaskStatusAsync(connection, task.ID, success, success ? "执行成功" : "执行失败");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 API 任务 {TaskId} 时发生异常", task.ID);
                    await UpdateTaskStatusAsync(connection, task.ID, false, $"执行异常: {ex.Message}");
                }
            }
        }

        private string GetTaskEndpoint(NdcApiTask task)
        {
            return _defaultEndpoint;
        }

        private async Task<bool> ProcessTaskAsync(NdcApiTask task)
        {
            _logger.LogInformation("开始处理 API 任务：ID={TaskId}, 类型={TaskType}, 任务编号={TaskCode}", task.ID, task.TaskType, task.TaskCode);

            return task.TaskType switch
            {
                1 => await SendHttpRequestAsync(task),
                2 => LogUnsupportedTask(task, "WebSocket"),
                3 => LogUnsupportedTask(task, "TCP/IP"),
                4 => LogUnsupportedTask(task, "UDP"),
                _ => LogUnknownTask(task)
            };
        }

        private bool LogUnsupportedTask(NdcApiTask task, string typeName)
        {
            _logger.LogWarning("暂不支持 {TypeName} 类型的任务 {TaskId}", typeName, task.ID);
            return false;
        }

        private bool LogUnknownTask(NdcApiTask task)
        {
            _logger.LogWarning("未知的任务类型 {TaskType}，任务 ID：{TaskId}", task.TaskType, task.ID);
            return false;
        }

        private async Task<bool> SendHttpRequestAsync(NdcApiTask task)
        {
            try
            {
                var url = GetTaskEndpoint(task);
                if (string.IsNullOrWhiteSpace(url))
                {
                    _logger.LogError("未配置 API 端点");
                    return false;
                }

                _logger.LogInformation("发送 API 请求：{Url}, 任务 ID：{TaskId}", url, task.ID);

                var requestData = new
                {
                    taskCode = task.TaskCode,
                    taskType = task.TaskType
                };

                var jsonContent = JsonConvert.SerializeObject(requestData);

                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("ApiTask:TimeoutSeconds", 30))
                };

                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("API 请求失败，状态码：{StatusCode}, 任务 ID：{TaskId}", response.StatusCode, task.ID);
                    return false;
                }

                var resultJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("API 请求响应：{Result}, 任务 ID：{TaskId}", resultJson, task.ID);

                try
                {
                    var result = JsonConvert.DeserializeObject<dynamic>(resultJson);
                    return result != null &&
                           (string.Equals(result.status?.ToString(), "Success", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(result.success?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析 API 响应失败，任务 ID：{TaskId}，原始响应：{Result}", task.ID, resultJson);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送 API 请求异常，任务 ID：{TaskId}", task.ID);
                return false;
            }
        }

        private async Task UpdateTaskStatusAsync(System.Data.IDbConnection connection, int taskId, bool success, string message)
        {
            try
            {
                await connection.ExecuteAsync(@"
                    UPDATE NdcApiTask
                    SET Excute = @Excute,
                        Message = @Message
                    WHERE ID = @ID",
                    new
                    {
                        ID = taskId,
                        Excute = success,
                        Message = message
                    });

                _logger.LogInformation("已更新 API 任务状态：ID={TaskId}, 成功={Success}, 消息={Message}", taskId, success, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新 API 任务状态失败，任务 ID：{TaskId}", taskId);
            }
        }
    }
}

