using WarehouseManagementSystem.Protocols.Ndc;
using WarehouseManagementSystem.Models.Ndc;
//using WarehouseManagementSystem.Protocols.Ndc.Queue;
using WarehouseManagementSystem.Services.Ndc;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NdcTaskStatuEnum = WarehouseManagementSystem.Models.Enums.TaskStatuEnum;
using NdcTaskTypeEnum = WarehouseManagementSystem.Models.Enums.TaskTypeEnum;

using System;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Services.Integrations;

namespace WarehouseManagementSystem.Services.Ndc
{
    /// <summary>
    /// ACI数据事件处理器
    /// </summary>
    /// <remarks>
    /// 负责处理所有ACI相关的事件，包括订单开始、参数确认、装卸货、任务完成等状态的处理
    /// 实现 ILocalEventHandler<AciDataEventArgs> 接口以处理本地事件
    /// </remarks>
    public class AciDataEventHandler
    {
        #region 依赖注入与基础构造

        private readonly AciAppManager _aciAppManager;
        private readonly IAciTaskDataService _taskDataService;
        private readonly IAciLocationDataService _locationDataService;
        private readonly IAciInteractionDataService _interactionDataService;
        private readonly IAgvOutboundInteractionService _agvOutboundInteractionService;
        private readonly IAgvOutboundQueueRepository _agvOutboundQueueRepository;
        private readonly ILogger<AciDataEventHandler> _logger;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IConfiguration _configuration;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        
        public AciDataEventHandler(
            AciAppManager aciAppManager, 
            IAciTaskDataService taskDataService,
            IAciLocationDataService locationDataService,
            IAciInteractionDataService interactionDataService,
            IAgvOutboundInteractionService agvOutboundInteractionService,
            IAgvOutboundQueueRepository agvOutboundQueueRepository,
            ILogger<AciDataEventHandler> logger, 
            IConfiguration configuration,
            IHttpClientFactory? httpClientFactory = null)
        {
            _aciAppManager = aciAppManager;
            _taskDataService = taskDataService;
            _locationDataService = locationDataService;
            _interactionDataService = interactionDataService;
            _agvOutboundInteractionService = agvOutboundInteractionService;
            _agvOutboundQueueRepository = agvOutboundQueueRepository;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        #endregion

        #region 日志记录辅助方法

        private void LogEventStart(string eventName, AciEvent ev)
        {
            _logger.LogInformation(
                "开始处理 ACI 事件: {EventName}, OrderIndex={OrderIndex}, Parameter1={Parameter1}, Parameter2={Parameter2}",
                eventName,
                ev.Index,
                ev.Parameter1,
                ev.Parameter2);
        }

        private void LogEventCompleted(string eventName, AciEvent ev, long elapsedMilliseconds)
        {
            _logger.LogInformation(
                "完成处理 ACI 事件: {EventName}, OrderIndex={OrderIndex}, Parameter1={Parameter1}, Parameter2={Parameter2}, ElapsedMs={ElapsedMs}",
                eventName,
                ev.Index,
                ev.Parameter1,
                ev.Parameter2,
                elapsedMilliseconds);
        }

        private void LogEventError(string eventName, AciEvent ev, Exception ex)
        {
            _logger.LogError(
                ex,
                "处理 ACI 事件失败: {EventName}, OrderIndex={OrderIndex}, Parameter1={Parameter1}, Parameter2={Parameter2}",
                eventName,
                ev.Index,
                ev.Parameter1,
                ev.Parameter2);
        }

        #endregion

        #region 全局事件分发总入口

        /// <summary>
        /// 处理ACI事件的主要方法
        /// </summary>
        /// <param name="e">ACI数据事件参数</param>
        /// <remarks>
        /// 使用信号量确保事件处理的线程安全
        /// 根据事件类型分发到不同的处理方法
        /// </remarks>
        
        public async Task HandleEventAsync(AciDataEventArgs e)
        {
            await _semaphore.WaitAsync();

            try
            {
                switch (e.AciData.DataType)
                {
                    case MessageType.OrderEvent:
                                                await HandleOrderEvent(e);
                        break;
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
        }

        /// <summary>
        /// 处理订单事件
        /// </summary>
        /// <param name="e">ACI数据事件参数</param>
        /// <remarks>
        /// 解析订单事件数据并根据不同的事件类型进行相应处理
        /// 维护事件历史记录
        /// </remarks>
        private async Task HandleOrderEvent(AciDataEventArgs e)
        {
            try
            {
                OrderEventAciData? data = e.AciData as OrderEventAciData;
                AciEvent ev = new AciEvent()
                {
                    Type = (AciHostEventTypeEnum)data.MagicCode1,
                    Parameter1 = data.MagicCode2,
                    Parameter2 = data.MagicCode3,
                    Index = data.OrderIndex
                };
                var ndcTask = await _taskDataService.FindNdcTaskByNdcTaskIdAsync(ev.Parameter2);
                switch (ev.Type)
                {
                    case AciHostEventTypeEnum.OrderStart:
                        
                        try
                        {
                            await HandleOrderStartEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("OrderStart", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.ParameterCheck:
                        try
                        {
                            _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.Confirm, 0, 0);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("ParameterCheck", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.MoveToLoad:
                        try
                        {
                            await HandleMoveToLoadEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("MoveToLoad", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.LoadHostSyncronisation:
                        try
                        {
                          
                            await HandleLoadHostSyncronisationEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("LoadHostSyncronisation", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.LoadingHostSyncronisation:
                        try
                        {

                          
                            await HandleLoadingHostSyncronisationEvent(ev);
                          
                        }
                        catch (Exception ex)
                        {
                            LogEventError("LoadingHostSyncronisation", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.UnloadHostSyncronisation:
                        try
                        {
                           
                            await HandleUnloadHostSyncronisationEvent(ev);
                           
                        }
                        catch (Exception ex)
                        {
                            LogEventError("UnloadHostSyncronisation", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.UnloadingHostSyncronisation:
                        try
                        {
                           
                            await HandleUnloadingHostSyncronisationEvent(ev);
                         
                        }
                        catch (Exception ex)
                        {
                            LogEventError("UnloadingHostSyncronisation", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.OrderFinish:
                        try
                        {
                            await HandleOrderFinishEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("OrderFinish", ev, ex);
                        }
                        break;
                   
                   
                    case AciHostEventTypeEnum.RelaseTask:
                        var relaseTaskModel = await _taskDataService.FindNdcTaskByNdcTaskIdAsync(ev.Parameter2);
                        if (relaseTaskModel != null)
                        {
                            var wmsModel = await _taskDataService.GetUserTaskByRequestCodeAsync(relaseTaskModel.SchedulTaskNo);

                            if (wmsModel != null)
                            {
                                if (!string.IsNullOrEmpty(wmsModel.taskGroupNo))
                                {
                                    var groupTasks = await _taskDataService.GetUserTasksByGroupNoAsync(wmsModel.taskGroupNo);
                                    var previousTasks = groupTasks.Where(t => t.priority < wmsModel.priority).ToList();
                                    
                                    if (previousTasks.Any(t => t.taskStatus < NdcTaskStatuEnum.TaskFinish))
                                    {
                                        return;
                                    }
                                }

                                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)AciHostEventTypeEnum.RelaseTask, 0, 0);
                            }
                        }
                        break;
                    case AciHostEventTypeEnum.End:
                        try
                        {
                            await HandleEndEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("End", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.CancelRequest:
                        try
                        {
                            await HandleCancelRequestEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("CancelRequest", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.Cancel:
                        try
                        {
                            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
                            {
                                _aciAppManager.SendHostAcknowledge(null, ev.Index, 255, 0, 0);
                                break;
                            }
                            _aciAppManager.SendHostAcknowledge(null, ev.Index, 255, 0, 0);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("Cancel", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.CarrierConnected:
                        try
                        {
                            // 当前没有实现的方法
                        }
                        catch (Exception ex)
                        {
                            LogEventError("CarrierConnected", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.Redirect:
                        try
                        {
                            _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmWashing, 400, 0);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("Redirect", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.OrderTransform:
                        try
                        {
                            await HandleOrderTransformEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("OrderTransform", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.InvalidDeliverStation:
                        try
                        {
                            await HandleInvalidDeliverStationEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("InvalidDeliverStation", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.OrderCancel:
                        try
                        {
                            await HandleOrderCancelEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("OrderCancel", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.OrderAgv:
                        try
                        {
                            await HandleOrderAgvEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("OrderAgv", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.RedirectRequestFetch:
                        try
                        {
                            await HandleRedirectRequestFetchEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("RedirectRequestFetch", ev, ex);
                        }
                        break;
                   
                    case AciHostEventTypeEnum.CarWashRequest:
                        try
                        {
                            _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmUnknown, 1160, 0);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("CarWashRequest", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.ResetStart:
                        try
                        {
                            _logger.LogCritical(
                                "收到系统重启事件: EventName={EventName}, OrderIndex={OrderIndex}",
                                "ResetStart",
                                ev.Index);
                            await HandleResetStartEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("ResetStart", ev, ex);
                        }
                        break;
                    case AciHostEventTypeEnum.ResetStart2:
                        try
                        {
                            _logger.LogCritical(
                                "收到系统重启事件: EventName={EventName}, OrderIndex={OrderIndex}",
                                "ResetStart2",
                                ev.Index);
                            await HandleResetStartEvent(ev);
                        }
                        catch (Exception ex)
                        {
                            LogEventError("ResetStart2", ev, ex);
                        }
                        break;
                 
                }

                if (ev.Type != AciHostEventTypeEnum.HostSync)
                {
                    try
                    {
                        _aciAppManager.AciEventAdd(ev);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "写入 ACI 事件历史失败: EventType={EventType}, OrderIndex={OrderIndex}", ev.Type, ev.Index);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理订单事件总入口失败");
            }
        }

        /// <summary>
        /// 处理订单开始事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为开始状态
        /// 设置订单索引
        /// </remarks>
        private async Task HandleOrderStartEvent(AciEvent ev)
        {

            var ndcTasks = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.None);
            if (ndcTasks != null)
            {
                ndcTasks.SetStatus(NdcTaskStatuEnum.TaskStart);
                ndcTasks.SetOrderIndex(ev.Index);
                await _taskDataService.UpdateNdcTaskAsync(ndcTasks);
                return;
            }
            var ndcTasksInProgress = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.TaskStart);
            if (ndcTasksInProgress != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.TaskStart, ndcTasksInProgress.PickupHeight, ndcTasksInProgress.UnloadHeight);
                return;
            }
        }

        /// <summary>
        /// 处理移动到装货点事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为确认车辆状态
        /// 设置订单索引
        /// </remarks>
        private async Task HandleMoveToLoadEvent(AciEvent ev)
        {

            var move = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.TaskStart);
            if (move != null)
            {
                move.SetStatus(NdcTaskStatuEnum.ConfirmCar, ev.Parameter1);
                move.SetOrderIndex(ev.Index);
                await _taskDataService.UpdateNdcTaskAsync(move);
            }
        }

        /// <summary>
        /// 处理装货同步事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为正在装货
        /// 发送装货高度和深度参数
        /// </remarks>
        private async Task HandleLoadHostSyncronisationEvent(AciEvent ev)
        {
          
            try
            {
               
                var load0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.ConfirmCar);
                if (load0 != null)
                {

                    load0.SetStatus(NdcTaskStatuEnum.PickingUp);
                    await _taskDataService.UpdateNdcTaskAsync(load0);
                    return;
                }

                var load1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.PickingUp);
                if (load1 != null)
                {
                    // 示例安全交互：先查记录，无记录才入队；有记录则只看状态，成功才回复 NDC。
                    var taskNumber = load1.SchedulTaskNo?.Trim();
                    if (string.IsNullOrWhiteSpace(taskNumber))
                    {
                        return;
                    }

                    var safetyRecord = await _agvOutboundQueueRepository.GetLatestByTaskNumberAndEventTypeAsync(
                        taskNumber,
                        (int)AgvOutboundEventType.SafetySignal);

                    // 无记录则创建后等待下次循环，不立即回复。
                    if (safetyRecord == null)
                    {
                        var safetyRequestTime = load1.CreationTime == default ? DateTime.Now : load1.CreationTime;
                        var room = $"NDC_LOAD_{load1.PickupSite}";
                        _logger.LogInformation(
                            "LoadHostSyncronisation 安全交互无记录，准备入队。TaskNumber={TaskNumber}, Room={Room}, RequestDate={RequestDate:yyyy-MM-dd HH:mm:ss}",
                            taskNumber,
                            room,
                            safetyRequestTime);
                        await _agvOutboundInteractionService.NotifySafetySignalAsync(taskNumber, safetyRequestTime, room);
                        return;
                    }

                    // 有记录但未成功（0待处理/2重试中/3终态失败）都不回复，等待下次循环。
                    if (safetyRecord.ProcessStatus != 1)
                    {
                        return;
                    }

                    var upHeight = load1.PickupHeight == 0 ? 0 : load1.PickupHeight;
                    var upDepth = 0;

                    _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.PickingUp, upHeight, upDepth);
                    _logger.LogInformation("安全交互完成后回复NDC，TaskNumber={TaskNumber}, QueueId={QueueId}", taskNumber, safetyRecord.ID);
                }
            }
            catch (Exception ex)
            {

                LogEventError("HandleLoadHostSyncronisationEvent", ev, ex);
            }
        }

      

    

        /// <summary>
        /// 处理装货完成同步事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为装货完成
        /// 处理安全交互信号
        /// </remarks>
        private async Task HandleLoadingHostSyncronisationEvent(AciEvent ev)
        {
          
            var vehicleAtLoad0 = await _taskDataService.FindNdcTaskByNdcTaskIdAsync(ev.Parameter2);
           
            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.PickDown, 0, 0);
                return;
            }

            var loadDone0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.PickingUp);
            if (loadDone0 != null)
            {
               
                loadDone0.SetStatus(NdcTaskStatuEnum.PickDown);

                await _taskDataService.UpdateNdcTaskAsync(loadDone0);
                return;
            }

            var loadDone1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.PickDown);
            if (loadDone1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.PickDown, 0, 0);
                return;
            }
        }

      

      

        /// <summary>
        /// 处理卸货同步事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为正在卸货
        /// 发送卸货高度和深度参数
        /// 处理站台交互逻辑：
        /// 1. 创建DO1请求进入站台的IO任务（持续信号）
        /// 2. 检测DI1信号（起升架允许进入反馈）
        /// 3. 确认DI1信号后允许AGV进入
        /// </remarks>
        private async Task HandleUnloadHostSyncronisationEvent(AciEvent ev)
        {
            // 获取当前任务信息
            var ndcTaskModel = await _taskDataService.FindNdcTaskByNdcTaskIdAsync(ev.Parameter2);
            if (ndcTaskModel == null) return;

            // 处理普通卸货任务的原有逻辑
            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.Unloading, 0, 0);
                return;
            }

            var unoad0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.PickDown);
            if (unoad0 != null)
            {
               

                unoad0.SetStatus(NdcTaskStatuEnum.Unloading);
                await _taskDataService.UpdateNdcTaskAsync(unoad0);
            }

            var unoad1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.Unloading);
            if (unoad1 != null)
            {
                var doneHeight = unoad1.UnloadHeight == 0 ? 0 : unoad1.UnloadHeight;
                var upDepth = 0;
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.Unloading, doneHeight, upDepth);
            }

            var unoad2 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.OrderAgv);
            if (unoad2 != null)
            {
                var doneHeight = unoad2.UnloadHeight == 0 ? 0 : unoad2.UnloadHeight;
                var depth = unoad2.UnloadDepth == 0 ? 0 : unoad2.UnloadDepth;
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.Unloading, doneHeight, depth);
                return;
            }

            var unoad3 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.CanceledWashing);
            if (unoad3 != null)
            {
                var doneHeight = unoad3.UnloadHeight == 0 ? 0 : unoad3.UnloadHeight;
                var depth = unoad3.UnloadDepth == 0 ? 0 : unoad3.UnloadDepth;
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.Unloading, doneHeight, depth);
                return;
            }
            
        }

        /// <summary>
        /// 处理卸货完成同步事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为卸货完成
        /// 处理站台交互信号：
        /// 1. 发送DO2脉冲信号（3秒）
        /// 2. 释放DO1持续信号
        /// </remarks>
        private async Task HandleUnloadingHostSyncronisationEvent(AciEvent ev)
        {
            // 获取当前任务信息
            var ndcTaskModel = await _taskDataService.FindNdcTaskByNdcTaskIdAsync(ev.Parameter2);
            if (ndcTaskModel == null) return;

            // 处理原有的卸货完成逻辑
            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.UnloadDown, 0, 0);
                return;
            }

            var unloadDone0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.Unloading);
            if (unloadDone0 != null)
            {
               

                unloadDone0.SetStatus(NdcTaskStatuEnum.UnloadDown);
                await _taskDataService.UpdateNdcTaskAsync(unloadDone0);
                return;
            }

            var unloadDone1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.UnloadDown);
            if (unloadDone1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.UnloadDown, 0, 0);
                return;
            }

            var unloadDone2 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.OrderAgv);
            if (unloadDone2 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.UnloadDown, 0, 0);
                return;
            }

            var unloadDone3 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.CanceledWashing);
            if (unloadDone3 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.UnloadDown, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理订单完成事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为完成状态
        /// 处理不同类型任务的完成逻辑
        /// </remarks>
        private async Task HandleOrderFinishEvent(AciEvent ev)
        {
            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.TaskFinish, 0, 0);
                return;
            }
            var finish0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.UnloadDown);
            if (finish0 != null)
            {
                finish0.SetStatus(NdcTaskStatuEnum.TaskFinish);
                await _taskDataService.UpdateNdcTaskAsync(finish0);
                return;
            }

            var finish1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.OrderAgv);

            if (finish1 != null)
            {
                finish1.SetStatus(NdcTaskStatuEnum.OrderAgvFinish);
                await _taskDataService.UpdateNdcTaskAsync(finish1);
            }

            var finish2 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.CanceledWashing);

            if (finish2 != null)
            {
                finish2.SetStatus(NdcTaskStatuEnum.CanceledWashFinish);
                await _taskDataService.UpdateNdcTaskAsync(finish2);
            }

            _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.TaskFinish, 0, 0);
        }

