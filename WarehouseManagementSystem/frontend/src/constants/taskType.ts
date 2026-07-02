export enum HeartbeatTaskType {
  PlatingToBuffer = 1,
  BufferToAssembly = 2,
  AssemblyToPlatingEmpty = 3
}

export enum NdcTaskType {
  MesMove = 1,
  MesEmptyPallet = 2,
  MesEmptyBin = 3,
  MesFullBin = 4,
  MesReturn = 5,
  Manual = 100,
  FeedToLineSide = 101
}

export const HeartbeatTaskTypeMap: Record<number, string> = {
  [HeartbeatTaskType.PlatingToBuffer]: '上料',
  [HeartbeatTaskType.BufferToAssembly]: '下料',
  [HeartbeatTaskType.AssemblyToPlatingEmpty]: '空储位到上料架'
};

export const NdcTaskTypeMap: Record<number, string> = {
  [NdcTaskType.MesMove]: '运料',
  [NdcTaskType.MesEmptyPallet]: '取空托',
  [NdcTaskType.MesEmptyBin]: '送空bin',
  [NdcTaskType.MesFullBin]: '取满bin',
  [NdcTaskType.MesReturn]: '退料',
  [NdcTaskType.Manual]: '人工任务',
  [NdcTaskType.FeedToLineSide]: '到线边'
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
