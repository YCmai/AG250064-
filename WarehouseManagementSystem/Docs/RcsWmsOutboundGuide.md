# WMS 交互简明说明

## 目标
这一套封装的目标很简单：

1. 业务层不自己拼 JSON
2. 业务层不自己写 HTTP 调用
3. 业务层不自己回写请求日志
4. 安全信号自动按配置重试

你在业务代码里只需要注入一个接口：

```csharp
IRcsWmsService
```

如果你只是想联调或手工触发，不想在业务代码里直接写调用，现在也可以直接走统一控制器：

- [ApiWmsController.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Controllers/ApiWmsController.cs)

## 服务入口
服务注册入口在这里：

- [Program.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Program.cs)

注册代码：

```csharp
builder.Services.Configure<RcsWmsOptions>(builder.Configuration.GetSection(RcsWmsOptions.SectionName));
builder.Services.AddHttpClient<IRcsWmsService, RcsWmsService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<RcsWmsOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
```

实际实现类在这里：

- [RcsWmsService.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Services/Rcs/RcsWmsService.cs)

控制器入口在这里：

- [ApiWmsController.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Controllers/ApiWmsController.cs)

## 当前执行模式
这一点现在明确一下，避免理解偏差。

当前不是“所有数据插库后都由后台服务统一自动发送”。

现在的模式是：

1. 物料到达生产线
   你可以只插库，也可以插库后立即发送。
2. 作业完成反馈
   你可以只插库，也可以插库后立即发送。
3. 安全信号
   首次发送仍然需要你主动调用。
   只有在首次发送后返回未安全或失败时，后台服务才会继续自动重试。

也就是说：

- `InsertXXXAsync`：只插库，不自动发
- `InsertAndSendXXXAsync`：插库后立即发
- 后台自动续跑：目前只针对安全信号重试

如果你后面想改成“3 类业务全部插库后由后台统一扫描发送”，也可以做，但当前这版还不是这个模式。

## 统一控制器怎么用
统一控制器路由前缀：

```text
/api/wms
```

示例查看接口：

```text
GET /api/wms/examples
```

这个接口会直接返回：

1. 当前执行模式说明
2. 三类业务的请求示例
3. 对应控制器 URL

常用接口如下。

### 物料到达生产线

```text
POST /api/wms/material-arrival/insert
POST /api/wms/material-arrival/insert-and-send
POST /api/wms/material-arrival/{id}/send
```

### 安全信号

```text
POST /api/wms/safety-signal/insert
POST /api/wms/safety-signal/insert-and-send
POST /api/wms/safety-signal/{id}/send
```

### 作业完成反馈

```text
POST /api/wms/job-feedback/insert
POST /api/wms/job-feedback/insert-and-send
POST /api/wms/job-feedback/{id}/send
```

## 你只需要记住的几个方法
接口定义在这里：

- [RcsWmsService.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Services/Rcs/RcsWmsService.cs)

常用方法分三类。

### 1. 只插表，不发送

```csharp
Task<int> InsertMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request);
Task<int> InsertSafetySignalAsync(RcsWmsSafetySignalCreateRequest request);
Task<int> InsertJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request);
```

适用场景：
先落库，后面什么时候发由你自己控制。

### 2. 按业务表 ID 发送

```csharp
Task<RcsWmsDispatchResult> SendMaterialArrivalAsync(int materialArrivalId, CancellationToken cancellationToken = default);
Task<RcsWmsDispatchResult> SendSafetySignalAsync(int safetySignalId, CancellationToken cancellationToken = default);
Task<RcsWmsDispatchResult> SendJobFeedbackAsync(int jobFeedbackId, CancellationToken cancellationToken = default);
```

适用场景：
你已经有业务记录 ID，只想触发发送。

### 3. 插入后立即发送

```csharp
Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request, CancellationToken cancellationToken = default);
Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendSafetySignalAsync(RcsWmsSafetySignalCreateRequest request, CancellationToken cancellationToken = default);
Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request, CancellationToken cancellationToken = default);
```

适用场景：
这是最推荐的调用方式，业务层通常直接用这一组就够了。

## 接口调用示例
这里保留“业务层注入服务”的写法。

### 1. 物料到达生产线

```csharp
public class DemoService
{
    private readonly IRcsWmsService _rcsWmsService;

    public DemoService(IRcsWmsService rcsWmsService)
    {
        _rcsWmsService = rcsWmsService;
    }

    public async Task SendMaterialArrivalAsync()
    {
        var request = new RcsWmsMaterialArrivalCreateRequest
        {
            OrderNumber = "20260402000001",
            PalletNumber = "PLT000000000000001",
            Barcodes = new List<string>
            {
                "123456789012",
                "123456789013"
            }
        };

        var (id, result) = await _rcsWmsService.InsertAndSendMaterialArrivalAsync(request);

        if (!result.Success)
        {
            throw new Exception($"发送 WMS 失败，ID={id}，原因：{result.ErrorMsg}");
        }
    }
}
```

