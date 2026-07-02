using System.Data;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Services;
using ServiceSettingKeys = WarehouseManagementSystem.Services.ServiceSettingKeys;

namespace WarehouseManagementSystem.Services.Tasks;

/// <summary>
/// 统一的用户任务创建服务。
/// </summary>
public interface IUserTaskCreationService
{
    /// <summary>
    /// 根据统一请求模型创建 <c>RCS_UserTasks</c> 任务。
    /// </summary>
    /// <param name="request">任务创建请求。</param>
    /// <param name="connection">可选的数据库连接；外部传入时可复用同一事务。</param>
    /// <param name="transaction">可选的数据库事务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建结果。</returns>
    Task<UserTaskCreationResult> CreateTaskAsync(
        UserTaskCreationRequest request,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 统一任务来源。
/// </summary>
public enum UserTaskSourceType
{
    Manual = 1,
    Mes = 2,
    Tablet = 3
}

/// <summary>
/// 统一任务创建请求。
/// </summary>
public sealed class UserTaskCreationRequest
{
    /// <summary>任务来源。</summary>
    public UserTaskSourceType SourceType { get; init; }

    /// <summary>内部任务编号；为空时由服务自动生成。</summary>
    public string? RequestCode { get; init; }

    /// <summary>任务组号，例如 MES 的 taskNumber。</summary>
    public string? TaskGroupNo { get; init; }

    /// <summary>优先级。</summary>
    public int? Priority { get; init; }

    /// <summary>源位置。</summary>
    public string? SourcePosition { get; init; }

    /// <summary>目标位置。</summary>
    public string? TargetPosition { get; init; }

    /// <summary>平板/外部任务回填的默认源位置。</summary>
    public string? DefaultSourcePosition { get; init; }

    /// <summary>平板/外部任务回填的默认目标位置。</summary>
    public string? DefaultTargetPosition { get; init; }

    /// <summary>物料编码。</summary>
    public string? MaterialCode { get; init; }

    /// <summary>托盘号。</summary>
    public string? PalletNumber { get; init; }

    /// <summary>Bin 编号。</summary>
    public string? BinNumber { get; init; }

    /// <summary>关联工单号。</summary>
    public string? OrderNumber { get; init; }

    /// <summary>平板扫码值或外部条码。</summary>
    public string? ScanCode { get; init; }

    /// <summary>人工任务指定的内部任务类型。</summary>
    public TaskTypeEnum? RequestedTaskType { get; init; }

    /// <summary>MES/平板传入的外部业务任务类型。</summary>
    public int? ExternalTaskType { get; init; }

    /// <summary>备注。</summary>
    public string? Remarks { get; init; }

    /// <summary>是否要求源/目标位置必须存在于储位表中。</summary>
    public bool ValidateLocationExistence { get; init; }

    /// <summary>是否校验目标储位在标准库道规则下可达。</summary>
    public bool ValidateReachableTarget { get; init; }

    /// <summary>创建成功后是否锁定源/目标储位。</summary>
    public bool LockLocations { get; init; }
}

/// <summary>
/// 用户任务创建结果。
/// </summary>
public sealed class UserTaskCreationResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>结果消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>创建后的任务 ID。</summary>
    public int TaskId { get; init; }

    /// <summary>创建后用户任务的初始状态。</summary>
    public int TaskStatus { get; init; }

    public static UserTaskCreationResult Ok(int taskId, int taskStatus, string message)
    {
        return new UserTaskCreationResult
        {
            Success = true,
            TaskId = taskId,
            TaskStatus = taskStatus,
            Message = message
        };
    }

    public static UserTaskCreationResult Fail(string message)
    {
        return new UserTaskCreationResult
        {
            Success = false,
            Message = message
        };
    }
}

internal sealed class UserTaskCreationDraft
{
    public TaskStatuEnum TaskStatus { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public string? TaskGroupNo { get; init; }
    public int TaskType { get; init; }
    public int Priority { get; init; }
    public string RobotCode { get; init; } = "0";
    public string? SourcePosition { get; init; }
    public string? TargetPosition { get; init; }
    public string? PalletNumber { get; init; }
    public string? BinNumber { get; init; }
    public string Remarks { get; init; } = string.Empty;
    public bool LockLocations { get; init; }
    public bool LockSourceLocation { get; init; }
    public bool LockTargetLocation { get; init; }
}

internal sealed class UserTaskDraftBuildResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public UserTaskCreationDraft? Draft { get; init; }

    public static UserTaskDraftBuildResult Ok(UserTaskCreationDraft draft)
    {
        return new UserTaskDraftBuildResult
        {
            Success = true,
            Draft = draft
        };
    }

    public static UserTaskDraftBuildResult Fail(string message)
    {
        return new UserTaskDraftBuildResult
        {
            Success = false,
            Message = message
        };
    }
}

internal sealed class TabletRouteHints
{
    public string? SourcePosition { get; init; }
    public string? TargetPosition { get; init; }
}

internal sealed class TabletRouteResolution
{
    public string? SourcePosition { get; init; }
    public string? TargetPosition { get; init; }
    public string? OrderNumber { get; init; }
    public string? ScanCode { get; init; }
}

/// <summary>
/// 统一的用户任务创建服务实现。
/// </summary>
public sealed class UserTaskCreationService : IUserTaskCreationService
{
    private const int TabletTaskTypeFeedToLineSide = 101;
    private const int MesTaskTypeEmptyPalletRecycle = 2;
    private const int MesTaskTypeBinBuffer = 4;
    private const string MesTaskType2DefaultTargetGroup = "空托回收处";
    private const string MesTaskType4DefaultTargetGroup = "BIN暂存间";

