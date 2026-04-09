export enum HeartbeatTaskStatus {
  Waiting = 0,
  Working = 1,
  Finished = 4,
  Cancel = 8
}

export enum NdcTaskStatus {
  None = -1,
  CarWash = 0,
  TaskStart = 1,
  Confirm = 2,
  ConfirmCar = 3,
  PickingUp = 4,
  PickDown = 6,
  Unloading = 8,
  UnloadDown = 10,
  TaskFinish = 11,
  Canceled = 30,
  CanceledWashing = 31,
  CanceledWashFinish = 32,
  RedirectRequest = 33,
  InvalidUp = 49,
  InvalidDown = 50,
  OrderAgv = 52,
  OrderAgvFinish = 53
}

export const HeartbeatStatusMap: Record<number, { text: string; color: string }> = {
  [HeartbeatTaskStatus.Waiting]: { text: '等待执行', color: 'blue' },
  [HeartbeatTaskStatus.Working]: { text: '正在执行', color: 'orange' },
  [HeartbeatTaskStatus.Finished]: { text: '已完成', color: 'green' },
  [HeartbeatTaskStatus.Cancel]: { text: '已取消', color: 'red' }
};

export const NdcStatusMap: Record<number, { text: string; color: string }> = {
  [NdcTaskStatus.None]: { text: '未执行', color: 'default' },
  [NdcTaskStatus.CarWash]: { text: '洗车(CarWash)', color: 'purple' },
  [NdcTaskStatus.TaskStart]: { text: '任务开始', color: 'blue' },
  [NdcTaskStatus.Confirm]: { text: '参数确认', color: 'cyan' },
  [NdcTaskStatus.ConfirmCar]: { text: '确认执行AGV', color: 'cyan' },
  [NdcTaskStatus.PickingUp]: { text: '取货中', color: 'orange' },
  [NdcTaskStatus.PickDown]: { text: '取货完成', color: 'green' },
  [NdcTaskStatus.Unloading]: { text: '卸货中', color: 'orange' },
  [NdcTaskStatus.UnloadDown]: { text: '卸货完成', color: 'green' },
  [NdcTaskStatus.TaskFinish]: { text: '任务结束', color: 'green' },
  [NdcTaskStatus.Canceled]: { text: '已取消', color: 'red' },
  [NdcTaskStatus.CanceledWashing]: { text: '取消并洗车', color: 'magenta' },
  [NdcTaskStatus.CanceledWashFinish]: { text: '取消洗车完成', color: 'green' },
  [NdcTaskStatus.RedirectRequest]: { text: '取货异常取消', color: 'red' },
  [NdcTaskStatus.InvalidUp]: { text: '无效取货点', color: 'red' },
  [NdcTaskStatus.InvalidDown]: { text: '无效卸货点', color: 'red' },
  [NdcTaskStatus.OrderAgv]: { text: '卸货异常重调度', color: 'warning' },
  [NdcTaskStatus.OrderAgvFinish]: { text: '重调度结束', color: 'green' }
};

export const getStatusInfo = (status: number, systemType: 'Heartbeat' | 'NDC') => {
  const map = systemType === 'NDC' ? NdcStatusMap : HeartbeatStatusMap;
  return map[status] || { text: `未知状态(${status})`, color: 'default' };
};