### 2. 安全信号

```csharp
var request = new RcsWmsSafetySignalCreateRequest
{
    TaskNumber = "AGV_TASK_202604020001",
    RequestDate = DateTime.Now,
    Room = "WEIGH01"
};

var (id, result) = await _rcsWmsService.InsertAndSendSafetySignalAsync(request);

if (!result.Success)
{
    // 对安全信号来说，不一定表示彻底失败
    // 如果 WMS 返回 safeFlag != Y，系统会写入下次重试时间，由后台继续重试
    Console.WriteLine($"首发未完成，ID={id}，错误：{result.ErrorMsg}，下次重试：{result.NextRetryTime}");
}
```

### 3. 作业完成反馈

```csharp
var request = new RcsWmsJobFeedbackCreateRequest
{
    TaskNumber = "AGV_TASK_202604020001",
    Status = "1"
};

var (id, result) = await _rcsWmsService.InsertAndSendJobFeedbackAsync(request);

if (!result.Success)
{
    Console.WriteLine($"作业完成反馈发送失败，ID={id}，原因：{result.ErrorMsg}");
}
```

### 4. 已有 ID 时直接发送

```csharp
var result = await _rcsWmsService.SendJobFeedbackAsync(jobFeedbackId);
```

## 安全信号为什么会自动重试
后台服务在这里：

- [RcsWmsSafetySignalRetryHostedService.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Services/Rcs/RcsWmsSafetySignalRetryHostedService.cs)

它内部会调用：

```csharp
await _rcsWmsService.ProcessDueSafetySignalsAsync(batchSize, cancellationToken);
```

处理规则：

1. 安全信号先落库
2. 首次调用 WMS
3. 如果返回 `flag != 0`，进入待重试
4. 如果返回 `flag = 0` 但 `safeFlag != Y`，也进入待重试
5. 到了 `NextRetryTime` 后，后台服务再次调用
6. 直到返回 `safeFlag = Y` 才算真正成功

注意：
这里只是“首次调用之后的自动重试”。
不是“插库后完全不调服务、后台自己首次发送”。

## 请求 JSON 结构
### 物料到达生产线

```json
{
  "orderNumber": "20260402000001",
  "palletNumber": "PLT000000000000001",
  "items": [
    {
      "barcode": "123456789012"
    },
    {
      "barcode": "123456789013"
    }
  ]
}
```

### 安全信号

```json
{
  "taskNumber": "AGV_TASK_202604020001",
  "requestDate": "20260402143000",
  "room": "WEIGH01"
}
```

### 作业完成反馈

```json
{
  "taskNumber": "AGV_TASK_202604020001",
  "status": "1"
}
```

## 相关表
建表 SQL 在这里：

- [CreateRcsWmsOutboundTables.sql](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Migrations/CreateRcsWmsOutboundTables.sql)

表包括：

1. `RCS_WmsRequestLog`
2. `RCS_WmsMaterialArrival`
3. `RCS_WmsMaterialArrivalItems`
4. `RCS_WmsSafetySignal`
5. `RCS_WmsJobFeedback`

## 配置位置
配置类：

- [RcsWmsOptions.cs](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/Services/Rcs/RcsWmsOptions.cs)

配置文件：

- [appsettings.json](/e:/NDCProject/AG250064无锡达能项目/WarehouseManagementSystem/appsettings.json)

配置节点：

```json
"RcsWmsOutbound": {
  "Enabled": true,
  "BaseUrl": "http://10.200.178.88:5000",
  "MaterialArrivalEndpoint": "/api/wms/material-arrival",
  "SafetySignalEndpoint": "/api/wms/safety-signal",
  "JobFeedbackEndpoint": "/api/wms/job-feedback",
  "TimeoutSeconds": 30,
  "SafetyRetryIntervalSeconds": 30,
  "SafetyBatchSize": 20
}
```

## 最后怎么用
如果你不想想太多，业务层就按这个模式写：

```csharp
private readonly IRcsWmsService _rcsWmsService;
```

然后按业务场景调用：

1. 物料到达生产线：`InsertAndSendMaterialArrivalAsync`
2. 安全信号：`InsertAndSendSafetySignalAsync`
3. 作业完成反馈：`InsertAndSendJobFeedbackAsync`

这样就够了。

如果你是联调人员，不想进业务层代码，直接调用控制器就行：

1. 先看 `GET /api/wms/examples`
2. 再调用对应的 `insert-and-send`