    private readonly IDatabaseService _db;
    private readonly ILogger<UserTaskCreationService> _logger;

    public UserTaskCreationService(
        IDatabaseService db,
        ILogger<UserTaskCreationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserTaskCreationResult> CreateTaskAsync(
        UserTaskCreationRequest request,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var ownsConnection = connection is null;
        var ownsTransaction = ownsConnection && transaction is null;
        IDbConnection? innerConnection = connection;
        IDbTransaction? innerTransaction = transaction;

        try
        {
            if (innerConnection is null)
            {
                innerConnection = _db.CreateConnection();
            }

            if (innerConnection.State != ConnectionState.Open)
            {
                innerConnection.Open();
            }

            if (ownsTransaction)
            {
                innerTransaction = innerConnection.BeginTransaction();
            }

            var draftResult = await BuildTaskDraftAsync(
                request,
                innerConnection,
                innerTransaction,
                cancellationToken);

            if (!draftResult.Success || draftResult.Draft is null)
            {
                if (ownsTransaction)
                {
                    innerTransaction?.Rollback();
                }

                return UserTaskCreationResult.Fail(draftResult.Message);
            }

            var taskId = await InsertUserTaskAsync(
                innerConnection,
                innerTransaction,
                draftResult.Draft,
                cancellationToken);

            if (draftResult.Draft.LockLocations)
            {
                var lockResult = await LockTaskLocationsAsync(
                    innerConnection,
                    innerTransaction,
                    draftResult.Draft,
                    cancellationToken);

                if (!lockResult.Success)
                {
                    if (ownsTransaction)
                    {
                        innerTransaction?.Rollback();
                    }

                    return UserTaskCreationResult.Fail(lockResult.Message);
                }
            }

            if (ownsTransaction)
            {
                innerTransaction?.Commit();
            }

            return UserTaskCreationResult.Ok(taskId, (int)draftResult.Draft.TaskStatus, "任务创建成功");
        }
        catch (Exception ex)
        {
            if (ownsTransaction)
            {
                innerTransaction?.Rollback();
            }

            _logger.LogError(ex, "创建统一任务失败，SourceType={SourceType}, RequestCode={RequestCode}", request.SourceType, request.RequestCode);
            return UserTaskCreationResult.Fail($"创建任务失败: {ex.Message}");
        }
        finally
        {
            if (ownsConnection && innerConnection?.State == ConnectionState.Open)
            {
                innerConnection.Close();
            }
        }
    }

    private async Task<UserTaskDraftBuildResult> BuildTaskDraftAsync(
        UserTaskCreationRequest request,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return request.SourceType switch
        {
            UserTaskSourceType.Manual => await BuildManualTaskDraftAsync(request, connection, transaction, cancellationToken),
            UserTaskSourceType.Mes or UserTaskSourceType.Tablet => await BuildExternalTaskDraftAsync(request, connection, transaction, cancellationToken),
            _ => UserTaskDraftBuildResult.Fail("不支持的任务来源")
        };
    }

    private async Task<UserTaskDraftBuildResult> BuildManualTaskDraftAsync(
        UserTaskCreationRequest request,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sourcePosition = NormalizeText(request.SourcePosition);
        var targetPosition = NormalizeText(request.TargetPosition);

        if (string.IsNullOrWhiteSpace(sourcePosition))
        {
            return UserTaskDraftBuildResult.Fail("源位置不能为空");
        }

        if (string.IsNullOrWhiteSpace(targetPosition))
        {
            return UserTaskDraftBuildResult.Fail("目标位置不能为空");
        }

        if (string.Equals(sourcePosition, targetPosition, StringComparison.OrdinalIgnoreCase))
        {
            return UserTaskDraftBuildResult.Fail("源位置和目标位置不能相同");
        }

        if (request.ValidateLocationExistence)
        {
            var existenceResult = await EnsureLocationsExistAsync(
                connection,
                transaction,
                new[] { sourcePosition, targetPosition },
                cancellationToken);

            if (!existenceResult.Success)
            {
                return existenceResult;
            }
        }

        if (request.ValidateReachableTarget)
        {
            var reachableResult = await EnsureTargetReachableAsync(
                connection,
                transaction,
                targetPosition,
                cancellationToken);

            if (!reachableResult.Success)
            {
                return reachableResult;
            }
        }

        var requestCode = NormalizeText(request.RequestCode);
        if (string.IsNullOrWhiteSpace(requestCode))
        {
            requestCode = $"RELOCATE_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..32];
        }

        var taskType = ResolveManualTaskType(request.RequestedTaskType);
        var taskStatus = await GetInitialTaskStatusAsync(connection, transaction, cancellationToken);

        var remarks = NormalizeText(request.Remarks);
        if (string.IsNullOrWhiteSpace(remarks))
        {
            remarks = "Manual task";
        }

        return UserTaskDraftBuildResult.Ok(new UserTaskCreationDraft
        {
            TaskStatus = taskStatus,
            RequestCode = requestCode,
            TaskGroupNo = NormalizeText(request.TaskGroupNo),
            TaskType = (int)taskType,
            Priority = NormalizePriority(request.Priority),
            SourcePosition = sourcePosition,
            TargetPosition = targetPosition,
            PalletNumber = NormalizeText(request.PalletNumber),
            BinNumber = NormalizeText(request.BinNumber),
            Remarks = remarks,
            LockLocations = request.LockLocations,
            LockSourceLocation = request.LockLocations,
            LockTargetLocation = request.LockLocations
        });
    }

    /// <summary>
    /// Why: 外部任务最终如何落到 <c>RCS_UserTasks</c>，统一从这里进入；
    /// 其中 MES 的 1/2/3/4/5 类型规则被进一步收口到单一方法，后续二开只需要聚焦那一个入口即可。
    /// </summary>
    private async Task<UserTaskDraftBuildResult> BuildExternalTaskDraftAsync(
        UserTaskCreationRequest request,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (!request.ExternalTaskType.HasValue)
        {
            return UserTaskDraftBuildResult.Fail("外部任务类型不能为空");
        }

        var requestCode = NormalizeText(request.RequestCode);
        if (string.IsNullOrWhiteSpace(requestCode))
        {
            return UserTaskDraftBuildResult.Fail("外部任务缺少 requestCode");
        }

        var taskGroupNo = NormalizeText(request.TaskGroupNo);
        var externalTaskType = request.ExternalTaskType.Value;
        var taskStatus = await GetInitialTaskStatusAsync(connection, transaction, cancellationToken);
        UserTaskCreationDraft? draft = null;

        if (request.SourceType == UserTaskSourceType.Mes)
        {
            draft = await BuildMesTaskDraftByTaskTypeAsync(
                request,
                requestCode,
                taskGroupNo,
                externalTaskType,
                taskStatus,
                connection,
                transaction,
                cancellationToken);
        }
        else if (externalTaskType == TabletTaskTypeFeedToLineSide)
        {
            draft = await CreateTabletFeedToLineSideDraftAsync();
        }

        if (draft is null)
        {
            return UserTaskDraftBuildResult.Fail($"不支持的外部任务类型: {externalTaskType}");
        }

        if (string.IsNullOrWhiteSpace(draft.TargetPosition))
        {
            return UserTaskDraftBuildResult.Fail("目标位置不能为空");
        }

        if (request.ValidateLocationExistence)
        {
            var keys = new[] { draft.SourcePosition, draft.TargetPosition }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();

            var existenceResult = await EnsureLocationsExistAsync(connection, transaction, keys, cancellationToken);
            if (!existenceResult.Success)
            {
                return existenceResult;
            }
        }

        if (request.ValidateReachableTarget && !string.IsNullOrWhiteSpace(draft.TargetPosition))
        {
            var reachableResult = await EnsureTargetReachableAsync(connection, transaction, draft.TargetPosition, cancellationToken);
            if (!reachableResult.Success)
            {
                return reachableResult;
            }
        }

        return UserTaskDraftBuildResult.Ok(draft);

        async Task<UserTaskCreationDraft> CreateTabletFeedToLineSideDraftAsync()
        {
            var sourcePosition = NormalizeText(request.SourcePosition);
            var targetPosition = NormalizeText(request.TargetPosition);
            var routeResolution = await ResolveTabletFeedRouteAsync(
                request,
                sourcePosition,
                targetPosition,
                connection,
                transaction,
                cancellationToken);
            var traceRemarks = AppendTabletTraceContext(
                BuildExternalRemarks(request.SourceType, externalTaskType, request.Remarks),
                routeResolution.OrderNumber,
                routeResolution.ScanCode);

            return new UserTaskCreationDraft
            {
                TaskStatus = taskStatus,
                RequestCode = requestCode,
                TaskGroupNo = taskGroupNo,
                TaskType = TabletTaskTypeFeedToLineSide,
                Priority = NormalizePriority(request.Priority),
                SourcePosition = routeResolution.SourcePosition,
                TargetPosition = routeResolution.TargetPosition,
                PalletNumber = NormalizeText(request.PalletNumber),
                BinNumber = NormalizeText(request.BinNumber),
                Remarks = traceRemarks,
                LockLocations = request.LockLocations,
                LockSourceLocation = request.LockLocations,
                LockTargetLocation = request.LockLocations
            };
        }
    }

    /// <summary>
    /// Why: 你后续如果主要维护 MES 下发任务，就只需要看这个方法。
    /// 它把 1/2/3/4/5 五种任务类型的“任务类型原样落库、起点、终点、默认终点补位规则”全部收口在一个地方，
    /// 避免再分散到多个 helper 里来回跳。
    /// </summary>
    private async Task<UserTaskCreationDraft?> BuildMesTaskDraftByTaskTypeAsync(
        UserTaskCreationRequest request,
        string requestCode,
        string? taskGroupNo,
        int externalTaskType,
        TaskStatuEnum taskStatus,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sourcePosition = NormalizeText(request.SourcePosition);
        var targetPosition = NormalizeText(request.TargetPosition);
        var traceRemarks = BuildExternalRemarks(request.SourceType, externalTaskType, request.Remarks);

        switch (externalTaskType)
        {
            case 1:
                await EnsureMesKnownRouteLocationsExistAsync(
                    sourcePosition,
                    targetPosition,
                    "MES任务类型1",
                    connection,
                    transaction,
                    cancellationToken);
                return CreateMesDraft(externalTaskType, sourcePosition, targetPosition);
            case 2:
                await EnsureMesSourceLocationExistsAsync(
                    sourcePosition,
                    "MES任务类型2",
                    connection,
                    transaction,
                    cancellationToken);
                return CreateMesDraft(
                    externalTaskType,
                    sourcePosition,
                    await ResolveMesTaskTargetPositionByFixedGroupAsync(
                        sourcePosition,
                        connection,
                        transaction,
                        MesTaskType2DefaultTargetGroup,
                        "MES任务类型2",
                        cancellationToken),
                    lockTargetOnly: true);
            case 3:
                await EnsureMesKnownRouteLocationsExistAsync(
                    sourcePosition,
                    targetPosition,
                    "MES任务类型3",
                    connection,
                    transaction,
                    cancellationToken);
                return CreateMesDraft(externalTaskType, sourcePosition, targetPosition);
            case 4:
                await EnsureMesSourceLocationExistsAsync(
                    sourcePosition,
                    "MES任务类型4",
                    connection,
                    transaction,
                    cancellationToken);
                return CreateMesDraft(
                    externalTaskType,
                    sourcePosition,
                    await ResolveMesTaskTargetPositionByFixedGroupAsync(
                        sourcePosition,
                        connection,
                        transaction,
                        MesTaskType4DefaultTargetGroup,
                        "MES任务类型4",
                        cancellationToken),
                    lockTargetOnly: true);
            case 5:
                await EnsureMesKnownRouteLocationsExistAsync(
                    sourcePosition,
                    targetPosition,
                    "MES任务类型5",
                    connection,
                    transaction,
                    cancellationToken);
                return CreateMesDraft(externalTaskType, sourcePosition, targetPosition);
            default:
                return null;
        }

        UserTaskCreationDraft CreateMesDraft(
            int taskType,
            string? resolvedSourcePosition,
            string? resolvedTargetPosition,
            bool lockLocations = false,
            bool lockTargetOnly = false)
        {
            var traceRemarks = BuildExternalRemarks(request.SourceType, externalTaskType, request.Remarks);
            var lockSourceLocation = request.LockLocations;
            var lockTargetLocation = request.LockLocations;

            if (lockTargetOnly)
            {
                lockTargetLocation = true;
            }
            else if (lockLocations)
            {
                lockSourceLocation = true;
                lockTargetLocation = true;
            }

            return new UserTaskCreationDraft
            {
                TaskStatus = taskStatus,
                RequestCode = requestCode,
                TaskGroupNo = taskGroupNo,
                TaskType = taskType,
                Priority = NormalizePriority(request.Priority),
                SourcePosition = resolvedSourcePosition,
                TargetPosition = resolvedTargetPosition,
                PalletNumber = NormalizeText(request.PalletNumber),
                BinNumber = NormalizeText(request.BinNumber),
                Remarks = traceRemarks,
                LockLocations = lockSourceLocation || lockTargetLocation,
                LockSourceLocation = lockSourceLocation,
                LockTargetLocation = lockTargetLocation
            };
        }
    }

    /// <summary>
    /// Why: 类型1/3/5属于“报文里已知起点和终点”的任务，
    /// 这里在落 <c>RCS_UserTasks</c> 前就先和 NDC 位置表对齐校验，避免到了 <c>RcsWmsTaskHostedService</c>
    /// 下发阶段才因为找不到 <c>NodeRemark</c> 被取消。
    /// </summary>
    private async Task EnsureMesKnownRouteLocationsExistAsync(
        string? sourcePosition,
        string? targetPosition,
        string taskTypeDisplayName,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePosition))
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 起点不能为空。");
        }

