using System.Net.Sockets;
using System.Net;
using NModbus;
using System.Data;
using WarehouseManagementSystem.Models.IO;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Db;
using Dapper;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System;
using WarehouseManagementSystem.Models.Ndc;

public interface ITaskService
{
    Task<(List<NdcUserTask> Items, int TotalItems)> GetUserTasks(
        int page = 1, 
        int pageSize = 10, 
        DateTime? filterDate = null, 
        DateTime? endDate = null);
    Task<Dictionary<int, int>> GetTaskStatusCounts(DateTime? filterDate = null, DateTime? endDate = null);
    Task<(bool success, string message)> CancelTask(int id);
    Task<bool> CheckDuplicateTask(string sourcePosition, string targetPosition);
    Task<NdcUserTask> GetUserTaskById(int id);
    Task<string> GetSystemType();
}

public class TaskService : ITaskService
{
    private readonly IDatabaseService _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        IDatabaseService db,
        IConfiguration configuration,
        ILogger<TaskService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(List<NdcUserTask> Items, int TotalItems)> GetUserTasks(
        int page = 1,
        int pageSize = 10,
        DateTime? filterDate = null,
        DateTime? endDate = null)
    {
        try
        {
            using var conn = _db.CreateConnection();

            var query = "SELECT * FROM RCS_UserTasks WHERE 1=1";
            var countQuery = "SELECT COUNT(*) FROM RCS_UserTasks WHERE 1=1";
            var parameters = new DynamicParameters();

            if (filterDate.HasValue)
            {
                query += " AND creatTime >= @FilterDate";
                countQuery += " AND creatTime >= @FilterDate";
                parameters.Add("@FilterDate", filterDate.Value);
            }

            if (endDate.HasValue)
            {
                query += " AND creatTime <= @EndDate";
                countQuery += " AND creatTime <= @EndDate";
                parameters.Add("@EndDate", endDate.Value.AddDays(1));
            }

            query += " ORDER BY creatTime DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            parameters.Add("@Offset", (page - 1) * pageSize);
            parameters.Add("@PageSize", pageSize);

            var items = await conn.QueryAsync<NdcUserTask>(query, parameters);
            var totalItems = await conn.ExecuteScalarAsync<int>(countQuery, parameters);

            return (items.ToList(), totalItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务列表失败");
            throw;
        }
    }

    private class TaskStatusCountDto
    {
        public int TaskStatus { get; set; }
        public int Count { get; set; }
    }

    public async Task<Dictionary<int, int>> GetTaskStatusCounts(DateTime? filterDate = null, DateTime? endDate = null)
    {
        try
        {
            using var conn = _db.CreateConnection();

            var query = "SELECT taskStatus AS TaskStatus, COUNT(*) AS Count FROM RCS_UserTasks WHERE 1=1";
            var parameters = new DynamicParameters();

            if (filterDate.HasValue)
            {
                query += " AND creatTime >= @FilterDate";
                parameters.Add("@FilterDate", filterDate.Value);
            }

            if (endDate.HasValue)
            {
                query += " AND creatTime <= @EndDate";
                parameters.Add("@EndDate", endDate.Value.AddDays(1));
            }

            query += " GROUP BY taskStatus";

            var results = await conn.QueryAsync<TaskStatusCountDto>(query, parameters);
            
            var dict = new Dictionary<int, int>();
            foreach (var row in results)
            {
                if (dict.ContainsKey(row.TaskStatus))
                    dict[row.TaskStatus] += row.Count;
                else
                    dict[row.TaskStatus] = row.Count;
            }

            return dict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务状态统计失败");
            throw;
        }
    }

    public async Task<NdcUserTask> GetUserTaskById(int id)
    {
        try
        {
            using var conn = _db.CreateConnection();

            var task = await conn.QueryFirstOrDefaultAsync<NdcUserTask>(
                "SELECT * FROM RCS_UserTasks WHERE Id = @Id",
                new { Id = id });

            return task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取任务详情失败: ID={id}");
            return null;
        }
    }

    public async Task<bool> CheckDuplicateTask(string sourcePosition, string targetPosition)
    {
        try
        {
            var systemType = await GetSystemType();
            using var conn = _db.CreateConnection();
            string query;
            var parameters = new DynamicParameters();
            parameters.Add("@SourcePosition", sourcePosition);
            parameters.Add("@TargetPosition", targetPosition);

            if (systemType == "NDC")
            {
                // NDC: Check if any task is NOT finished and NOT canceled
                // Finished: 11, 53, 32
                // Canceled: >= 30 (except 32, 53)
                // Active = taskStatus < 30 AND taskStatus != 11
                query = @"
                    SELECT COUNT(*) FROM RCS_UserTasks 
                    WHERE sourcePosition = @SourcePosition 
                    AND targetPosition = @TargetPosition 
                    AND taskStatus < 30 
                    AND taskStatus != 11";
            }
            else
            {
                query = @"
                    SELECT COUNT(*) FROM RCS_UserTasks 
                    WHERE sourcePosition = @SourcePosition 
                    AND targetPosition = @TargetPosition 
                    AND (taskStatus & @FinishedFlag) = 0 
                    AND (taskStatus & @CancelFlag) = 0";
                parameters.Add("@FinishedFlag", (int)TaskStatuEnum.Finished);
                parameters.Add("@CancelFlag", (int)TaskStatuEnum.Cancel);
            }

            var count = await conn.ExecuteScalarAsync<int>(query, parameters);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查重复任务失败");
            return false;
        }
    }

    public async Task<string> GetSystemType()
    {
        try 
        {
            return "NDC";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统类型失败");
            return "NDC";
        }
    }

    public async Task<(bool success, string message)> CancelTask(int id)
    {
        try
        {
            using var conn = _db.CreateConnection();
            var systemType = await GetSystemType();
            
            // 检查任务是否存在且可以取消
            var task = await conn.QueryFirstOrDefaultAsync<NdcUserTask>(
                "SELECT * FROM RCS_UserTasks WHERE Id = @Id", 
                new { Id = id });

            if (task == null)
            {
                return (false, "任务不存在");
            }

            if (systemType == "NDC")
            {
                // NDC Logic
                // Cannot cancel if Finished (11, 53, 32) or already Canceled (>= 30)
                // 32, 53 are >= 30, so check >= 30 or == 11
                if ((int)task.taskStatus >= 30 || (int)task.taskStatus == 11)
                {
                    return (false, "已完成或已取消的任务不能取消");
                }
                
                // Cancel by setting IsCancelled to 1 (let the background service handle the rest)
                var result = await conn.ExecuteAsync(@"
                    UPDATE RCS_UserTasks 
                    SET IsCancelled = 1
                    WHERE Id = @Id",
                    new { Id = id });
                    
                return (result > 0, result > 0 ? "任务取消成功" : "任务取消失败");
            }
            else
            {
                // Heartbeat Logic
                // Check if task is already finished
                if (((int)task.taskStatus & (int)TaskStatuEnum.Finished) != 0)
                {
                    return (false, "已完成的任务不能取消");
                }

                // Check if task is already canceled
                if (((int)task.taskStatus & (int)TaskStatuEnum.Cancel) != 0)
                {
                    return (false, "任务已经被取消");
                }

                // Set the Cancel flag
                var newStatus = (int)task.taskStatus | (int)TaskStatuEnum.Cancel;

                // 更新任务状态为已取消
                var result = await conn.ExecuteAsync(@"
                    UPDATE RCS_UserTasks 
                    SET IsCancelled = 1,
                        taskStatus = @TaskStatus
                    WHERE Id = @Id",
                    new { 
                        Id = id,
                        TaskStatus = newStatus
                    });
                    
                return (result > 0, result > 0 ? "任务取消成功" : "任务取消失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消任务失败");
            return (false, "系统错误，请联系管理员");
        }
    }

    private async Task RevertCancelStatus(int id)
    {
        try
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE RCS_UserTasks 
                SET IsCancelled = 0 
                WHERE Id = @Id",
                new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复任务取消状态失败");
        }
    }

    private (string baseUrl, string port, string http) GetConnectionParameters()
    {
        try
        {
            var baseUrl = _configuration["ConnectionStrings:IPAddress"];
            var port = _configuration["ConnectionStrings:Port"];
            var http = _configuration["ConnectionStrings:Http"];
            return (baseUrl, port, http);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取连接参数失败");
            return (string.Empty, string.Empty, string.Empty);
        }
    }
}


