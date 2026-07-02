using Client100.Entity;

using WarehouseManagementSystem.Models.PLC;

namespace WarehouseManagementSystem.Service.Plc
{
    /// <summary>
    /// PLC信号服务接口
    /// </summary>
    public interface IPlcSignalService
    {
        /// <summary>
        /// PLC 手动可控写入方标识。
        /// 约定只有该类型的信号才允许在页面执行写入与重置，
        /// 这样可以避免把 PLC 原生输出点误开放给人工操作。
        /// </summary>
        public const string ManualWritableWriter = "WMS";

        Task<List<RCS_PlcDevice>> GetAllPlcDevicesAsync();
        Task<RCS_PlcDevice> GetPlcDeviceByIdAsync(int id);
        Task<int> AddPlcDeviceAsync(RCS_PlcDevice device);
        Task UpdatePlcDeviceAsync(RCS_PlcDevice device);
        Task DeletePlcDeviceAsync(int id);
        
        /// <summary>
        /// 获取所有PLC信号
        /// </summary>
        Task<List<RCS_PlcSignal>> GetAllPlcSignalsAsync();

        /// <summary>
        /// 根据设备ID获取PLC信号
        /// </summary>
        Task<List<RCS_PlcSignal>> GetPlcSignalsByDeviceIdAsync(string deviceId, string dbBlock = null);

        Task<RCS_PlcSignal> GetPlcSignalByIdAsync(int id);
        Task<int> AddPlcSignalAsync(RCS_PlcSignal signal);
        Task UpdatePlcSignalAsync(RCS_PlcSignal signal);
        Task DeletePlcSignalAsync(int id);

        /// <summary>
        /// 重置PLC信号
        /// </summary>
        Task ResetPlcSignalAsync(int signalId);

        /// <summary>
        /// 手动触发PLC信号
        /// </summary>
        Task ManualTriggerSignalAsync(int signalId, bool value);


        Task<AutoPlcTask>GetAutoTask(string PlcType,string PLCTypeDb,string Signal,int Status);


        Task UpdateAutoTask(int Id);

        /// <summary>
        /// 初始化指定 PLC 的标准设备与信号模板。
        /// 这里用于现场快速导入成套 DB 点位，避免人工逐条录入导致出错。
        /// </summary>
        /// <param name="request">初始化参数。</param>
        /// <returns>返回初始化结果摘要。</returns>
        Task<PlcInitializationResult> InitializeDeviceSignalsAsync(PlcDeviceInitializationRequest request);

    }

    /// <summary>
    /// PLC 设备初始化请求。
    /// </summary>
    public class PlcDeviceInitializationRequest
    {
        /// <summary>
        /// 设备 IP 地址。
        /// </summary>
        public string IpAddress { get; set; } = "192.168.0.100";

        /// <summary>
        /// 端口。
        /// </summary>
        public int Port { get; set; } = 102;

        /// <summary>
        /// PLC 品牌。
        /// </summary>
        public string Brand { get; set; } = "西门子";

        /// <summary>
        /// DB 块地址。
        /// </summary>
        public string ModuleAddress { get; set; } = "DB1";

        /// <summary>
        /// 设备备注。
        /// </summary>
        public string Remark { get; set; } = "DB1数据交互PLC";

        /// <summary>
        /// 是否启用设备。
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 工位点。
        /// </summary>
        public string StationPoint { get; set; } = string.Empty;

        /// <summary>
        /// 信号请求点。
        /// </summary>
        public string SignalRequestPoint { get; set; } = string.Empty;

        /// <summary>
        /// 离开复位点。
        /// </summary>
        public string LeaveResetPoint { get; set; } = string.Empty;
    }

    /// <summary>
    /// PLC 设备初始化结果。
    /// </summary>
    public class PlcInitializationResult
    {
        /// <summary>
        /// 设备主键。
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 是否新建了设备。
        /// </summary>
        public bool DeviceCreated { get; set; }

        /// <summary>
        /// 新增信号数量。
        /// </summary>
        public int InsertedSignalCount { get; set; }

        /// <summary>
        /// 已存在而跳过的信号数量。
        /// </summary>
        public int SkippedSignalCount { get; set; }
    }
}
