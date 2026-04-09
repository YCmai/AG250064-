# 仓库管理系统前端

这是仓库管理系统的前端应用，使用Vue 3 + TypeScript + Vite构建。

## 项目结构

```
frontend/
├── src/
│   ├── views/           # 页面组件
│   ├── layouts/         # 布局组件
│   ├── services/        # API服务
│   ├── stores/          # 状态管理（Pinia）
│   ├── router/          # 路由配置
│   ├── App.vue          # 主应用组件
│   ├── main.ts          # 入口文件
│   └── style.css        # 全局样式
├── public/              # 静态资源
├── vite.config.ts       # Vite配置
├── tsconfig.json        # TypeScript配置
├── package.json         # 项目依赖
└── README.md            # 项目说明
```

## 快速开始

### 安装依赖

```bash
npm install
```

### 开发模式

```bash
npm run dev
```

应用将在 `http://localhost:5173` 启动

### 构建生产版本

```bash
npm run build
```

### 预览生产版本

```bash
npm run preview
```

## 可用的脚本

- `npm run dev` - 启动开发服务器
- `npm run build` - 构建生产版本
- `npm run preview` - 预览生产版本
- `npm run lint` - 运行ESLint检查并自动修复
- `npm run format` - 使用Prettier格式化代码
- `npm run test` - 运行单元测试
- `npm run test:ui` - 使用UI运行单元测试
- `npm run test:coverage` - 生成测试覆盖率报告

## 技术栈

- **Vue 3** - UI框架
- **TypeScript** - 类型检查
- **Vite** - 构建工具
- **Vue Router** - 路由管理
- **Pinia** - 状态管理
- **Axios** - HTTP客户端
- **Ant Design Vue** - UI组件库
- **Vitest** - 单元测试框架

## 环境变量

创建 `.env` 文件配置环境变量：

```
VITE_API_URL=http://localhost:5000/api
```

## API集成

所有API请求都通过 `src/services/api.ts` 进行，该文件配置了：

- 基础URL
- 请求/响应拦截器
- JWT令牌管理
- 错误处理

## 状态管理

使用Pinia进行状态管理，主要stores：

- `auth` - 认证状态（用户信息、令牌）
- `location` - 储位管理状态
- `task` - 任务管理状态

## 开发指南

### 添加新页面

1. 在 `src/views` 中创建新的页面组件
2. 在 `src/router/index.ts` 中添加路由
3. 在布局中添加导航菜单项

### 添加新API服务

1. 在 `src/services` 中创建新的服务文件
2. 使用 `api` 实例进行HTTP请求
3. 定义TypeScript接口

### 添加新状态

1. 在 `src/stores` 中创建新的store
2. 使用Pinia的 `defineStore` 定义状态和方法
3. 在组件中使用 `useXxxStore()` hook

## 浏览器支持

- Chrome（最新版本）
- Firefox（最新版本）
- Safari（最新版本）
- Edge（最新版本）

## 许可证

MIT
