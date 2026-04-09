namespace WarehouseManagementSystem.Shared.Ndc;

public enum TaskTypeEnum
{
    In = 1, // 入库任务
    Out = 2, // 出库任务
    ParentPallet = 3, // 母托盘/空托盘出入库
    Transfer = 4 // 搬运转移任务
}

public enum TaskStatuEnum
{
    None = -1, // 尚未开始/未分配
    CarWash = 0, // 洗车任务/回待命区
    TaskStart = 1, // 任务正式开始
    Confirm = 2, // 等待确认
    ConfirmCar = 3, // 确认分配车辆
    PickingUp = 4, // 正在取货/上升中
    PickDown = 6, // 取货完毕下降中
    Unloading = 8, // 正在卸货/上升中
    UnloadDown = 10, // 卸货完毕下降中
    TaskFinish = 11, // 任务顺利完成
    Canceled = 30, // 任务被手动/异常取消
    CanceledWashing = 31, // 洗车被取消中
    CanceledWashFinish = 32, // 洗车被取消完成
    RedirectRequest = 33, // 重新请求重定向目的地
    InvalidUp = 49, // 错误/无效上升状态
    InvalidDown = 50, // 错误/无效下降状态
    OrderAgv = 52,
    OrderAgvFinish = 53
}

public enum ReplyTaskState
{
    TaskStart = 1, // 任务正式开始
    Confirm = 2, // 等待确认
    ConfirmCar = 3, // 确认分配车辆
    PickingUp = 4, // 正在取货/上升中
    PickDown = 6, // 取货完毕下降中
    Unloading = 8, // 正在卸货/上升中
    UnloadDown = 10, // 卸货完毕下降中
    TaskFinish = 11, // 任务顺利完成
    ConfirmCancellation = 143,
    ConfirmWashing = 254,
    RedirectOrNot = 141,
    PickRedirectOrNot = 140,
    ConfirmRedirection = 142,
    ConfirmUnknown = 142,
    End = 153
}

public enum TaskState
{
    Wait = 0,
    Recycled = -1
}

public enum PriorityEnum
{
    None = 0,
    ExecuteNow = 1
}




