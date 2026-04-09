# 前端项目设置指南

## 项目概述

这是一个使用Vue 3 + TypeScript + Vite构建的仓库管理系统前端应用。

## 项目已完成的功能

### Phase 2: 前端框架搭建 ✅

#### 2.1 项目基础设施
- ✅ Vite构建工具配置
- ✅ TypeScript编译选项配置
- ✅ ESLint和Prettier配置
- ✅ 环境变量管理（.env和.env.production）

#### 2.2 API客户端
- ✅ Axios实例创建和配置
- ✅ 请求拦截器（自动添加JWT令牌）
- ✅ 响应拦截器（错误处理和令牌过期处理）
- ✅ API服务类：
  - `authService` - 认证相关API
  - `locationService` - 储位管理API
  - `taskService` - 任务管理API

#### 2.4 状态管理（Pinia）
- ✅ AuthStore - 用户认证状态
- ✅ LocationStore - 储位列表状态
- ✅ TaskStore - 任务列表状态

#### 2.8 公共组件库
- ✅ MainLayout - 主布局组件（包含导航栏和侧边栏）
- ✅ 使用Ant Design Vue组件库

#### 2.10 全局样式
- ✅ 全局CSS样式
- ✅ 响应式布局支持

#### 2.11 错误处理和提示
- ✅ API错误处理
- ✅ 使用Ant Design Vue的message和Modal组件

### Phase 3: 认证功能迁移 ✅

#### 3.1 登录页面
- ✅ LoginView组件
- ✅ 用户名和密码输入
- ✅ "记住我"复选框
- ✅ 登录按钮和表单验证
- ✅ 登录成功/失败处理

#### 3.2 登出功能
- ✅ MainLayout中的登出按钮
- ✅ 登出逻辑实现

#### 3.3 路由守卫
- ✅ 认证守卫实现
- ✅ 未登录用户重定向到登录页
- ✅ 已登录用户可访问受保护页面

#### 3.4 JWT令牌管理
- ✅ 令牌保存到localStorage
- ✅ API请求自动添加令牌
- ✅ 令牌过期处理

### Phase 4: 储位管理功能迁移 ✅

#### 4.1 储位列表页面
- ✅ LocationsView组件
- ✅ 储位列表显示（表格布局）
- ✅ 搜索功能
- ✅ 分页功能
- ✅ 清空物料操作

### Phase 5: 任务管理功能迁移 ✅

#### 5.1 任务列表页面
- ✅ TasksView组件
- ✅ 任务列表显示
- ✅ 日期范围过滤
- ✅ 分页功能
- ✅ 任务取消操作

## 项目结构

```
frontend/
├── src/
│   ├── views/              # 页面组件
│   │   ├── LoginView.vue   # 登录页面
│   │   ├── DashboardView.vue # 仪表板
│   │   ├── LocationsView.vue # 储位管理
│   │   └── TasksView.vue   # 任务管理
│   ├── layouts/            # 布局组件
│   │   └── MainLayout.vue  # 主布局
│   ├── services/           # API服务
│   │   ├── api.ts          # Axios配置
│   │   ├── auth.ts         # 认证服务
│   │   ├── location.ts     # 储位服务
│   │   └── task.ts         # 任务服务
│   ├── stores/             # Pinia状态管理
│   │   ├── auth.ts         # 认证状态
│   │   ├── location.ts     # 储位状态
│   │   └── task.ts         # 任务状态
│   ├── router/             # 路由配置
│   │   └── index.ts        # 路由定义和守卫
│   ├── App.vue             # 主应用组件
│   ├── main.ts             # 入口文件
│   └── style.css           # 全局样式
├── index.html              # HTML入口
├── vite.config.ts          # Vite配置
├── tsconfig.json           # TypeScript配置
├── package.json            # 项目依赖
└── README.md               # 项目说明
```

## 快速开始

### 1. 安装依赖

```bash
cd frontend
npm install
```

### 2. 启动开发服务器

```bash
npm run dev
```

应用将在 `http://localhost:5173` 启动

### 3. 构建生产版本

```bash
npm run build
```

## 环境配置

### 开发环境 (.env)
```
VITE_API_URL=http://localhost:5000/api
```

### 生产环境 (.env.production)
```
VITE_API_URL=/api
```

## 后续开发任务

### 需要完成的任务

1. **单元测试** - 为各个组件和服务编写单元测试
2. **储位详情弹框** - 创建LocationDetailModal组件
3. **批量操作** - 实现批量清空物料和批量锁定/解锁
4. **储位创建/编辑** - 创建LocationCreateEditView组件
5. **任务创建** - 创建TaskCreateView组件
6. **任务详情** - 创建TaskDetailView组件
7. **其他功能模块** - 迁移PLC信号、IO监控等功能

## 技术栈

- **Vue 3** - 渐进式JavaScript框架
- **TypeScript** - 类型安全的JavaScript
- **Vite** - 下一代前端构建工具
- **Vue Router** - 官方路由管理库
- **Pinia** - 官方状态管理库
- **Axios** - HTTP客户端
- **Ant Design Vue** - 企业级UI组件库
- **Vitest** - 单元测试框架

## 常见命令

```bash
# 开发
npm run dev

# 构建
npm run build

# 预览生产版本
npm run preview

# 代码检查和修复
npm run lint

# 代码格式化
npm run format

# 运行单元测试
npm run test

# 运行单元测试（UI模式）
npm run test:ui

# 生成测试覆盖率报告
npm run test:coverage
```

## 注意事项

1. 确保后端API服务运行在 `http://localhost:5000`
2. 开发时使用 `npm run dev` 启动开发服务器
3. 生产部署时需要配置正确的API地址
4. 所有API请求都会自动添加JWT令牌
5. 令牌过期时会自动重定向到登录页

## 支持的浏览器

- Chrome（最新版本）
- Firefox（最新版本）
- Safari（最新版本）
- Edge（最新版本）

## 许可证

MIT