        /// <summary>
        /// 处理无效卸货站点事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为无效卸货点
        /// 发送确认取消指令
        /// </remarks>
        private async Task HandleInvalidDeliverStationEvent(AciEvent ev)
        {
            var invalidDown0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.InvalidDown);
            if (invalidDown0 != null)
            {
                invalidDown0.SetStatus(NdcTaskStatuEnum.InvalidDown, ev.Parameter1);
                await _taskDataService.UpdateNdcTaskAsync(invalidDown0);
                return;
            }

            var invalidDown1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.InvalidDown);
            if (invalidDown1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmCancellation, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理订单取消事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为已取消
        /// 处理洗车重定向确认
        /// </remarks>
        private async Task HandleOrderCancelEvent(AciEvent ev)
        {
            var orderCancl0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.CanceledWashing);

            if (orderCancl0 != null)
            {
                orderCancl0.SetStatus(NdcTaskStatuEnum.CanceledWashing);
                await _taskDataService.UpdateNdcTaskAsync(orderCancl0);
                return;
            }
            var orderCancl1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.CanceledWashing);

            if (orderCancl1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmRedirection, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理系统重启事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 处理所有已取消的任务
        /// 更新任务状态
        /// </remarks>
        private async Task HandleResetStartEvent(AciEvent ev)
        {
            var cancelTasks = await _taskDataService.GetNdcTasksByStatusRangeAsync(NdcTaskStatuEnum.CarWash, NdcTaskStatuEnum.TaskFinish);

            foreach (var cancelTask in cancelTasks)
            {
                cancelTask.SetStatus(NdcTaskStatuEnum.Canceled);
                await _taskDataService.UpdateNdcTaskAsync(cancelTask);
                _logger.LogCritical("系统重启后重置任务状态: SchedulTaskNo={SchedulTaskNo}", cancelTask.SchedulTaskNo);
            }
        }

        /// <summary>
        /// 处理取货请求重定向事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 处理取货失败的情况
        /// 更新任务状态为重定向请求
        /// </remarks>
        private async Task HandleRedirectRequestFetchEvent(AciEvent ev)
        {
            var fetch0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.RedirectRequest);
            if (fetch0 != null)
            {
                fetch0.SetStatus(NdcTaskStatuEnum.RedirectRequest);
                await _taskDataService.UpdateNdcTaskAsync(fetch0);
                return;
            }

            var fetch1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.RedirectRequest);
            if (fetch1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmCancellation, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理AGV订单事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 处理AGV相关的任务状态更新
        /// 发送重定向确认
        /// </remarks>
        private async Task HandleOrderAgvEvent(AciEvent ev)
        {
            if (ev.Parameter2 == (int)NdcTaskStatuEnum.CarWash)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmRedirection, 0, 0);
                return;
            }

