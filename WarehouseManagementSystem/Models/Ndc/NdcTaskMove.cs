using WarehouseManagementSystem.Shared.Ndc;
using NdcTaskStatuEnum = WarehouseManagementSystem.Shared.Ndc.TaskStatuEnum;

namespace WarehouseManagementSystem.Models.Ndc;

/// <summary>
/// NDC 任务搬运指令模型。对应数据库中的 [NdcTask_Moves] 表。
/// 该类记录了发往 ACI (NDC) 的具体搬运参数和状态。
/// </summary>
public class NdcTaskMove : TaskBaseEntity
{
    public NdcTaskMove()
    {
    }

    public NdcTaskMove(
        Guid id,
        Guid fatherTaskId,
        string fatherTaskType,
        int ndcTaskId,
        string schedulTaskNo,
        int taskType,
        string group,
        int pickupSite,
        int pickupHeight,
        int unloadSite,
        int unloadHeight,
        int priority)
    {
        Id = id;
        FatherTaskId = fatherTaskId;
        FatherTaskType = fatherTaskType;
        NdcTaskId = ndcTaskId;
        SchedulTaskNo = schedulTaskNo;
        TaskType = taskType;
        Group = group;
        PickupSite = pickupSite;
        PickupHeight = pickupHeight;
        UnloadSite = unloadSite;
        UnloadHeight = unloadHeight;
        Priority = priority;
        TaskStatus = NdcTaskStatuEnum.None;
        CreationTime = DateTime.Now;
    }

    /// <summary>NDC 系统分配的任务 ID (或调度索引)</summary>
    public int NdcTaskId { get; set; }

    /// <summary>调度系统任务序列号/业务单号</summary>
    public string? SchedulTaskNo { get; set; }

    /// <summary>任务类型代码</summary>
    public int TaskType { get; set; }

    /// <summary>区域分组 (Group)</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>取货站点地标</summary>
    public int PickupSite { get; set; }

    /// <summary>取货举升高度</summary>
    public int PickupHeight { get; set; }

    /// <summary>取货伸缩深度</summary>
    public int PickUpDepth { get; set; }

    /// <summary>卸货/投递站点地标</summary>
    public int UnloadSite { get; set; }

    /// <summary>卸货放置高度</summary>
    public int UnloadHeight { get; set; }

    /// <summary>卸货伸缩深度</summary>
    public int UnloadDepth { get; set; }

    /// <summary>NDC 任务执行状态 (对应 AciHostEventTypeEnum)</summary>
    public NdcTaskStatuEnum TaskStatus { get; set; }

    /// <summary>执行此任务的 AGV 编号</summary>
    public int AgvId { get; set; }

    /// <summary>任务优先级</summary>
    public int Priority { get; set; }

    /// <summary>执行反馈备注/报警信息</summary>
    public string? Remark { get; set; }

    /// <summary>是否请求取消</summary>
    public bool CancelTask { get; set; }

    /// <summary>订单在 NDC 中的内部索引</summary>
    public int OrderIndex { get; set; }

    public void SetStatus(NdcTaskStatuEnum taskStatu, int parameter = 0)
    {
        TaskStatus = taskStatu;

        switch (taskStatu)
        {
            case NdcTaskStatuEnum.ConfirmCar:
                SetAgvId(parameter);
                break;
            case NdcTaskStatuEnum.TaskFinish:
            case NdcTaskStatuEnum.Canceled:
            case NdcTaskStatuEnum.CanceledWashing:
            case NdcTaskStatuEnum.CanceledWashFinish:
            case NdcTaskStatuEnum.RedirectRequest:
            case NdcTaskStatuEnum.OrderAgv:
            case NdcTaskStatuEnum.OrderAgvFinish:
                SetFinishTime();
                break;
            case NdcTaskStatuEnum.InvalidUp:
            case NdcTaskStatuEnum.InvalidDown:
                SetFinishTime();
                SetRemark(parameter);
                break;
        }
    }

    public void SetNdcId(int id)
    {
        NdcTaskId = id;
    }

    public void SetAgvId(int agvId)
    {
        AgvId = agvId;
    }

    public void SetOrderIndex(int orderIndex)
    {
        OrderIndex = orderIndex;
    }

    public void SetRemark(int site)
    {
        Remark = $"存在无效站点:{site}";
    }

    public void RecoveryId()
    {
        NdcTaskId = (int)TaskState.Recycled;
    }

    public void SetFinishTime()
    {
        CloseTime = DateTime.Now;
    }

    public void SetAgvBlank(int unloadSite, int unloadHeight)
    {
        UnloadSite = unloadSite;
        UnloadHeight = unloadHeight;
    }

    public void SetUnloadDepth(int depth)
    {
        UnloadDepth = depth;
    }
}


