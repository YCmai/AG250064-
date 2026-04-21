using System.ComponentModel.DataAnnotations.Schema;
using WarehouseManagementSystem.Models.Enums;
using NdcTaskStatuEnum = WarehouseManagementSystem.Models.Enums.TaskStatuEnum;

namespace WarehouseManagementSystem.Models.Ndc;

/// <summary>
/// NDC 用户任务实体，记录任务请求、执行过程与结果信息。
/// </summary>
public class NdcUserTask : NdcEntityBase<int>
{
    // 任务运行状态
    public NdcTaskStatuEnum taskStatus { get; set; }
    // 任务被执行的时间点
    public DateTime? executedTime { get; set; }
    // 运行时关联的执行任务 ID
    public string? runTaskId { get; set; }
    // 任务开始时间
    public DateTime? startTime { get; set; }
    // 是否已执行
    public bool executed { get; set; }
    // 任务创建时间（保留原字段名以兼容历史数据）
    public DateTime? creatTime { get; set; }
    // 任务结束时间
    public DateTime? endTime { get; set; }
    // 外部请求单号
    public string? requestCode { get; set; }
    // 任务类型
    public TaskTypeEnum taskType { get; set; }
    // 优先级（值越小优先级通常越高，具体由调度侧定义）
    public int? priority { get; set; }
    // 指定机器人编号，默认 "0" 表示未指定
    public string? robotCode { get; set; } = "0";
    // 起点库位
    public string? sourcePosition { get; set; }
    // 终点库位
    public string? targetPosition { get; set; }
    // 任务组号
    public string? taskGroupNo { get; set; }
    // 托盘号
    public string? palletNo { get; set; }
    // bin号/条码字段（用于上位机物料到达信息上报）
    public string? binNumber { get; set; }
    // 载重（单位由业务侧约定）
    public int? weight { get; set; }
    // 备注信息
    public string? remarks { get; set; }
    // 是否已取消
    public bool IsCancelled { get; set; }
}



