using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;

namespace WarehouseManagementSystem.Services.Tasks;

/// <summary>
/// PDA 绑定仓储接口。
/// </summary>
public interface IPdaBindingRepository
{
    /// <summary>
    /// 查询生效中的工单列表。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工单集合。</returns>
    Task<List<RCS_WorkOrder>> GetActiveWorkOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据工单号查询工单。
    /// </summary>
    /// <param name="orderNumber">工单号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工单信息。</returns>
    Task<RCS_WorkOrder?> FindWorkOrderAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询是否已存在相同绑定。
    /// </summary>
    /// <param name="orderNumber">工单号。</param>
    /// <param name="palletNumber">托盘号。</param>
    /// <param name="barcode">SSCC码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否存在。</returns>
    Task<bool> ExistsBindingAsync(string orderNumber, string palletNumber, string barcode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增 PDA 绑定记录。
    /// </summary>
    /// <param name="entity">绑定实体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新记录 ID。</returns>
    Task<int> InsertBindingAsync(RCS_PdaTaskBinding entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据任务请求号查询绑定记录。
    /// </summary>
    /// <param name="requestCode">任务请求号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>绑定记录。</returns>
    Task<RCS_PdaTaskBinding?> FindByRequestCodeAsync(string requestCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记绑定记录回传结果。
    /// </summary>
    /// <param name="requestCode">任务请求号。</param>
    /// <param name="feedbackStatus">回传状态。</param>
    /// <param name="feedbackError">回传错误。</param>
    /// <param name="feedbackTime">回传时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateFeedbackStatusAsync(
        string requestCode,
        int feedbackStatus,
        string? feedbackError,
        DateTime? feedbackTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PDA 绑定服务接口。
/// </summary>
public interface IPdaBindingService
{
    /// <summary>
    /// 获取 PDA 可选工单。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工单下拉数据。</returns>
    Task<List<PdaWorkOrderOption>> GetWorkOrderOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交 PDA 扫码绑定并创建任务。
    /// </summary>
    /// <param name="request">绑定请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>绑定结果。</returns>
    Task<PdaBindingResult> BindAsync(PdaBindingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询指定任务是否存在可回传的 PDA 绑定。
    /// </summary>
    /// <param name="requestCode">任务请求号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>绑定记录。</returns>
    Task<RCS_PdaTaskBinding?> GetBindingByRequestCodeAsync(string requestCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新 PDA 绑定回传状态。
    /// </summary>
    /// <param name="requestCode">任务请求号。</param>
    /// <param name="feedbackStatus">回传状态。</param>
    /// <param name="feedbackError">回传错误。</param>
    /// <param name="feedbackTime">回传时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateFeedbackStatusAsync(
        string requestCode,
        int feedbackStatus,
        string? feedbackError,
        DateTime? feedbackTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PDA 绑定请求。
/// </summary>
public sealed class PdaBindingRequest
{
    /// <summary>工单号。</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>托盘号。</summary>
    public string PalletNumber { get; init; } = string.Empty;

    /// <summary>SSCC码。</summary>
    public string Barcode { get; init; } = string.Empty;
}

/// <summary>
/// PDA 工单下拉项。
/// </summary>
public sealed class PdaWorkOrderOption
{
    /// <summary>工单号。</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>物料编码。</summary>
    public string MaterialNumber { get; init; } = string.Empty;

    /// <summary>物料名称。</summary>
    public string MaterialName { get; init; } = string.Empty;

    /// <summary>前端展示标签。</summary>
    public string DisplayLabel { get; init; } = string.Empty;
}

/// <summary>
/// PDA 绑定结果。
/// </summary>
public sealed class PdaBindingResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>提示消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>绑定记录 ID。</summary>
    public int BindingId { get; init; }

    /// <summary>用户任务 ID。</summary>
    public int TaskId { get; init; }

    /// <summary>任务请求号。</summary>
    public string RequestCode { get; init; } = string.Empty;

    public static PdaBindingResult Ok(int bindingId, int taskId, string requestCode)
    {
        return new PdaBindingResult
        {
            Success = true,
            BindingId = bindingId,
            TaskId = taskId,
            RequestCode = requestCode,
            Message = "绑定成功"
        };
    }

    public static PdaBindingResult Fail(string message)
    {
        return new PdaBindingResult
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
/// PDA 绑定仓储实现。
/// </summary>
public sealed class PdaBindingRepository : IPdaBindingRepository
{
    private readonly IDatabaseService _db;

    public PdaBindingRepository(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<RCS_WorkOrder>> GetActiveWorkOrdersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        var items = await connection.QueryAsync<RCS_WorkOrder>(new CommandDefinition(
            @"SELECT *
              FROM RCS_WorkOrder
              WHERE MsgType = '1'
              ORDER BY CreateTime DESC, ID DESC;",
            cancellationToken: cancellationToken));

        return items.ToList();
    }

    public async Task<RCS_WorkOrder?> FindWorkOrderAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_WorkOrder>(new CommandDefinition(
            @"SELECT TOP 1 *
              FROM RCS_WorkOrder
              WHERE OrderNumber = @OrderNumber
                AND MsgType = '1'
              ORDER BY ID DESC;",
            new { OrderNumber = orderNumber.Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsBindingAsync(
        string orderNumber,
        string palletNumber,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1)
              FROM RCS_PdaTaskBinding
              WHERE OrderNumber = @OrderNumber
                AND PalletNumber = @PalletNumber
                AND Barcode = @Barcode
                AND BindingStatus = 1;",
            new
            {
                OrderNumber = orderNumber.Trim(),
                PalletNumber = palletNumber.Trim(),
                Barcode = barcode.Trim()
            },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<int> InsertBindingAsync(RCS_PdaTaskBinding entity, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO RCS_PdaTaskBinding
              (
                  OrderNumber,
                  MaterialNumber,
                  MaterialName,
                  PalletNumber,
                  ScanCode,
                  Barcode,
                  BindingStatus,
                  ExternalTaskType,
                  FeedbackStatus,
                  FeedbackTime,
                  FeedbackError,
                  TaskGroupNo,
                  RequestCode,
                  CreateTime,
                  UpdateTime,
                  Remarks
              )
              VALUES
              (
                  @OrderNumber,
                  @MaterialNumber,
                  @MaterialName,
                  @PalletNumber,
                  @ScanCode,
                  @Barcode,
                  @BindingStatus,
                  @ExternalTaskType,
                  @FeedbackStatus,
                  @FeedbackTime,
                  @FeedbackError,
                  @TaskGroupNo,
                  @RequestCode,
                  @CreateTime,
                  @UpdateTime,
                  @Remarks
              );
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            entity,
            cancellationToken: cancellationToken));
    }

    public async Task<RCS_PdaTaskBinding?> FindByRequestCodeAsync(string requestCode, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_PdaTaskBinding>(new CommandDefinition(
            @"SELECT TOP 1 *
              FROM RCS_PdaTaskBinding
              WHERE RequestCode = @RequestCode
              ORDER BY ID DESC;",
            new { RequestCode = requestCode.Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateFeedbackStatusAsync(
        string requestCode,
        int feedbackStatus,
        string? feedbackError,
        DateTime? feedbackTime,
        CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_PdaTaskBinding
              SET FeedbackStatus = @FeedbackStatus,
                  FeedbackTime = @FeedbackTime,
                  FeedbackError = @FeedbackError,
                  UpdateTime = @UpdateTime
              WHERE RequestCode = @RequestCode;",
            new
            {
                RequestCode = requestCode.Trim(),
                FeedbackStatus = feedbackStatus,
                FeedbackTime = feedbackTime,
                FeedbackError = string.IsNullOrWhiteSpace(feedbackError) ? null : feedbackError.Trim(),
                UpdateTime = DateTime.Now
            },
            cancellationToken: cancellationToken));
    }
}

/// <summary>
/// PDA 绑定服务实现。
/// </summary>
public sealed class PdaBindingService : IPdaBindingService
{
    private const int TabletTaskTypeFeedToLineSide = 101;

    private readonly IPdaBindingRepository _pdaBindingRepository;
    private readonly IUserTaskCreationService _userTaskCreationService;
    private readonly ILogger<PdaBindingService> _logger;

    public PdaBindingService(
        IPdaBindingRepository pdaBindingRepository,
        IUserTaskCreationService userTaskCreationService,
        ILogger<PdaBindingService> logger)
    {
        _pdaBindingRepository = pdaBindingRepository;
        _userTaskCreationService = userTaskCreationService;
        _logger = logger;
    }

    public async Task<List<PdaWorkOrderOption>> GetWorkOrderOptionsAsync(CancellationToken cancellationToken = default)
    {
        var workOrders = await _pdaBindingRepository.GetActiveWorkOrdersAsync(cancellationToken);
        return workOrders
            .Select(item => new PdaWorkOrderOption
            {
                OrderNumber = item.OrderNumber,
                MaterialNumber = item.MaterialNumber,
                MaterialName = item.MaterialName,
                DisplayLabel = $"{item.OrderNumber} / {item.MaterialNumber} / {item.MaterialName}"
            })
            .ToList();
    }

    public async Task<PdaBindingResult> BindAsync(PdaBindingRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return PdaBindingResult.Fail("请求体不能为空");
        }

        var orderNumber = Normalize(request.OrderNumber);
        var palletNumber = Normalize(request.PalletNumber);
        var barcode = Normalize(request.Barcode);

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return PdaBindingResult.Fail("工单不能为空");
        }

        if (string.IsNullOrWhiteSpace(palletNumber))
        {
            return PdaBindingResult.Fail("托盘号不能为空");
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            return PdaBindingResult.Fail("SSCC码不能为空");
        }

        try
        {
            var workOrder = await _pdaBindingRepository.FindWorkOrderAsync(orderNumber, cancellationToken);
            if (workOrder == null)
            {
                return PdaBindingResult.Fail("未找到可绑定的生效工单");
            }

            var bindingExists = await _pdaBindingRepository.ExistsBindingAsync(orderNumber, palletNumber, barcode, cancellationToken);
            if (bindingExists)
            {
                return PdaBindingResult.Fail("该工单下的托盘号与SSCC码已绑定，请勿重复提交");
            }

            var requestCode = $"PDA_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..32];
            var taskGroupNo = $"PDA_{orderNumber}_{DateTime.Now:yyyyMMddHHmmss}";

            var binding = new RCS_PdaTaskBinding
            {
                OrderNumber = workOrder.OrderNumber,
                MaterialNumber = workOrder.MaterialNumber,
                MaterialName = workOrder.MaterialName,
                PalletNumber = palletNumber!,
                ScanCode = barcode!,
                Barcode = barcode!,
                BindingStatus = 1,
                ExternalTaskType = TabletTaskTypeFeedToLineSide,
                FeedbackStatus = 0,
                FeedbackTime = null,
                FeedbackError = null,
                TaskGroupNo = taskGroupNo,
                RequestCode = requestCode,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now,
                // Why: 现场正式规则未最终冻结前，允许在备注里临时挂接 source/target 提示，任务创建服务会统一解析。
                Remarks = $"PDA扫码绑定; orderNumber={workOrder.OrderNumber}; palletNumber={palletNumber}; barcode={barcode}"
            };

            var bindingId = await _pdaBindingRepository.InsertBindingAsync(binding, cancellationToken);

            var createTaskResult = await _userTaskCreationService.CreateTaskAsync(new UserTaskCreationRequest
            {
                SourceType = UserTaskSourceType.Tablet,
                RequestCode = requestCode,
                TaskGroupNo = taskGroupNo,
                Priority = 1,
                OrderNumber = workOrder.OrderNumber,
                ScanCode = barcode,
                ExternalTaskType = TabletTaskTypeFeedToLineSide,
                PalletNumber = palletNumber,
                BinNumber = barcode,
                Remarks = $"PDA binding for work order {orderNumber}",
                ValidateLocationExistence = false,
                ValidateReachableTarget = false,
                LockLocations = false
            }, cancellationToken: cancellationToken);

            if (!createTaskResult.Success)
            {
                return PdaBindingResult.Fail(createTaskResult.Message);
            }

            return PdaBindingResult.Ok(bindingId, createTaskResult.TaskId, requestCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDA扫码绑定失败，OrderNumber={OrderNumber}, PalletNumber={PalletNumber}, Barcode={Barcode}", orderNumber, palletNumber, barcode);
            return PdaBindingResult.Fail("PDA扫码绑定失败");
        }
    }

    public Task<RCS_PdaTaskBinding?> GetBindingByRequestCodeAsync(string requestCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestCode))
        {
            return Task.FromResult<RCS_PdaTaskBinding?>(null);
        }

        return _pdaBindingRepository.FindByRequestCodeAsync(requestCode, cancellationToken);
    }

    public Task UpdateFeedbackStatusAsync(
        string requestCode,
        int feedbackStatus,
        string? feedbackError,
        DateTime? feedbackTime,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestCode))
        {
            return Task.CompletedTask;
        }

        return _pdaBindingRepository.UpdateFeedbackStatusAsync(
            requestCode,
            feedbackStatus,
            feedbackError,
            feedbackTime,
            cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