            var orderAgv0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.OrderAgv);
            if (orderAgv0 != null)
            {
                orderAgv0.SetStatus(NdcTaskStatuEnum.OrderAgv);
                await _taskDataService.UpdateNdcTaskAsync(orderAgv0);
                return;
            }

            var orderAgv1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.OrderAgv);
            if (orderAgv1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmRedirection, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理订单转换事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 处理取货流程失败的情况
        /// 更新任务状态为无效取货点
        /// </remarks>
        private async Task HandleOrderTransformEvent(AciEvent ev)
        {
            var invalidUp0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.InvalidUp);
            if (invalidUp0 != null)
            {
                invalidUp0.SetStatus(NdcTaskStatuEnum.InvalidUp, ev.Parameter1);
                await _taskDataService.UpdateNdcTaskAsync(invalidUp0);
                return;
            }

            var invalidUp1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.InvalidUp);
            if (invalidUp1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmCancellation, 0, 0);
                return;
            }
        }

        /// <summary>
        /// 处理结束事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 回收已完成任务的ID
        /// 发送结束确认
        /// </remarks>
        private async Task HandleEndEvent(AciEvent ev)
        {
            int taskOut = 0, taskIn = 0;

            var recovery = await _taskDataService.GetNdcTasksByNdcTaskIdAsync(ev.Parameter1);
            foreach (var item in recovery)
            {
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.TaskFinish) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.Canceled) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.InvalidUp) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.InvalidDown) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.CanceledWashFinish) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.RedirectRequest) item.RecoveryId();
                if ((int)item.TaskStatus == (int)NdcTaskStatuEnum.OrderAgvFinish) item.RecoveryId();
            }
            _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.End, 0, 0);
        }

        /// <summary>
        /// 处理取消请求事件
        /// </summary>
        /// <param name="ev">ACI事件对象</param>
        /// <remarks>
        /// 更新任务状态为已取消
        /// 发送取消确认
        /// </remarks>
        private async Task HandleCancelRequestEvent(AciEvent ev)
        {
            var cancel0 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusNotAsync(ev.Parameter2, NdcTaskStatuEnum.Canceled);

            if (cancel0 != null)
            {
                cancel0.SetStatus(NdcTaskStatuEnum.Canceled);
                await _taskDataService.UpdateNdcTaskAsync(cancel0);

                return;
            }

            var cancel1 = await _taskDataService.FindNdcTaskByNdcTaskIdAndStatusAsync(ev.Parameter2, NdcTaskStatuEnum.Canceled);

            if (cancel1 != null)
            {
                _aciAppManager.SendHostAcknowledge(null, ev.Index, (int)ReplyTaskState.ConfirmCancellation, 0, 0);
                return;
            }
        }

    #endregion
    }
}
