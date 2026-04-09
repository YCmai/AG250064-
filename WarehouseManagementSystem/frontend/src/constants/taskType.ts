export enum HeartbeatTaskType {
  PlatingToBuffer = 1,
  BufferToAssembly = 2,
  AssemblyToPlatingEmpty = 3
}

export enum NdcTaskType {
  Transport = 1,
  Charge = 2
}

export const HeartbeatTaskTypeMap: Record<number, string> = {
  [HeartbeatTaskType.PlatingToBuffer]: '上料',
  [HeartbeatTaskType.BufferToAssembly]: '下料',
  [HeartbeatTaskType.AssemblyToPlatingEmpty]: '空储位到上料架'
};

export const NdcTaskTypeMap: Record<number, string> = {
  [NdcTaskType.Transport]: '搬运',
  [NdcTaskType.Charge]: '充电'
};

export const getTaskTypeInfo = (type: number, systemType: 'Heartbeat' | 'NDC') => {
  const map = systemType === 'NDC' ? NdcTaskTypeMap : HeartbeatTaskTypeMap;
  return map[type] || `未知类型(${type})`;
};

export const getTaskTypeOptions = (systemType: 'Heartbeat' | 'NDC') => {
  const map = systemType === 'NDC' ? NdcTaskTypeMap : HeartbeatTaskTypeMap;
  return Object.entries(map).map(([value, label]) => ({
    value: Number(value),
    label
  }));
};
