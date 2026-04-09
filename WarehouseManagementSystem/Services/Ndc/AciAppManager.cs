using System.Collections.Concurrent;
using WarehouseManagementSystem.Protocols.Ndc;
using WarehouseManagementSystem.Shared.Ndc;

namespace WarehouseManagementSystem.Services.Ndc;

/// <summary>
/// ACI 应用程序管理器。负责维持 NDC 下层基于 ACI 协议的连接，并维护收发心跳、事件缓存与委托转发。
/// </summary>
public class AciAppManager : IDisposable
{
    #region 字段与属性
    
    private const int DefaultServerPort = 30001;
    private readonly AciConnection _aciClient;
    private readonly ConcurrentQueue<AciEvent> _aciEventQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private int _serverPort = DefaultServerPort;

    /// <summary>
    /// 当前缓存保留的有效 ACI 事件数量
    /// </summary>
    public int AciEventCount { get; set; }

    /// <summary>
    /// 获取底层的 ACI 连接客户端
    /// </summary>
    public AciConnection AciClient => _aciClient;

    /// <summary>
    /// 获取事件队列对象
    /// </summary>
    public ConcurrentQueue<AciEvent> AciEventQueue => _aciEventQueue;
    
    #endregion

    #region 生命周期 (构造与销毁)

    public AciAppManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _aciEventQueue = new ConcurrentQueue<AciEvent>();
        _aciClient = new AciConnection();

        // 注册连接与数据接收事件
        _aciClient.RequestDataReceived += AciClient_RequestDataReceived;
        _aciClient.ConnectedChanged += AciClient_ConnectedChanged;
        
        // 初始化连接目标，底层库会在设置目标端点后自行维护连接。
        _aciClient.SetServerLocalIP(_serverPort);
    }

    public void Dispose()
    {
        _aciClient.RequestDataReceived -= AciClient_RequestDataReceived;
        _aciClient.ConnectedChanged -= AciClient_ConnectedChanged;
        _aciClient.Dispose();
    }
    
    #endregion

    #region 内部事件接收与分发监听

    /// <summary>
    /// 数据接收事件：将下位机通过 ACI 发来的数据传递给独立的处理器 `AciDataEventHandler` 异步执行
    /// </summary>
    private async void AciClient_RequestDataReceived(object? sender, AciDataEventArgs e)
    {
        using var scope = _scopeFactory.CreateScope();
        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<AciDataEventHandler>();
            await handler.HandleEventAsync(e);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<ILogger<AciAppManager>>();
            logger?.LogError(ex, "处理 ACI 数据接收回调时发生未捕获异常");
        }
    }

    /// <summary>
    /// 当连接状态发生改变时触发：如果重连成功则下发全局参数读请求以触发握手动作
    /// </summary>
    private void AciClient_ConnectedChanged(object? sender, EventArgs e)
    {
        SendGlobalParamRead(InitialHostCallBack, 0, 1);
    }

    /// <summary>
    /// 初次主机设备通信握手的回调动作
    /// </summary>
    private void InitialHostCallBack(AciCommandData data)
    {
        try
        {
            if (data.Acknowledged && data.AcknowledgeData is GlobalParamStatusAciData ack && ack.ParamValues.Length >= 1)
            {
                SendGlobalParamWrite(null, 0, 1, new[] { 2 });

                if (ack.ParamValues[0] == 2)
                {
                    Thread.Sleep(1000);
                    SendGlobalParamWrite(null, 0, 1, new[] { 2 });
                    return;
                }
            }

            if (AciClient.Connected)
            {
                SendGlobalParamRead(InitialHostCallBack, 0, 1);
            }
        }
        catch (Exception ex)
        {
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<AciAppManager>>();
            logger?.LogError(ex, "执行 ACI 主机握手回调时发生未捕获异常");
        }
    }
    
    #endregion

    #region 公共管理与控制 API 方法

    /// <summary>
    /// 确保 ACI 到 NDC 的连接处于可用状态。
    /// 如果当前未连接，则重新设置服务端端点，触发底层库重新建立连接。
    /// </summary>
    public void EnsureConnected()
    {
        if (AciClient.Connected)
        {
            return;
        }

        _aciClient.SetServerLocalIP(_serverPort);
    }

    /// <summary>
    /// 主动触发一次重连。
    /// 当前底层库没有显式 Connect 方法，因此通过重新设置服务端端点来触发连接流程。
    /// </summary>
    public void Reconnect()
    {
        _aciClient.SetServerLocalIP(_serverPort);
    }

    /// <summary>
    /// 将产生的业务流水事件压入缓存队列中，超出阈值时自动移除过时记录
    /// </summary>
    public void AciEventAdd(AciEvent e)
    {
        AciEventQueue.Enqueue(e);

        if (AciEventCount++ > 100 && AciEventQueue.TryDequeue(out var remove))
        {
            remove.SetOverDate();
            AciEventCount--;
        }
    }

    /// <summary>
    /// 发送全局参数读取命令
    /// </summary>
    public AciCommandData SendGlobalParamRead(AciCommandCallBack? callback, int index, int number)
    {
        return AciClient.SendGlobalParamRead(callback, index, number);
    }

    /// <summary>
    /// 发送全局参数写入命令
    /// </summary>
    public AciCommandData SendGlobalParamWrite(AciCommandCallBack? callback, int index, int number, int[] vals)
    {
        return AciClient.SendGlobalParamWrite(callback, index, number, vals);
    }

    /// <summary>
    /// 回复主机确认信号（基于插入本地参数发送标准回复格式）
    /// </summary>
    public AciCommandData SendHostAcknowledge(AciCommandCallBack? callback, int oix, int hostack, int ack1, int ack2)
    {
        return SendLocalParamInsert(callback, oix, 18, new[] { hostack, ack1, ack2 });
    }

    /// <summary>
    /// 向特定的局部参数表内插入本地参数数据
    /// </summary>
    public AciCommandData SendLocalParamInsert(AciCommandCallBack? callback, int oix, int pix, int[] pvals)
    {
        return AciClient.SendLocalParamInsert(callback, oix, pix, pvals);
    }

    /// <summary>
    /// 触发并发送全新的 Order 初始建单（下发）指令到设备核心调度层
    /// </summary>
    public AciCommandData SendOrderInitial(AciCommandCallBack? callback, int key, int trp, int pri, int[] vals)
    {
        return AciClient.SendOrderInitial(callback, key, trp, pri, vals);
    }

    /// <summary>
    /// 撤销并删除之前下发建立的订单队列（取消任务）
    /// </summary>
    public AciCommandData SendOrderDeleteViaOrder(AciCommandCallBack? callback, int index)
    {
        return AciClient.SendOrderDeleteViaOrder(callback, index);
    }
    
    #endregion
}


