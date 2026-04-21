using System.Collections.Concurrent;
using System.Threading;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Protocols.Ndc;

namespace WarehouseManagementSystem.Services.Ndc;

/// <summary>
/// ACI 应用程序管理器。负责维持 NDC 下层基于 ACI 协议的连接，并维护收发心跳、事件缓存与委托转发。
/// </summary>
public class AciAppManager : IDisposable
{
    #region 字段与属性

    private const int DefaultLocalPort = 30001;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HandshakeObserveInterval = TimeSpan.FromMilliseconds(500);

    private readonly AciConnection _aciClient;
    private readonly ConcurrentQueue<AciEvent> _aciEventQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AciAppManager> _logger;
    private readonly CancellationTokenSource _reconnectCts = new();
    private readonly Task _reconnectTask;
    private CancellationTokenSource? _handshakeCts;
    private Task? _handshakeTask;
    private int _handshakeStarted;

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

    public AciAppManager(IServiceScopeFactory scopeFactory, ILogger<AciAppManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _aciEventQueue = new ConcurrentQueue<AciEvent>();
        _aciClient = new AciConnection();

        // 注册连接与数据接收事件
        _aciClient.RequestDataReceived += AciClient_RequestDataReceived;
        _aciClient.ConnectedChanged += AciClient_ConnectedChanged;

        // 初始化监听端口，并启动轻量重连巡检
        ResetLocalEndpoint();
        _reconnectTask = Task.Run(() => RunReconnectLoopAsync(_reconnectCts.Token));
    }

    public void Dispose()
    {
        _reconnectCts.Cancel();
        _aciClient.RequestDataReceived -= AciClient_RequestDataReceived;
        _aciClient.ConnectedChanged -= AciClient_ConnectedChanged;
        _aciClient.Dispose();
        _reconnectCts.Dispose();
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
        if (_aciClient.Connected)
        {
            _logger.LogInformation("ACI 连接已建立，开始执行握手流程");
            StartHandshake();
            return;
        }

        CancelHandshake();
        Interlocked.Exchange(ref _handshakeStarted, 0);
        _logger.LogWarning("ACI 连接已断开，等待后台主动重连");
    }

