using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WarehouseManagementSystem.Infrastructure.Ndc;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Shared.Ndc;
using NdcTaskStatuEnum = WarehouseManagementSystem.Shared.Ndc.TaskStatuEnum;

namespace WarehouseManagementSystem.Services.Ndc;

/// <summary>
/// ACI 任务派发长服务框架。负责将转化好的内部 NDC 任务源源不断投递向 ACI 硬件接驳层。
/// </summary>
public class AciSendTaskHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AciAppManager _aciAppManager;
    private readonly ILogger<AciSendTaskHostedService> _logger;

    #region 生命周期构建
    
    public AciSendTaskHostedService(
        IServiceScopeFactory scopeFactory,
        AciAppManager aciAppManager,
        ILogger<AciSendTaskHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _aciAppManager = aciAppManager;
        _logger = logger;
    }
    
    #endregion

    #region 主服务轮询 (ExecuteAsync)

    /// <summary>
    /// 定期扫描需要执行的待发 NDC 数据记录，通过解析状态投递到系统接口。5秒长轮询间隔。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            // 当底层的 ACI 核心连接断开时，不需要继续发送数据或挤压丢帧跳单
            if (!_aciAppManager.AciClient.Connected)
            {
                continue;
            }

            try
            {
                await ProcessTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台轮询调用并下发 NDC 任务时发生未知异常阻塞。");
            }
        }
    }
    
    #endregion

    #region 核心任务处理派发逻辑 (ProcessTasksAsync)

    /// <summary>
    /// 处理待投递的指令列表：获取 ID 号、受制于同组数上限控停控制、建立底层指令通信模型包并实际发送。
    /// </summary>
    private async Task ProcessTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ndcTaskRepository = scope.ServiceProvider.GetRequiredService<IEntityRepository<NdcTaskMove>>();
        var groupMaxTaskCountCategory = scope.ServiceProvider.GetRequiredService<IGroupMaxTaskCountCategory>();

        // 1. 获取尚未得到实际任务调度 ID，处在原始等待区 (NdcTaskId == Wait) 且尚未激活的占位任务记录
        var waitList = await ndcTaskRepository.GetListAsync(i =>
            i.TaskStatus == NdcTaskStatuEnum.None &&
            i.NdcTaskId == (int)TaskState.Wait);

        if (waitList.Any())
        {
            var waitGroups = waitList.GroupBy(p => p.Group);
            
            // 查询所有当前可能正在使用的或者已经被占用过的不可复用的调度 Id （防止冲突）
            var idHasExecution = (await ndcTaskRepository.GetListAsync(p =>
                    p.NdcTaskId != (int)TaskState.Recycled &&
                    p.NdcTaskId != (int)TaskState.Wait))
                .Select(i => i.NdcTaskId)
                .ToList();

            foreach (var group in waitGroups)
            {
                // 检测同一组别下的已经投入并发活跃作业的任务规模占用标识组
                var idHasUse = (await ndcTaskRepository.GetListAsync(p =>
                        p.NdcTaskId != (int)TaskState.Recycled &&
                        p.NdcTaskId != (int)TaskState.Wait &&
                        p.Group == group.Key))
                    .Select(i => i.NdcTaskId)
                    .ToList();

                // 取这个分组（Group）下的排序后任务序列进行投递尝试
                var waitGroupList = group.OrderByDescending(p => p.Priority).ToList();
                foreach (var item in waitGroupList)
                {
                    // 若超过此特定群落最大任务允许接纳的派单数量阈值上线，执行旧数据号流转归档回收
                    var maxCount = groupMaxTaskCountCategory.GetMaxTaskCount(item.Group);
                    if (idHasUse.Count >= maxCount)
                    {
                        var recoveryTasks = await ndcTaskRepository.GetListAsync(i =>
                            ((int)i.TaskStatus == (int)NdcTaskStatuEnum.TaskFinish ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.Canceled ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.InvalidUp ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.InvalidDown ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.CanceledWashFinish ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.RedirectRequest ||
                             (int)i.TaskStatus == (int)NdcTaskStatuEnum.OrderAgvFinish) &&
                            i.NdcTaskId != (int)TaskState.Recycled);

                        foreach (var recoveryTask in recoveryTasks)
                        {
                            recoveryTask.RecoveryId();
                        }

                        // 更新数据库将已经回收归档任务标记为废弃不再进入冲突视野
                        await ndcTaskRepository.UpdateManyAsync(recoveryTasks, true);
                        break; 
                    }

                    // 根据有效策略请求返回随机不冲突的空闲运行 Id 提供本次运使
                    var newId = GetRandom.getIds(idHasExecution, 1, 10000);
                    if (newId == 0)
                    {
                        break;
                    }

                    // 对本次将要跑起的真实运载建立主键赋值防碰处理
                    item.SetNdcId(newId);
                    await ndcTaskRepository.UpdateAsync(item, true);
                    idHasUse.Add(newId);
                    idHasExecution.Add(newId);
                }
            }
        }

        // 2. 拿到刚才已经被成功分派下发随机 Id (NdcTaskId) 并且等候发车，仍然处于 None (刚建单) 准备期的队伍队列
        var tasks = (await ndcTaskRepository.GetListAsync(i =>
                i.TaskStatus == NdcTaskStatuEnum.None &&
                i.NdcTaskId != (int)TaskState.Wait &&
                i.NdcTaskId != (int)TaskState.Recycled))
            .OrderBy(i => i.Priority)
            .ToList();

        // 循环下发设备启动交互参数命令（即实际派单到下位机中控引擎环节）
        foreach (var item in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _aciAppManager.SendOrderInitial(
                null,
                item.NdcTaskId,
                1,
                item.Priority,
                new[] { item.NdcTaskId, item.PickupSite, item.UnloadSite });

            // 适当缩短防洪水缓冲时间，从 1000ms 减少到 200ms
            await Task.Delay(200, cancellationToken);
        }

        // 3. 处理在上游标记为由于种种原因被系统中断作废（即强行撤单取消的退市清盘队列处理）
        var cancelList = await ndcTaskRepository.GetListAsync(i =>
            i.CancelTask &&
            i.TaskStatus > NdcTaskStatuEnum.CarWash &&
            i.TaskStatus < NdcTaskStatuEnum.TaskFinish &&
            !string.IsNullOrEmpty(i.SchedulTaskNo));

        foreach (var cancelTask in cancelList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 调度层面从总线摘走此订单不再派发其未执行步骤动作
            _aciAppManager.SendOrderDeleteViaOrder(null, cancelTask.OrderIndex);
            
            await Task.Delay(200, cancellationToken);
        }
    }
    
    #endregion
}


