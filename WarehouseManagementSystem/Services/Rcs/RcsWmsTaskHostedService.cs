using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Services.Integrations;
using NdcTaskStatuEnum = WarehouseManagementSystem.Models.Enums.TaskStatuEnum;
using NdcTaskTypeEnum = WarehouseManagementSystem.Models.Enums.TaskTypeEnum;

namespace WarehouseManagementSystem.Services.Rcs;

/// <summary>
/// RCS 任务与 NDC 调度系统的同步服务
/// 负责任务的下发创建、状态回写、请求取消以及库位资源释放
/// </summary>
public class RcsWmsTaskHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RcsWmsTaskHostedService> _logger;

    public RcsWmsTaskHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RcsWmsTaskHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(CreateNewTasksAsync, nameof(CreateNewTasksAsync));
            await RunSafelyAsync(UpdateTaskStatusAsync, nameof(UpdateTaskStatusAsync));
            await RunSafelyAsync(CancelTasksAsync, nameof(CancelTasksAsync));
        }
    }

    private async Task RunSafelyAsync(Func<Task> action, string actionName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行 {ActionName} 时发生未捕获异常", actionName);
        }
    }

    #region 任务取消逻辑

    /// <summary>
    /// 处理已标记为取消状态的 RCS 任务并同步至 NDC
    /// </summary>
    private async Task CancelTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dependencies = CreateDependencies(scope.ServiceProvider);

        var cancelTasks = await dependencies.UserTaskService.GetCancelableTasksAsync();

        foreach (var cancelTask in cancelTasks)
        {
            await CancelSingleTaskAsync(cancelTask, dependencies);
        }
    }

    private async Task CancelSingleTaskAsync(NdcUserTask userTask, RcsScopedDependencies dependencies)
    {
        try
        {
            var reqCode = GetScheduleTaskNo(userTask);
            var ndcTask = await dependencies.NdcTaskService.FindByScheduleTaskNoAsync(reqCode);

            if (ndcTask != null)
            {
                if (ndcTask.TaskStatus == NdcTaskStatuEnum.None || ndcTask.TaskStatus == NdcTaskStatuEnum.CarWash)
                {
                    ndcTask.SetStatus(NdcTaskStatuEnum.Canceled);
                    _logger.LogInformation("拦截并直接取消未执行的 NDC 任务: {RequestCode}", reqCode);
                }
                else if (!ndcTask.CancelTask)
                {
                    ndcTask.CancelTask = true;
                    _logger.LogInformation("标记执行中的 NDC 任务为请求取消状态: {RequestCode}", reqCode);
                }

                await dependencies.NdcTaskService.UpdateAsync(ndcTask);
            }
            else
            {
                userTask.taskStatus = NdcTaskStatuEnum.Canceled;
                await dependencies.UserTaskService.UpdateAsync(userTask);
                _logger.LogInformation("任务 {RequestCode} 未生成下发记录，直接置为已取消", reqCode);
            }

            await UnlockTaskLocationsAsync(userTask, dependencies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消任务处理失败: RequestCode={RequestCode}", userTask.requestCode);
        }
    }

    #endregion

    #region 任务状态同步与完成处理

    /// <summary>
    /// 读取下位机反馈，更新 RCS 用户任务状态
    /// </summary>
    private async Task UpdateTaskStatusAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dependencies = CreateDependencies(scope.ServiceProvider);

        var userTasks = await dependencies.UserTaskService.GetActiveTasksAsync();

        var distinctTasks = userTasks.GroupBy(x => x.Id).Select(x => x.First()).ToList();
        var requestCodes = distinctTasks
            .Where(x => !string.IsNullOrWhiteSpace(x.requestCode))
            .Select(x => x.requestCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ndcTasks = await dependencies.NdcTaskService.GetByScheduleTaskNosAsync(requestCodes);

        foreach (var userTask in distinctTasks)
        {
            var reqCode = GetScheduleTaskNo(userTask);
            var ndcTask = ndcTasks.FirstOrDefault(x => x.SchedulTaskNo == reqCode);
            
            if (ndcTask == null || ndcTask.TaskStatus == userTask.taskStatus) continue;

            var oldStatus = userTask.taskStatus;
            userTask.taskStatus = ndcTask.TaskStatus;
            userTask.robotCode = ndcTask.AgvId.ToString();

            _logger.LogInformation("任务 {RequestCode} 状态流转: {OldStatus} -> {NewStatus}", reqCode, oldStatus, ndcTask.TaskStatus);

            if (ndcTask.TaskStatus == NdcTaskStatuEnum.TaskFinish)
            {
                await UnlockTaskLocationsAsync(userTask, dependencies);
            }
            else if (ndcTask.TaskStatus == NdcTaskStatuEnum.Canceled ||
                     ndcTask.TaskStatus == NdcTaskStatuEnum.RedirectRequest)
            {
                await UnlockTaskLocationsAsync(userTask, dependencies);
            }

            await dependencies.UserTaskService.UpdateAsync(userTask);
            await TryTriggerUpstreamInteractionsAsync(userTask, ndcTask.TaskStatus, dependencies);
        }
    }

 

    /// <summary>
    /// 释放任务意外中断所占用的起终点库位锁
    /// </summary>
    private async Task UnlockTaskLocationsAsync(NdcUserTask userTask, RcsScopedDependencies dependencies)
    {
        var locations = await dependencies.LocationService.GetByNodeRemarksAsync(new[]
        {
            userTask.sourcePosition,
            userTask.targetPosition
        });
        var sourceLocation = locations.FirstOrDefault(x => x.NodeRemark == userTask.sourcePosition);
        var targetLocation = locations.FirstOrDefault(x => x.NodeRemark == userTask.targetPosition);

        if (sourceLocation != null && sourceLocation.Lock)
        {
            sourceLocation.Lock = false;
            await dependencies.LocationService.UpdateAsync(sourceLocation);
            _logger.LogInformation("任务终结导致强制释放源库位锁: {NodeRemark}", sourceLocation.NodeRemark);
        }

        if (targetLocation != null && targetLocation.Lock)
        {
            targetLocation.Lock = false;
            await dependencies.LocationService.UpdateAsync(targetLocation);
            _logger.LogInformation("任务终结导致强制释放目标库位锁: {NodeRemark}", targetLocation.NodeRemark);
        }
    }

    #endregion

    #region 任务下发逻辑

    /// <summary>
    /// 轮询下发新生成的待处理任务
    /// </summary>
    private async Task CreateNewTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dependencies = CreateDependencies(scope.ServiceProvider);

        var pendingTasks = await dependencies.UserTaskService.GetPendingTasksAsync();
        if (!pendingTasks.Any()) return;

        var locations = await dependencies.LocationService.GetAllAsync();
        var unfinishedNdcTasks = await dependencies.NdcTaskService.GetUnfinishedTasksAsync();

        foreach (var task in pendingTasks
                     .OrderBy(x => x.priority ?? int.MaxValue)
                     .ThenBy(x => x.creatTime ?? DateTime.MaxValue))
        {
            if (unfinishedNdcTasks.Any(x => x.SchedulTaskNo == GetScheduleTaskNo(task))) continue;

            await CreateNdcTaskAsync(task, locations, dependencies);
        }
    }

    /// <summary>
    /// 构建 NDC 任务并落库推送到底层系统
    /// </summary>
    private async Task CreateNdcTaskAsync(
        NdcUserTask userTask,
        List<NdcLocation> locations,
        RcsScopedDependencies dependencies)
    {
        var pickupLocation = locations.FirstOrDefault(x => x.NodeRemark == userTask.sourcePosition);
        var unloadLocation = locations.FirstOrDefault(x => x.NodeRemark == userTask.targetPosition);
        var reqCode = GetScheduleTaskNo(userTask);

        if (pickupLocation == null || unloadLocation == null)
        {
            _logger.LogWarning("任务 {RequestCode} 起点或终点库位不存在，直接标记为已取消", reqCode);
            userTask.taskStatus = NdcTaskStatuEnum.Canceled;
            await dependencies.UserTaskService.UpdateAsync(userTask);
            return;
        }

        var existingTask = await dependencies.NdcTaskService.FindByScheduleTaskNoAsync(reqCode);
        if (existingTask != null)
        {
            _logger.LogWarning("任务 {RequestCode} 的 NDC 执行记录已被意外创建，跳过重复下发", reqCode);
            return;
        }

        try
        {
            var ndcTask = new NdcTaskMove(
                Guid.NewGuid(),
                Guid.NewGuid(),
                userTask.taskType.ToString(),
                0,
                reqCode,
                (int)userTask.taskType,
                "K",
                Convert.ToInt32(pickupLocation.Name),
                pickupLocation.LiftingHeight,
                Convert.ToInt32(unloadLocation.Name),
                unloadLocation.UnloadHeight,
                0);

            await dependencies.NdcTaskService.InsertAsync(ndcTask);
            _logger.LogInformation("成功下发并建立 NDC 调度工单, RequestCode: {RequestCode}, 源:{Source}, 终:{Target}", 
                reqCode, pickupLocation.NodeRemark, unloadLocation.NodeRemark);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构造 NDC 通信工单失败，无法投递: RequestCode={RequestCode}", reqCode);
        }
    }

    #endregion

    #region 工具辅助与信道聚合层

    /// <summary>
    /// 根据任务状态变化触发上位机主动交互。
    /// 当前仅实现两个示例：
    /// 1) 物料达到生产线信息（示例：入库任务进入卸货状态）
    /// 2) 作业完成反馈（任务组全部进入终态后发送）
    /// </summary>
    private async Task TryTriggerUpstreamInteractionsAsync(
        NdcUserTask userTask,
        NdcTaskStatuEnum newStatus,
        RcsScopedDependencies dependencies)
    {
        if (ShouldNotifyMaterialArrived(userTask, newStatus))
        {
            await dependencies.OutboundInteractionService.NotifyMaterialArrivedAsync(userTask);
        }

        var completedStatus = ConvertToJobCompletedStatus(newStatus);
        if (!completedStatus.HasValue)
        {
            return;
        }

        var taskNumber = ResolveTaskNumber(userTask);
        if (string.IsNullOrWhiteSpace(taskNumber))
        {
            _logger.LogWarning("跳过作业完成反馈：taskNumber为空，RequestCode={RequestCode}", userTask.requestCode);
            return;
        }

        if (!string.IsNullOrWhiteSpace(userTask.taskGroupNo))
        {
            var hasUnfinishedTasks = await dependencies.UserTaskService.ExistsUnfinishedTasksInGroupAsync(userTask.taskGroupNo);
            if (hasUnfinishedTasks)
            {
                return;
            }
        }

        await dependencies.OutboundInteractionService.NotifyJobCompletedAsync(taskNumber, completedStatus.Value);
    }

    /// <summary>
    /// 物料达到生产线触发条件示例：入库类型任务进入卸货阶段时上报。
    /// </summary>
    private static bool ShouldNotifyMaterialArrived(NdcUserTask userTask, NdcTaskStatuEnum newStatus)
    {
        return userTask.taskType == NdcTaskTypeEnum.In && newStatus == NdcTaskStatuEnum.Unloading;
    }

    /// <summary>
    /// 将内部任务终态映射为上位机“作业完成反馈”状态：
    /// 1=完成，2=终止/取消。
    /// </summary>
    private static int? ConvertToJobCompletedStatus(NdcTaskStatuEnum status)
    {
        return status switch
        {
            NdcTaskStatuEnum.TaskFinish => 1,
            NdcTaskStatuEnum.OrderAgvFinish => 1,
            NdcTaskStatuEnum.Canceled => 2,
            NdcTaskStatuEnum.CanceledWashFinish => 2,
            NdcTaskStatuEnum.InvalidUp => 2,
            NdcTaskStatuEnum.InvalidDown => 2,
            NdcTaskStatuEnum.RedirectRequest => 2,
            _ => null
        };
    }

    /// <summary>
    /// 反馈任务号优先使用任务组号，缺失时回退 requestCode。
    /// </summary>
    private static string ResolveTaskNumber(NdcUserTask userTask)
    {
        if (!string.IsNullOrWhiteSpace(userTask.taskGroupNo))
        {
            return userTask.taskGroupNo.Trim();
        }

        return userTask.requestCode?.Trim() ?? string.Empty;
    }

    private static string GetScheduleTaskNo(NdcUserTask userTask) => userTask.requestCode ?? string.Empty;

    private static RcsScopedDependencies CreateDependencies(IServiceProvider serviceProvider) => new(
        serviceProvider.GetRequiredService<IRcsUserTaskService>(),
        serviceProvider.GetRequiredService<IRcsNdcTaskService>(),
        serviceProvider.GetRequiredService<IRcsLocationService>(),
        serviceProvider.GetRequiredService<IRcsInteractionService>(),
        serviceProvider.GetRequiredService<IAgvOutboundInteractionService>());

    private sealed record RcsScopedDependencies(
        IRcsUserTaskService UserTaskService,
        IRcsNdcTaskService NdcTaskService,
        IRcsLocationService LocationService,
        IRcsInteractionService InteractionService,
        IAgvOutboundInteractionService OutboundInteractionService);

    #endregion
}

