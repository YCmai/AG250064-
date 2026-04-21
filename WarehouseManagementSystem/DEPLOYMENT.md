# 仓库管理系统 (WMS) - 部署与使用文档

## 1. 系统架构
本系统采用前后端分离开发，后端集成部署的架构。
- **后端**: ASP.NET Core 6.0 (C#)
- **前端**: Vue 3 + TypeScript + Vite + Pinia + Ant Design Vue
- **数据库**: SQL Server
- **部署模式**: 前端编译为静态资源，由 ASP.NET Core 后端作为静态文件服务器进行托管 (SPA 模式)。

## 2. 环境要求
- Windows 10/11 或 Windows Server
- .NET 6.0 SDK 或 Runtime
- Node.js 18+ (用于前端构建)
- SQL Server 2012+

## 3. 开发指南

### 3.1 前端开发
前端代码位于 `frontend` 目录。

1. 进入前端目录:
   ```bash
   cd frontend
   ```
2. 安装依赖:
   ```bash
   npm install
   ```
3. 启动开发服务器 (热重载模式):
   ```bash
   npm run dev
   ```
   *注意：开发模式下，前端默认运行在 `http://localhost:5173`。后端 API 需要启动并运行在 `http://localhost:5003`。*

### 3.2 后端开发
1. 使用 Visual Studio 2022 打开 `WarehouseManagementSystem.sln`。
2. 确保 `appsettings.json` 中的数据库连接字符串正确。
3. 按 `F5` 启动调试。
   *注意：后端启动后会自动托管 `wwwroot` 中的静态文件。如果你修改了前端代码，需要重新运行前端构建命令才能在后端地址看到变化，或者使用前端开发服务器进行调试。*

## 4. 构建与发布

### 4.1 前端构建 (发布前必做)
在发布系统前，必须先编译前端代码。编译后的文件会自动输出到后端的 `wwwroot` 目录。

1. 打开终端，进入 `frontend` 目录。
2. 运行构建命令:
   ```bash
   npm run build
   ```
   *此命令会自动执行以下操作：*
   - 编译 Vue 代码
   - 清空后端的 `wwwroot` 目录
   - 将生成的 `index.html` 和静态资源 (`assets/`) 复制到 `wwwroot`

### 4.2 后端发布
1. 在 Visual Studio 中，右键点击项目 **WarehouseManagementSystem** -> **发布**。
2. 选择发布目标 (文件夹)，例如 `bin\Release\net6.0\publish`。
3. 点击 **发布**。

或者使用命令行:
```bash
dotnet publish -c Release -o ./publish
```

## 5. 部署运行

### 方式一：直接运行 (推荐用于测试)
1. 确保数据库已正确配置。
2. 进入发布输出目录。
3. 双击 `WarehouseManagementSystem.exe` 或运行:
   ```bash
   dotnet WarehouseManagementSystem.dll
   ```
4. 访问 `http://localhost:5003` (或配置的端口)。

### 方式二：IIS 部署 (生产环境)
1. 安装 **ASP.NET Core Hosting Bundle** (对应 .NET 6 版本)。
2. 在 IIS 中添加网站，物理路径指向发布目录。
3. 应用程序池配置：
   - .NET CLR 版本: **无托管代码**
   - 管道模式: **集成**
4. 确保 IIS 用户 (如 `IIS AppPool\YourAppPoolName`) 对发布目录有读取权限。

## 6. 关键配置说明

### 6.1 前端 API 地址
- 生产环境 (`.env.production`): `VITE_API_URL=/api`
  - 这意味着前端会向当前域名下的 `/api` 路径发送请求，无需硬编码后端 IP。
- 开发环境 (`.env`): 默认为 `http://localhost:5003/api`。

### 6.2 路由配置
后端 `Program.cs` 已配置 SPA 支持：
```csharp
// 所有未匹配的 API 请求都会返回 index.html，由前端路由处理
app.MapFallbackToFile("index.html");
```

### 6.3 数据库连接
在 `appsettings.json` 中配置：
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=WMS_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

## 7. 常见问题

**Q: 启动后页面空白或 404？**
A: 请检查 `wwwroot` 目录下是否有 `index.html`。如果没有，请先运行前端构建命令 `npm run build`。

**Q: 登录后刷新页面报错？**
A: 确保后端 `PermissionMiddleware` 没有拦截 SPA 的路由请求（如 `/dashboard`, `/settings` 等）。目前的配置已处理此问题，通过 `MapFallbackToFile` 转发给前端。

**Q: 修改了前端代码没生效？**
A: 前端是静态编译的。修改 `.vue` 或 `.ts` 文件后，必须再次运行 `npm run build` 覆盖 `wwwroot` 中的旧文件。