    /// <summary>
    /// 初次主机设备通信握手的回调动作
    /// </summary>
    private void InitialHostCallBack(AciCommandData data)
    {
        // 握手已改为独立异步流程驱动，保留该方法仅为兼容旧调用点。
    }

    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ReconnectInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_aciClient.Connected)
                {
                    continue;
                }

                try
                {
                    ResetLocalEndpoint();
                    _logger.LogInformation("检测到 ACI 未连接，已重新绑定本地监听端口 {Port}", DefaultLocalPort);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "执行 ACI 主动重连时发生异常");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 应用关闭时正常退出后台循环。
        }
    }

    private void ResetLocalEndpoint()
    {
        _aciClient.SetServerLocalIP(DefaultLocalPort);
    }

    private void StartHandshake()
    {
        if (Interlocked.Exchange(ref _handshakeStarted, 1) == 1)
        {
            return;
        }

        CancelHandshake();
        _handshakeCts = new CancellationTokenSource();
        _handshakeTask = Task.Run(() => ExecuteHandshakeAsync(_handshakeCts.Token));
    }

    private void CancelHandshake()
    {
        if (_handshakeCts == null)
        {
            return;
        }

        try
        {
            _handshakeCts.Cancel();
            _handshakeCts.Dispose();
        }
        catch
        {
            // 忽略握手取消阶段的释放异常。
        }
        finally
        {
            _handshakeCts = null;
            _handshakeTask = null;
        }
    }

    private async Task ExecuteHandshakeAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && AciClient.Connected)
            {
                var readResult = await SendGlobalParamReadAsync(0, 1, cancellationToken);
                LogGlobalParamCommandResult("Read", readResult);
                if (!TryGetHandshakeStatus(readResult, out var paramValue))
                {
                    _logger.LogWarning("ACI 握手读取全局参数失败，500ms 后重试");
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    continue;
                }

                _logger.LogInformation("ACI 握手读取成功，当前参数值={ParamValue}", paramValue);

                var writeResult = await SendGlobalParamWriteAsync(0, 1, new[] { 2 }, cancellationToken);
                LogGlobalParamCommandResult("Write", writeResult);
                if (writeResult.ErrorCode != AciCommandErrorCode.None)
                {
                    _logger.LogWarning("ACI 握手写入参数 2 失败，ErrorCode={ErrorCode}", writeResult.ErrorCode);
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    continue;
                }

                _logger.LogInformation("ACI 握手已发送全局参数写入 2");

                if (paramValue == 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                    var finalWriteResult = await SendGlobalParamWriteAsync(0, 1, new[] { 2 }, cancellationToken);
                    LogGlobalParamCommandResult("WriteConfirm", finalWriteResult);
                    if (finalWriteResult.ErrorCode != AciCommandErrorCode.None)
                    {
                        _logger.LogWarning("ACI 握手补发参数 2 失败，ErrorCode={ErrorCode}", finalWriteResult.ErrorCode);
                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                        continue;
                    }

                    var verifyResult = await SendGlobalParamReadAsync(0, 1, cancellationToken);
                    LogGlobalParamCommandResult("ReadVerify", verifyResult);
                    if (TryGetHandshakeStatus(verifyResult, out var verifiedValue) && verifiedValue == 2)
                    {
                        _logger.LogInformation("ACI 握手完成，写后回读确认值={ParamValue}", verifiedValue);
                        await ObservePostHandshakeReadsAsync(cancellationToken);
                        return;
                    }

                    _logger.LogWarning("ACI 握手写后回读未确认到 2，当前值={ParamValue}，500ms 后重试", verifiedValue);
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 连接断开或服务停止时，握手取消属于正常流程。
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行 ACI 握手流程时发生未捕获异常");
        }
        finally
        {
            Interlocked.Exchange(ref _handshakeStarted, 0);
        }
    }

    private bool TryGetHandshakeStatus(AciCommandData commandData, out int paramValue)
    {
        paramValue = 0;

        if (commandData.ErrorCode != AciCommandErrorCode.None)
        {
            return false;
        }

        if (commandData.AcknowledgeData is not GlobalParamStatusAciData ack || ack.ParamValues.Length < 1)
        {
            return false;
        }

        paramValue = ack.ParamValues[0];
        return true;
    }

    private void LogGlobalParamCommandResult(string phase, AciCommandData commandData)
    {
        var request = commandData.RequestData as GlobalParamCommandAciData;
        var ack = commandData.AcknowledgeData as GlobalParamStatusAciData;
        var requestMagicIndex = request?.MagicIndex;
        var ackMagicIndex = ack?.MagicIndex;
        var ackParamValue = ack?.ParamValues != null && ack.ParamValues.Length > 0
            ? ack.ParamValues[0]
            : (int?)null;

        _logger.LogInformation(
            "ACI 握手 {Phase}: RequestMagicIndex={RequestMagicIndex}, AckMagicIndex={AckMagicIndex}, ErrorCode={ErrorCode}, Acknowledged={Acknowledged}, AckParamValue0={AckParamValue0}",
            phase,
            requestMagicIndex,
            ackMagicIndex,
            commandData.ErrorCode,
            commandData.Acknowledged,
            ackParamValue);
    }

    private async Task ObservePostHandshakeReadsAsync(CancellationToken cancellationToken)
    {
        const int observeCount = 5;

        for (var i = 1; i <= observeCount; i++)
        {
            await Task.Delay(HandshakeObserveInterval, cancellationToken);

            var observeResult = await SendGlobalParamReadAsync(0, 1, cancellationToken);
            LogGlobalParamCommandResult($"PostHandshakeRead[{i}]", observeResult);

            if (TryGetHandshakeStatus(observeResult, out var observeValue))
            {
                _logger.LogInformation("ACI 握手后连续回读第 {Index} 次，当前值={ParamValue}", i, observeValue);

                if (observeValue != 2)
                {
                    _logger.LogWarning("ACI 握手后连续回读发现参数回落为 {ParamValue}，准备自动补写 2", observeValue);

                    var recovered = await TryRecoverHandshakeStateAsync(cancellationToken);
                    if (!recovered)
                    {
                        _logger.LogWarning("ACI 握手后自动补写 2 失败，结束本轮观察");
                        return;
                    }
                }
            }
            else
            {
                _logger.LogWarning("ACI 握手后连续回读第 {Index} 次失败", i);
            }
        }
    }

    private async Task<bool> TryRecoverHandshakeStateAsync(CancellationToken cancellationToken)
    {
        const int maxRetryCount = 3;

        for (var retry = 1; retry <= maxRetryCount; retry++)
        {
            var writeResult = await SendGlobalParamWriteAsync(0, 1, new[] { 2 }, cancellationToken);
            LogGlobalParamCommandResult($"RecoverWrite[{retry}]", writeResult);
            if (writeResult.ErrorCode != AciCommandErrorCode.None)
            {
                _logger.LogWarning("ACI 自动补写 2 第 {Retry} 次失败，ErrorCode={ErrorCode}", retry, writeResult.ErrorCode);
                await Task.Delay(HandshakeObserveInterval, cancellationToken);
                continue;
            }

            await Task.Delay(HandshakeObserveInterval, cancellationToken);

            var verifyResult = await SendGlobalParamReadAsync(0, 1, cancellationToken);
            LogGlobalParamCommandResult($"RecoverRead[{retry}]", verifyResult);
            if (TryGetHandshakeStatus(verifyResult, out var verifyValue) && verifyValue == 2)
            {
                _logger.LogInformation("ACI 自动补写 2 成功，第 {Retry} 次回读确认值={ParamValue}", retry, verifyValue);
                return true;
            }

            _logger.LogWarning("ACI 自动补写 2 第 {Retry} 次后回读仍未确认到 2，当前值={ParamValue}", retry, verifyValue);
        }

        return false;
    }

    private Task<AciCommandData> SendGlobalParamReadAsync(int index, int number, CancellationToken cancellationToken)
    {
        return SendCommandAsync(
            callback => SendGlobalParamRead(callback, index, number),
            cancellationToken);
    }

    private Task<AciCommandData> SendGlobalParamWriteAsync(int index, int number, int[] values, CancellationToken cancellationToken)
    {
        return SendCommandAsync(
            callback => SendGlobalParamWrite(callback, index, number, values),
            cancellationToken);
    }

    private async Task<AciCommandData> SendCommandAsync(
        Func<AciCommandCallBack, AciCommandData> sendFunc,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<AciCommandData>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        sendFunc(data => tcs.TrySetResult(data));

        return await tcs.Task;
    }
    
    #endregion

    #region 公共管理与控制 API 方法

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


