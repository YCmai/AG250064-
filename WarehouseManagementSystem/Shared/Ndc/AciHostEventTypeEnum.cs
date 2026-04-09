namespace WarehouseManagementSystem.Shared.Ndc;

/// <summary>
/// NDC ACI 协议的主机事件类型枚举。
/// 数值与 <see cref="TaskStatuEnum"/> 中部分阶段对应，便于状态同步与解释。
/// </summary>
public enum AciHostEventTypeEnum
{
    // ─── 任务执行阶段（与 TaskStatuEnum 数值一一对应）────────────────────────────

    /// <summary>任务开始（对应 TaskStatuEnum.TaskStart = 1）</summary>
    OrderStart = 1,

    /// <summary>参数检查（TaskStart 阶段内部子步骤）</summary>
    ParameterCheck = 2,

    /// <summary>移动至取货点（对应 TaskStatuEnum.PickingUp = 4）</summary>
    MoveToLoad = 3,

    /// <summary>等待取货主机同步</summary>
    LoadHostSyncronisation = 4,

    /// <summary>正在取货/举升中</summary>
    Loading = 5,

    /// <summary>取货完成主机同步</summary>
    LoadingHostSyncronisation = 6,

    /// <summary>移动至卸货点（对应 TaskStatuEnum.Unloading = 8）</summary>
    MovingToUnload = 7,

    /// <summary>等待卸货主机同步</summary>
    UnloadHostSyncronisation = 8,

    /// <summary>正在卸货/举升中</summary>
    Unloading = 9,

    /// <summary>卸货完成主机同步</summary>
    UnloadingHostSyncronisation = 10,

    /// <summary>任务顺利完成（对应 TaskStatuEnum.TaskFinish = 11）</summary>
    OrderFinish = 11,

    // ─── 系统同步与主机交互事件 ──────────────────────────────────────────────────

    /// <summary>释放任务（主机下发取消指令前的释放操作）</summary>
    RelaseTask = 20,

    /// <summary>主机同步（对应 TaskStatuEnum.ConfirmCar = 3 前的握手）</summary>
    HostSync = 30,

    /// <summary>接收到新订单/任务（NDC 已接收任务）</summary>
    OrderReceived = 31,

    /// <summary>载具已分配连接（对应 TaskStatuEnum.CarrierConnected ≈ ConfirmCar = 3）</summary>
    CarrierConnected = 32,

    /// <summary>取货路径重定向请求（对应 TaskStatuEnum.RedirectRequest = 33）</summary>
    RedirectRequestFetch = 33,

    /// <summary>放货路径重定向/订单主动取消（对应 TaskStatuEnum.Canceled = 30 的请求侧触发）</summary>
    RedirectRequestDeliver = 34,
    /// <summary>订单主动取消（同值 34，用于明确语义区分）</summary>
    OrderCancel = 34,

    /// <summary>订单執行完成（正常完成确认）</summary>
    OrderComplete = 35,

    // ─── 载具区域管控事件 ─────────────────────────────────────────────────────────

    /// <summary>订单转化/变更（异常状态，对应 TaskStatuEnum.OrderAgv = 52 前的转换）</summary>
    OrderTransform = 49,

    /// <summary>AGV 分配订单（对应 TaskStatuEnum.OrderAgv = 52）</summary>
    OrderAgv = 52,

    /// <summary>取消请求（主机发起，对应 IsCancelled 标记触发）</summary>
    CancelRequest = 48,

    /// <summary>无效卸货站点（投递目标异常）</summary>
    InvalidDeliverStation = 50,

    // ─── 洗车 / 充电事件 ─────────────────────────────────────────────────────────

    /// <summary>洗车/充电请求（对应 TaskStatuEnum.CarWash = 0）</summary>
    CarWashRequest = 60,

    /// <summary>洗车/充电失败（对应 TaskStatuEnum.CanceledWashing = 31）</summary>
    CarWashFailed = 61,

    /// <summary>洗车/充电完成（对应 TaskStatuEnum.CanceledWashFinish = 32）</summary>
    CarWashComplete = 62,

    /// <summary>载具继续（洗车后恢复执行）</summary>
    CarrierContinue = 63,

    /// <summary>充电开始</summary>
    CarChargeStart = 70,

    /// <summary>充电结束</summary>
    CarChargeEnd = 71,

    /// <summary>充电异常</summary>
    CarChargeError = 75,

    // ─── AGV 区域管控事件 ─────────────────────────────────────────────────────────

    /// <summary>AGV 已进入指定区域</summary>
    AGVInRegion = 79,

    /// <summary>AGV 请求进入区域</summary>
    AGVRequestEnterRegion = 80,

    /// <summary>AGV 已离开指定区域</summary>
    AGVOutRegion = 81,

    /// <summary>AGV 区域状态更新</summary>
    AGVRegionStatusUpdate = 82,

    // ─── 重定向确认 ───────────────────────────────────────────────────────────────

    /// <summary>验证码激活/有效键值</summary>
    ValidKey = 45,

    /// <summary>提取货物异常/取货失败（对应 TaskStatuEnum.InvalidUp = 49）</summary>
    FetchError = 46,

    /// <summary>投递货物异常/放货失败（对应 TaskStatuEnum.InvalidDown = 50）</summary>
    DeliverError = 47,

    /// <summary>取货重定向确认（主机回应选择）</summary>
    PickRedirectOrNot = 140,

    /// <summary>卸货重定向确认（主机回应选择）</summary>
    UnloadRedirectOrNot = 141,

    /// <summary>重定向信号（协议内部使用）</summary>
    Redirect = 254,

    /// <summary>取消信号（协议内部使用）</summary>
    Cancel = 255,

    /// <summary>结束会话（协议内部使用）</summary>
    End = 153,

    // ─── 系统控制事件（6000 段）──────────────────────────────────────────────────

    /// <summary>暖启动（重新连接，保留任务状态）</summary>
    WarmStart = 6001,

    /// <summary>冷启动（全局重置，清空任务状态）</summary>
    ColdStart = 6002,

    /// <summary>载具/车辆重连（等同 ResetStart）</summary>
    ConnectionCarrier = 6003,

    /// <summary>中央控制重启</summary>
    CentralControlRestart = 6004,

    // ─── 系统重启别名（兼容旧事件处理器）────────────────────────────────────────────

    /// <summary>系统重启（载具重连 6003，同 ConnectionCarrier）</summary>
    ResetStart = 6003,

    /// <summary>系统重启2（冷启动 6002，同 ColdStart）</summary>
    ResetStart2 = 6002,

    // ─── 特殊标记 ─────────────────────────────────────────────────────────────────

    /// <summary>储位/位置上报（用于定位同步）</summary>
    Location = 252,

    /// <summary>未定义/空事件</summary>
    None = 0,
}
