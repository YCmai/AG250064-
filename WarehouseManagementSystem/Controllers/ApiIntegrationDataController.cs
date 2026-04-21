using Dapper;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers;

/// <summary>
/// 集成数据查询控制器，用于前端查看 AGV 对接相关落库数据。
/// </summary>
[ApiController]
[Route("api/integration-data")]
public class ApiIntegrationDataController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<ApiIntegrationDataController> _logger;

    public ApiIntegrationDataController(IDatabaseService db, ILogger<ApiIntegrationDataController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 查询工单落库数据（RCS_WorkOrder）。
    /// </summary>
    [HttpGet("workorders")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<RCS_WorkOrder>>>> GetWorkOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? orderNumber = null)
    {
        try
        {
            NormalizePage(ref page, ref pageSize);

            using var connection = _db.CreateConnection();
            var parameters = new DynamicParameters();
            var whereSql = BuildLikeWhere("OrderNumber", "OrderNumber", orderNumber, parameters);
            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            var total = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM RCS_WorkOrder {whereSql};", parameters);

            var items = (await connection.QueryAsync<RCS_WorkOrder>(
                $@"SELECT *
                   FROM RCS_WorkOrder
                   {whereSql}
                   ORDER BY ID DESC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", parameters)).ToList();

            return Ok(ApiResponseHelper.Success(PaginatedResponse<RCS_WorkOrder>.Create(items, total, page, pageSize), "查询成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 RCS_WorkOrder 失败");
            return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<RCS_WorkOrder>>("查询失败"));
        }
    }

    /// <summary>
    /// 查询 AGV 指令收件箱主表（RCS_AgvCommandInbox）。
    /// </summary>
    [HttpGet("agv-command-inbox")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<RCS_AgvCommandInbox>>>> GetAgvCommandInbox(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? taskNumber = null)
    {
        try
        {
            NormalizePage(ref page, ref pageSize);

            using var connection = _db.CreateConnection();
            var parameters = new DynamicParameters();
            var whereSql = BuildLikeWhere("TaskNumber", "TaskNumber", taskNumber, parameters);
            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            var total = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM RCS_AgvCommandInbox {whereSql};", parameters);

            var items = (await connection.QueryAsync<RCS_AgvCommandInbox>(
                $@"SELECT *
                   FROM RCS_AgvCommandInbox
                   {whereSql}
                   ORDER BY ID DESC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", parameters)).ToList();

            return Ok(ApiResponseHelper.Success(PaginatedResponse<RCS_AgvCommandInbox>.Create(items, total, page, pageSize), "查询成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 RCS_AgvCommandInbox 失败");
            return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<RCS_AgvCommandInbox>>("查询失败"));
        }
    }

    /// <summary>
    /// 查询 AGV 指令收件箱子表（RCS_AgvCommandInboxItems）。
    /// </summary>
    [HttpGet("agv-command-inbox-items")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<AgvCommandInboxItemView>>>> GetAgvCommandInboxItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? inboxId = null,
        [FromQuery] string? taskNumber = null)
    {
        try
        {
            NormalizePage(ref page, ref pageSize);

            using var connection = _db.CreateConnection();
            var parameters = new DynamicParameters();
            var whereConditions = new List<string>();

            if (inboxId.HasValue)
            {
                whereConditions.Add("i.InboxId = @InboxId");
                parameters.Add("InboxId", inboxId.Value);
            }

            if (!string.IsNullOrWhiteSpace(taskNumber))
            {
                whereConditions.Add("h.TaskNumber LIKE @TaskNumber");
                parameters.Add("TaskNumber", $"%{taskNumber.Trim()}%");
            }

            var whereSql = whereConditions.Count == 0
                ? string.Empty
                : $"WHERE {string.Join(" AND ", whereConditions)}";

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            var total = await connection.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(1)
                   FROM RCS_AgvCommandInboxItems i
                   INNER JOIN RCS_AgvCommandInbox h ON h.ID = i.InboxId
                   {whereSql};", parameters);

            var items = (await connection.QueryAsync<AgvCommandInboxItemView>(
                $@"SELECT
                       i.ID,
                       i.InboxId,
                       h.TaskNumber,
                       i.Seq,
                       i.PalletNumber,
                       i.BinNumber,
                       i.FromStation,
                       i.ToStation,
                       i.TaskType,
                       i.CreateTime
                   FROM RCS_AgvCommandInboxItems i
                   INNER JOIN RCS_AgvCommandInbox h ON h.ID = i.InboxId
                   {whereSql}
                   ORDER BY i.ID DESC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", parameters)).ToList();

            return Ok(ApiResponseHelper.Success(PaginatedResponse<AgvCommandInboxItemView>.Create(items, total, page, pageSize), "查询成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 RCS_AgvCommandInboxItems 失败");
            return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<AgvCommandInboxItemView>>("查询失败"));
        }
    }

    /// <summary>
    /// 查询 AGV 主动上报出站队列（RCS_AgvOutboundQueue）。
    /// </summary>
    [HttpGet("agv-outbound-queue")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<RCS_AgvOutboundQueue>>>> GetAgvOutboundQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? taskNumber = null,
        [FromQuery] int? eventType = null,
        [FromQuery] int? processStatus = null)
    {
        try
        {
            NormalizePage(ref page, ref pageSize);

            using var connection = _db.CreateConnection();
            var parameters = new DynamicParameters();
            var whereConditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(taskNumber))
            {
                whereConditions.Add("TaskNumber LIKE @TaskNumber");
                parameters.Add("TaskNumber", $"%{taskNumber.Trim()}%");
            }

            if (eventType.HasValue)
            {
                whereConditions.Add("EventType = @EventType");
                parameters.Add("EventType", eventType.Value);
            }

            if (processStatus.HasValue)
            {
                whereConditions.Add("ProcessStatus = @ProcessStatus");
                parameters.Add("ProcessStatus", processStatus.Value);
            }

            var whereSql = whereConditions.Count == 0
                ? string.Empty
                : $"WHERE {string.Join(" AND ", whereConditions)}";

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            var total = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM RCS_AgvOutboundQueue {whereSql};", parameters);

            var items = (await connection.QueryAsync<RCS_AgvOutboundQueue>(
                $@"SELECT *
                   FROM RCS_AgvOutboundQueue
                   {whereSql}
                   ORDER BY ID DESC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", parameters)).ToList();

            return Ok(ApiResponseHelper.Success(PaginatedResponse<RCS_AgvOutboundQueue>.Create(items, total, page, pageSize), "查询成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 RCS_AgvOutboundQueue 失败");
            return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<RCS_AgvOutboundQueue>>("查询失败"));
        }
    }

    /// <summary>
    /// 新增 AGV 主动上报出站队列记录。
    /// </summary>
    [HttpPost("agv-outbound-queue")]
    public async Task<ActionResult<ApiResponse<object>>> CreateAgvOutboundQueue([FromBody] AgvOutboundQueueUpsertRequest request)
    {
        var validateError = ValidateAgvOutboundQueueRequest(request);
        if (!string.IsNullOrEmpty(validateError))
        {
            return BadRequest(ApiResponseHelper.Failure<object>(validateError));
        }

        try
        {
            using var connection = _db.CreateConnection();
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM RCS_AgvOutboundQueue WHERE BusinessKey = @BusinessKey;",
                new { request.BusinessKey });
            if (exists > 0)
            {
                return Ok(ApiResponseHelper.Failure<object>("businessKey 已存在，不能重复新增"));
            }

            var now = DateTime.Now;
            await connection.ExecuteAsync(
                @"INSERT INTO RCS_AgvOutboundQueue
                  (
                    EventType, TaskNumber, BusinessKey, RequestBody, ProcessStatus,
                    RetryCount, LastError, NextRetryTime, CreateTime, ProcessTime, UpdateTime
                  )
                  VALUES
                  (
                    @EventType, @TaskNumber, @BusinessKey, @RequestBody, @ProcessStatus,
                    @RetryCount, @LastError, @NextRetryTime, @CreateTime, @ProcessTime, @UpdateTime
                  );",
                new
                {
                    request.EventType,
                    request.TaskNumber,
                    request.BusinessKey,
                    request.RequestBody,
                    request.ProcessStatus,
                    request.RetryCount,
                    request.LastError,
                    request.NextRetryTime,
                    CreateTime = now,
                    request.ProcessTime,
                    UpdateTime = now
                });

            return Ok(ApiResponseHelper.Success<object>(null, "新增成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增 RCS_AgvOutboundQueue 失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>("新增失败"));
        }
    }

    /// <summary>
    /// 编辑 AGV 主动上报出站队列记录。
    /// </summary>
    [HttpPut("agv-outbound-queue/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAgvOutboundQueue([FromRoute] int id, [FromBody] AgvOutboundQueueUpsertRequest request)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponseHelper.Failure<object>("id 非法"));
        }

        var validateError = ValidateAgvOutboundQueueRequest(request);
        if (!string.IsNullOrEmpty(validateError))
        {
            return BadRequest(ApiResponseHelper.Failure<object>(validateError));
        }

        try
        {
            using var connection = _db.CreateConnection();

            var current = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT ID FROM RCS_AgvOutboundQueue WHERE ID = @ID;",
                new { ID = id });
            if (!current.HasValue)
            {
                return Ok(ApiResponseHelper.Failure<object>("记录不存在"));
            }

            var duplicated = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1)
                  FROM RCS_AgvOutboundQueue
                  WHERE BusinessKey = @BusinessKey AND ID <> @ID;",
                new { request.BusinessKey, ID = id });
            if (duplicated > 0)
            {
                return Ok(ApiResponseHelper.Failure<object>("businessKey 已存在，不能重复"));
            }

            await connection.ExecuteAsync(
                @"UPDATE RCS_AgvOutboundQueue
                  SET EventType = @EventType,
                      TaskNumber = @TaskNumber,
                      BusinessKey = @BusinessKey,
                      RequestBody = @RequestBody,
                      ProcessStatus = @ProcessStatus,
                      RetryCount = @RetryCount,
                      LastError = @LastError,
                      NextRetryTime = @NextRetryTime,
                      ProcessTime = @ProcessTime,
                      UpdateTime = @UpdateTime
                  WHERE ID = @ID;",
                new
                {
                    ID = id,
                    request.EventType,
                    request.TaskNumber,
                    request.BusinessKey,
                    request.RequestBody,
                    request.ProcessStatus,
                    request.RetryCount,
                    request.LastError,
                    request.NextRetryTime,
                    request.ProcessTime,
                    UpdateTime = DateTime.Now
                });

            return Ok(ApiResponseHelper.Success<object>(null, "更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 RCS_AgvOutboundQueue 失败，ID={ID}", id);
            return StatusCode(500, ApiResponseHelper.Failure<object>("更新失败"));
        }
    }

    /// <summary>
    /// 删除 AGV 主动上报出站队列记录。
    /// </summary>
    [HttpDelete("agv-outbound-queue/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAgvOutboundQueue([FromRoute] int id)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponseHelper.Failure<object>("id 非法"));
        }

        try
        {
            using var connection = _db.CreateConnection();
            var affected = await connection.ExecuteAsync("DELETE FROM RCS_AgvOutboundQueue WHERE ID = @ID;", new { ID = id });
            if (affected == 0)
            {
                return Ok(ApiResponseHelper.Failure<object>("记录不存在或已删除"));
            }

            return Ok(ApiResponseHelper.Success<object>(null, "删除成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 RCS_AgvOutboundQueue 失败，ID={ID}", id);
            return StatusCode(500, ApiResponseHelper.Failure<object>("删除失败"));
        }
    }

    private static void NormalizePage(ref int page, ref int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;
    }

    private static string BuildLikeWhere(string columnName, string parameterName, string? value, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        parameters.Add(parameterName, $"%{value.Trim()}%");
        return $"WHERE {columnName} LIKE @{parameterName}";
    }

    private static string? ValidateAgvOutboundQueueRequest(AgvOutboundQueueUpsertRequest request)
    {
        if (request == null)
        {
            return "请求体不能为空";
        }

        if (request.EventType < 1 || request.EventType > 3)
        {
            return "eventType 仅允许 1~3";
        }

        if (string.IsNullOrWhiteSpace(request.TaskNumber))
        {
            return "taskNumber 不能为空";
        }

        if (string.IsNullOrWhiteSpace(request.BusinessKey))
        {
            return "businessKey 不能为空";
        }

        if (string.IsNullOrWhiteSpace(request.RequestBody))
        {
            return "requestBody 不能为空";
        }

        if (request.ProcessStatus < 0 || request.ProcessStatus > 3)
        {
            return "processStatus 仅允许 0~3";
        }

        if (request.RetryCount < 0)
        {
            return "retryCount 不能小于 0";
        }

        return null;
    }
}

/// <summary>
/// AGV 指令收件箱子表展示模型（带 taskNumber）。
/// </summary>
public class AgvCommandInboxItemView
{
    public int ID { get; set; }
    public int InboxId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public int Seq { get; set; }
    public string? PalletNumber { get; set; }
    public string? BinNumber { get; set; }
    public string? FromStation { get; set; }
    public string ToStation { get; set; } = string.Empty;
    public int TaskType { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// AGV 主动上报出站队列新增/编辑请求模型。
/// </summary>
public class AgvOutboundQueueUpsertRequest
{
    public int EventType { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string BusinessKey { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public int ProcessStatus { get; set; }
    public int RetryCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime? NextRetryTime { get; set; }
    public DateTime? ProcessTime { get; set; }
}