        if (string.IsNullOrWhiteSpace(targetPosition))
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 终点不能为空。");
        }

        var locations = await LoadLocationsByNodeRemarksAsync(
            connection,
            transaction,
            new[] { sourcePosition, targetPosition },
            cancellationToken);

        var sourceLocation = locations.FirstOrDefault(x => string.Equals(x.NodeRemark, sourcePosition, StringComparison.OrdinalIgnoreCase));
        if (sourceLocation == null)
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 起点“{sourcePosition}”不在 储位配置 中。");
        }

        var targetLocation = locations.FirstOrDefault(x => string.Equals(x.NodeRemark, targetPosition, StringComparison.OrdinalIgnoreCase));
        if (targetLocation == null)
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 终点“{targetPosition}”不在 储位配置 中。");
        }
    }

    /// <summary>
    /// Why: 类型2/4的终点由系统自行找位，先保证起点合法，再按固定区域常量找“最里层、未锁定、空闲、未被占任务”的目标位，
    /// 这样和 <c>RcsWmsTaskHostedService</c> 后续按 <c>NodeRemark</c> 下发 NDC 任务的要求保持一致。
    /// </summary>
    private async Task EnsureMesSourceLocationExistsAsync(
        string? sourcePosition,
        string taskTypeDisplayName,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePosition))
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 起点不能为空。");
        }

        var locations = await LoadLocationsByNodeRemarksAsync(
            connection,
            transaction,
            new[] { sourcePosition },
            cancellationToken);

        var sourceLocation = locations.FirstOrDefault(x => string.Equals(x.NodeRemark, sourcePosition, StringComparison.OrdinalIgnoreCase));
        if (sourceLocation == null)
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 起点“{sourcePosition}”不在 NdcLocation 中。");
        }
    }

    private async Task<string> ResolveMesTaskTargetPositionByFixedGroupAsync(
        string? sourcePosition,
        IDbConnection connection,
        IDbTransaction? transaction,
        string targetGroup,
        string taskTypeDisplayName,
        CancellationToken cancellationToken)
    {
        var candidateLocations = (await connection.QueryAsync<NdcLocation>(new CommandDefinition(
            @"SELECT *
              FROM RCS_Locations
              WHERE [Group] = @Group
              ORDER BY [Group], LaneCode, DepthIndex, NodeRemark;",
            new { Group = targetGroup },
            transaction,
            cancellationToken: cancellationToken))).ToList();

        if (candidateLocations.Count == 0)
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 固定终点区域“{targetGroup}”下没有可用储位。");
        }

        var busyNodeRemarks = await LoadBusyTaskNodeRemarksAsync(connection, transaction, cancellationToken);
        var availableLocation = candidateLocations
            .Where(location => location.Enabled)
            .Where(location => !location.Lock)
            .Where(location => !IsMaterialPresent(location))
            .Where(location => !busyNodeRemarks.Contains(location.NodeRemark ?? string.Empty))
            .Where(location => !string.Equals(location.NodeRemark, sourcePosition, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(location => location.DepthIndex)
            .ThenBy(location => location.NodeRemark, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (availableLocation == null)
        {
            throw new InvalidOperationException($"{taskTypeDisplayName} 在固定区域“{targetGroup}”下未找到满足“最里层、空闲、未锁定”的目标储位。");
        }

        return availableLocation.NodeRemark ?? throw new InvalidOperationException($"{taskTypeDisplayName} 选中的目标储位缺少 NodeRemark。");
    }

    /// <summary>
    /// Why: 平板送料任务后续会持续调整现场规则，这里统一负责把“工单 / 绑定记录 / 默认配置 / 显式传参”
    /// 汇总成最终起点和终点，避免业务规则散落在控制器或 PDA 绑定入口中。
    /// </summary>
    private async Task<TabletRouteResolution> ResolveTabletFeedRouteAsync(
        UserTaskCreationRequest request,
        string? sourcePosition,
        string? targetPosition,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var binding = await LoadLatestTabletBindingAsync(request, connection, transaction, cancellationToken);
        var orderNumber = NormalizeText(request.OrderNumber) ?? NormalizeText(binding?.OrderNumber);
        var scanCode = NormalizeText(request.ScanCode) ?? NormalizeText(binding?.ScanCode) ?? NormalizeText(request.PalletNumber);
        var workOrder = string.IsNullOrWhiteSpace(orderNumber)
            ? null
            : await LoadActiveWorkOrderAsync(orderNumber, connection, transaction, cancellationToken);

        var configuredSourcePosition = await GetSystemSettingValueAsync(
            connection,
            transaction,
            ServiceSettingKeys.TabletFeedSourcePosition,
            "PDA_BUFFER_IN",
            cancellationToken);
        var configuredTargetPosition = await GetSystemSettingValueAsync(
            connection,
            transaction,
            ServiceSettingKeys.TabletFeedTargetPosition,
            "LINE_SIDE_BUFFER",
            cancellationToken);

        var routeHints = ParseTabletRouteHints(
            request.Remarks,
            binding?.Remarks,
            workOrder?.Remarks);

        var resolvedSourcePosition =
            sourcePosition ??
            NormalizeText(request.DefaultSourcePosition) ??
            routeHints.SourcePosition ??
            configuredSourcePosition;

        var resolvedTargetPosition =
            targetPosition ??
            NormalizeText(request.DefaultTargetPosition) ??
            routeHints.TargetPosition ??
            configuredTargetPosition;

        if (string.IsNullOrWhiteSpace(resolvedSourcePosition))
        {
            throw new InvalidOperationException("平板任务起点解析失败，请配置 TabletFeedSourcePosition 或在规则方法中返回起点。");
        }

        if (string.IsNullOrWhiteSpace(resolvedTargetPosition))
        {
            throw new InvalidOperationException("平板任务终点解析失败，请配置 TabletFeedTargetPosition 或在规则方法中返回终点。");
        }

        if (string.Equals(resolvedSourcePosition, resolvedTargetPosition, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("平板任务起点和终点不能相同。");
        }

        return new TabletRouteResolution
        {
            SourcePosition = resolvedSourcePosition,
            TargetPosition = resolvedTargetPosition,
            OrderNumber = orderNumber,
            ScanCode = scanCode
        };
    }

    private async Task<int> InsertUserTaskAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        UserTaskCreationDraft draft,
        CancellationToken cancellationToken)
    {
        const string insertTaskSql = @"
INSERT INTO RCS_UserTasks
(
    taskStatus,
    executed,
    creatTime,
    requestCode,
    taskGroupNo,
    taskType,
    priority,
    robotCode,
    sourcePosition,
    targetPosition,
    palletNo,
    binNumber,
    taskCode,
    IsCancelled,
    remarks
)
VALUES
(
    @taskStatus,
    @executed,
    @creatTime,
    @requestCode,
    @taskGroupNo,
    @taskType,
    @priority,
    @robotCode,
    @sourcePosition,
    @targetPosition,
    @palletNo,
    @binNumber,
    @taskCode,
    @IsCancelled,
    @remarks
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            insertTaskSql,
            new
            {
                taskStatus = (int)draft.TaskStatus,
                executed = false,
                creatTime = DateTime.Now,
                requestCode = draft.RequestCode,
                taskGroupNo = draft.TaskGroupNo,
                taskType = (int)draft.TaskType,
                priority = draft.Priority,
                robotCode = draft.RobotCode,
                sourcePosition = draft.SourcePosition,
                targetPosition = draft.TargetPosition,
                palletNo = draft.PalletNumber,
                binNumber = draft.BinNumber,
                taskCode = draft.RequestCode,
                IsCancelled = false,
                remarks = draft.Remarks
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private async Task<UserTaskDraftBuildResult> EnsureLocationsExistAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        IEnumerable<string> nodeRemarks,
        CancellationToken cancellationToken)
    {
        var keys = nodeRemarks
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return UserTaskDraftBuildResult.Fail("未提供有效的储位节点");
        }

        var locations = await LoadLocationsByNodeRemarksAsync(connection, transaction, keys, cancellationToken);
        var missing = keys
            .Except(locations.Select(x => x.NodeRemark ?? string.Empty), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
        {
            return UserTaskDraftBuildResult.Fail($"以下储位不存在: {string.Join(", ", missing)}");
        }

        return UserTaskDraftBuildResult.Ok(new UserTaskCreationDraft());
    }

    private async Task<UserTaskDraftBuildResult> EnsureTargetReachableAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string targetPosition,
        CancellationToken cancellationToken)
    {
        var targetLocation = await connection.QueryFirstOrDefaultAsync<NdcLocation>(new CommandDefinition(
            @"SELECT TOP 1 * FROM RCS_Locations WHERE NodeRemark = @NodeRemark;",
            new { NodeRemark = targetPosition },
            transaction,
            cancellationToken: cancellationToken));

        if (targetLocation == null)
        {
            return UserTaskDraftBuildResult.Fail("目标储位不存在");
        }

        if (!targetLocation.Enabled || targetLocation.Lock || IsMaterialPresent(targetLocation))
        {
            return UserTaskDraftBuildResult.Fail("目标储位不可用");
        }

        if (!HasStructuredLaneInfo(targetLocation))
        {
            return UserTaskDraftBuildResult.Ok(new UserTaskCreationDraft());
        }

        var laneLocations = (await connection.QueryAsync<NdcLocation>(new CommandDefinition(
            @"SELECT * FROM RCS_Locations WHERE LaneCode = @LaneCode;",
            new { LaneCode = targetLocation.LaneCode },
            transaction,
            cancellationToken: cancellationToken))).ToList();

        var outerLocations = laneLocations
            .Where(item => item.DepthIndex < targetLocation.DepthIndex)
            .OrderBy(item => item.DepthIndex)
            .ToList();

        if (outerLocations.All(IsMaterialPresent))
        {
            return UserTaskDraftBuildResult.Ok(new UserTaskCreationDraft());
        }

        return UserTaskDraftBuildResult.Fail("目标储位在当前库道结构下不可达，请优先处理外侧储位。");
    }

    private async Task<(bool Success, string Message)> LockTaskLocationsAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        UserTaskCreationDraft draft,
        CancellationToken cancellationToken)
    {
        var nodeRemarks = new List<string>();

        if (draft.LockSourceLocation && !string.IsNullOrWhiteSpace(draft.SourcePosition))
        {
            nodeRemarks.Add(draft.SourcePosition);
        }

        if (draft.LockTargetLocation && !string.IsNullOrWhiteSpace(draft.TargetPosition))
        {
            nodeRemarks.Add(draft.TargetPosition);
        }

        var distinctNodeRemarks = nodeRemarks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctNodeRemarks.Length == 0)
        {
            return (true, string.Empty);
        }

        var lockedCount = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE RCS_Locations SET Lock = 1 WHERE NodeRemark IN @NodeRemarks",
            new { NodeRemarks = distinctNodeRemarks },
            transaction,
            cancellationToken: cancellationToken));

        return lockedCount >= distinctNodeRemarks.Length
            ? (true, string.Empty)
            : (false, "锁定储位失败，任务创建已取消");
    }

    private async Task<List<NdcLocation>> LoadLocationsByNodeRemarksAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        IEnumerable<string> nodeRemarks,
        CancellationToken cancellationToken)
    {
        var keys = nodeRemarks
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return new List<NdcLocation>();
        }

        var items = await connection.QueryAsync<NdcLocation>(new CommandDefinition(
            @"SELECT * FROM RCS_Locations WHERE NodeRemark IN @NodeRemarks;",
            new { NodeRemarks = keys },
            transaction,
            cancellationToken: cancellationToken));

        return items.ToList();
    }

    private async Task<HashSet<string>> LoadBusyTaskNodeRemarksAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var busyStatuses = new[]
        {
            (int)TaskStatuEnum.None,
            (int)TaskStatuEnum.CarWash,
            (int)TaskStatuEnum.TaskStart,
            (int)TaskStatuEnum.Confirm,
            (int)TaskStatuEnum.ConfirmCar,
            (int)TaskStatuEnum.PickingUp,
            (int)TaskStatuEnum.PickDown,
            (int)TaskStatuEnum.Unloading,
            (int)TaskStatuEnum.UnloadDown,
            (int)TaskStatuEnum.RedirectRequest,
            (int)TaskStatuEnum.OrderAgv,
            (int)TaskStatuEnum.OrderAgvFinish
        };

        var taskNodes = await connection.QueryAsync<string>(new CommandDefinition(
            @"SELECT DISTINCT NodeRemark
              FROM
              (
                  SELECT sourcePosition AS NodeRemark
                  FROM RCS_UserTasks
                  WHERE IsCancelled = 0
                    AND executed = 0
                    AND taskStatus IN @BusyStatuses
                    AND sourcePosition IS NOT NULL
                    AND LTRIM(RTRIM(sourcePosition)) <> ''
                  UNION
                  SELECT targetPosition AS NodeRemark
                  FROM RCS_UserTasks
                  WHERE IsCancelled = 0
                    AND executed = 0
                    AND taskStatus IN @BusyStatuses
                    AND targetPosition IS NOT NULL
                    AND LTRIM(RTRIM(targetPosition)) <> ''
              ) AS BusyNodes;",
            new { BusyStatuses = busyStatuses },
            transaction,
            cancellationToken: cancellationToken));

        return taskNodes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<TaskStatuEnum> GetInitialTaskStatusAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var systemType = await GetSystemSettingValueAsync(
            connection,
            transaction,
            "SystemType",
            "Heartbeat",
            cancellationToken);

        return string.Equals(systemType?.Trim(), "NDC", StringComparison.OrdinalIgnoreCase)
            ? TaskStatuEnum.None
            : (TaskStatuEnum)0;
    }

    private static async Task<string> GetSystemSettingValueAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string key,
        string defaultValue,
        CancellationToken cancellationToken)
    {
        var value = await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Value FROM SystemSettings WHERE [Key] = @Key;",
            new { Key = key },
            transaction,
            cancellationToken: cancellationToken));

        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static TaskTypeEnum ResolveManualTaskType(TaskTypeEnum? requestedTaskType)
    {
        if (requestedTaskType.HasValue && Enum.IsDefined(typeof(TaskTypeEnum), (int)requestedTaskType.Value))
        {
            return requestedTaskType.Value;
        }

        return TaskTypeEnum.Manual;
    }

    private static int NormalizePriority(int? priority)
    {
        return priority.HasValue && priority.Value > 0 ? priority.Value : 1;
    }

    /// <summary>
    /// Why: 现场还未完全沉淀结构化路由字段时，工单或绑定备注里经常会临时带 source/target 提示；
    /// 先集中兼容这些键值对，后续只需替换这里即可平滑切到正式字段。
    /// </summary>
    private static TabletRouteHints ParseTabletRouteHints(params string?[] candidates)
    {
        string? resolvedSourcePosition = null;
        string? resolvedTargetPosition = null;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var segments = candidate
                .Split(new[] { ';', '\r', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments)
            {
                var separatorIndex = segment.IndexOf('=');
                if (separatorIndex < 0)
                {
                    separatorIndex = segment.IndexOf(':');
                }

                if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
                {
                    continue;
                }

                var key = segment[..separatorIndex].Trim().ToLowerInvariant();
                var value = NormalizeText(segment[(separatorIndex + 1)..]);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                switch (key)
                {
                    case "source":
                    case "sourceposition":
                    case "from":
                    case "fromstation":
                    case "start":
                    case "起点":
                        resolvedSourcePosition ??= value;
                        break;
                    case "target":
                    case "targetposition":
                    case "to":
                    case "tostation":
                    case "end":
                    case "终点":
                        resolvedTargetPosition ??= value;
                        break;
                }
            }
        }

        return new TabletRouteHints
        {
            SourcePosition = resolvedSourcePosition,
            TargetPosition = resolvedTargetPosition
        };
    }

    private static string BuildExternalRemarks(UserTaskSourceType sourceType, int externalTaskType, string? remarks)
    {
        var sourceName = sourceType == UserTaskSourceType.Tablet ? "Tablet" : "MES";
        var normalizedRemarks = NormalizeText(remarks);

        if (string.IsNullOrWhiteSpace(normalizedRemarks))
        {
            return $"{sourceName}->AGV; externalTaskType={externalTaskType}";
        }

        return $"{normalizedRemarks}; source={sourceName}; externalTaskType={externalTaskType}";
    }

    private static string AppendTabletTraceContext(string remarks, string? orderNumber, string? scanCode)
    {
        var parts = new List<string> { remarks };

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            parts.Add($"orderNumber={orderNumber}");
        }

        if (!string.IsNullOrWhiteSpace(scanCode))
        {
            parts.Add($"scanCode={scanCode}");
        }

        return string.Join("; ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Task<RCS_WorkOrder?> LoadActiveWorkOrderAsync(
        string orderNumber,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return connection.QueryFirstOrDefaultAsync<RCS_WorkOrder>(new CommandDefinition(
            @"SELECT TOP 1 *
              FROM RCS_WorkOrder
              WHERE OrderNumber = @OrderNumber
                AND MsgType = '1'
              ORDER BY ID DESC;",
            new { OrderNumber = orderNumber },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<RCS_PdaTaskBinding?> LoadLatestTabletBindingAsync(
        UserTaskCreationRequest request,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var requestCode = NormalizeText(request.RequestCode);
        var taskGroupNo = NormalizeText(request.TaskGroupNo);
        var orderNumber = NormalizeText(request.OrderNumber);
        var scanCode = NormalizeText(request.ScanCode) ?? NormalizeText(request.PalletNumber);

        return connection.QueryFirstOrDefaultAsync<RCS_PdaTaskBinding>(new CommandDefinition(
            @"SELECT TOP 1 *
              FROM RCS_PdaTaskBinding
              WHERE BindingStatus = 1
                AND (@OrderNumber IS NULL OR OrderNumber = @OrderNumber)
                AND
                (
                    (@RequestCode IS NOT NULL AND RequestCode = @RequestCode)
                    OR (@TaskGroupNo IS NOT NULL AND TaskGroupNo = @TaskGroupNo)
                    OR (@ScanCode IS NOT NULL AND ScanCode = @ScanCode)
                )
              ORDER BY ID DESC;",
            new
            {
                OrderNumber = orderNumber,
                RequestCode = requestCode,
                TaskGroupNo = taskGroupNo,
                ScanCode = scanCode
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static List<RecommendedLocationResult> BuildRecommendedLocations(List<NdcLocation> locations)
    {
        var laneGroups = locations
            .Where(HasStructuredLaneInfo)
            .GroupBy(location => location.LaneCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DepthIndex).ThenBy(item => item.NodeRemark).ToList(), StringComparer.OrdinalIgnoreCase);

        var recommendationIndex = 1;
        var standardCandidates = new List<RecommendedLocationResult>();
        var fallbackCandidates = new List<RecommendedLocationResult>();

        foreach (var location in locations)
        {
            var result = new RecommendedLocationResult
            {
                Id = location.Id,
                Name = location.Name ?? string.Empty,
                NodeRemark = location.NodeRemark ?? string.Empty,
                Group = location.Group ?? string.Empty,
                LaneCode = location.LaneCode ?? string.Empty,
                DepthIndex = location.DepthIndex,
                WattingNode = location.WattingNode,
                IsEmpty = !IsMaterialPresent(location),
                IsLocked = location.Lock,
                Enabled = location.Enabled,
                MaterialCode = location.MaterialCode,
                PalletID = location.PalletID
            };

            if (HasStructuredLaneInfo(location))
            {
                var laneLocations = laneGroups[location.LaneCode!];
                result.IsReachableTarget = IsReachableTarget(location, laneLocations);
                result.IsRecommendedTarget = result.IsReachableTarget && result.IsEmpty && location.Enabled && !location.Lock;

                if (result.IsRecommendedTarget)
                {
                    standardCandidates.Add(result);
                }
                else
                {
                    fallbackCandidates.Add(result);
                }
            }
            else
            {
                result.IsReachableTarget = result.IsEmpty && location.Enabled && !location.Lock;
                result.IsRecommendedTarget = false;
                fallbackCandidates.Add(result);
            }
        }

        var orderedStandardCandidates = standardCandidates
            .OrderBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LaneCode, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.DepthIndex)
            .ThenBy(item => item.NodeRemark, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in orderedStandardCandidates)
        {
            item.RecommendationOrder = recommendationIndex++;
        }

        return orderedStandardCandidates
            .Concat(fallbackCandidates
                .OrderByDescending(item => item.IsReachableTarget)
                .ThenBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => string.IsNullOrWhiteSpace(item.LaneCode) ? 1 : 0)
                .ThenBy(item => item.LaneCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DepthIndex <= 0 ? int.MaxValue : item.DepthIndex)
                .ThenBy(item => item.NodeRemark, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsReachableTarget(NdcLocation candidate, List<NdcLocation> laneLocations)
    {
        if (!candidate.Enabled || candidate.Lock || IsMaterialPresent(candidate))
        {
            return false;
        }

        if (!HasStructuredLaneInfo(candidate))
        {
            return true;
        }

        var outerLocations = laneLocations
            .Where(item => item.DepthIndex < candidate.DepthIndex)
            .OrderBy(item => item.DepthIndex)
            .ToList();

        return outerLocations.All(IsMaterialPresent);
    }

    private static bool HasStructuredLaneInfo(NdcLocation location)
    {
        return !string.IsNullOrWhiteSpace(location.LaneCode) && location.DepthIndex > 0;
    }

    private static bool IsMaterialPresent(NdcLocation location)
    {
        return !string.IsNullOrWhiteSpace(location.MaterialCode) &&
               !string.Equals(location.MaterialCode, "0", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(location.MaterialCode, "empty", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ExternalRouteResolution
{
    public string? SourcePosition { get; init; }
    public string? TargetPosition { get; init; }
}
