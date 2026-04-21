# WarehouseManagementSystem 架构分层说明

本文档用于统一目录分类，降低“文件越写越散”的维护成本。

## 顶层目录职责

- `Controllers/`：接口入口层，仅处理参数校验、鉴权、返回格式，不放复杂业务逻辑。
- `Services/`：业务编排层，按领域拆分（`Ndc/`、`Plc/`、`Io/`、`Rcs/`、`Tasks/`、`Materials/`、`Locations/`、`User/`）。
- `Models/`：数据模型层（实体、DTO、ViewModel、枚举等）。
- `Infrastructure/`：技术实现层（协议、仓储、中间件、连接、跨领域基础能力）。
- `Db/`：数据库访问基础设施（当前统一入口：`IDatabaseService`）。
- `Views/`、`wwwroot/`、`frontend/`：前端页面与静态资源。

## 本次目录收拢

为减少顶层分散目录，已完成以下归类：

- `Protocols/Ndc` -> `Infrastructure/Protocols/Ndc`
- `Middleware/` -> `Infrastructure/Web/Middleware/`
- `Services` 按模块细分子层：
  - `Services/Ndc/Core`、`Services/Ndc/Hosted`
  - `Services/Plc/Core`、`Services/Plc/Hosted`
  - `Services/Io/Core`、`Services/Io/Hosted`
- `Models` 按模块细分：
  - `Models/Ndc/Enums`（ACI/NDC 相关枚举与工具）
  - `Models/PLC/Enums`（PLC/Heartbeat 相关枚举）
  - `Models/IO`、`Models/Ndc`、`Models/PLC` 下放置对应模块模型

说明：命名空间保持不变，因此不会影响现有引用和运行行为。

## 新增代码放置规则

- NDC 协议编解码、连接处理：放 `Infrastructure/Protocols/Ndc/`
- 通用仓储与数据访问适配：放 `Infrastructure/Ndc/` 或 `Db/`
- 请求管道拦截（日志、异常、权限、过期控制）：放 `Infrastructure/Web/Middleware/`
- 业务处理逻辑（任务流转、设备控制、规则判断）：放 `Services/<领域>/`
- 纯数据结构：放 `Models/<分类>/`

## 后续可选优化（建议分阶段）

1. `Services` 下按“模块+子层”再细分，例如 `Services/Ndc/Application`、`Services/Ndc/Domain`。
2. 将历史命名不一致文件逐步重命名（不建议一次性大改）。
3. 对 `DapperEntityRepository` 的内存过滤查询做下推 SQL 优化，避免全表拉取。
