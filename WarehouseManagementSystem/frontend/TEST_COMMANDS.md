# 测试命令参考

## 1. 单元测试

### 运行所有单元测试
```bash
npm run test
```

### 运行特定测试文件
```bash
npm run test -- src/__tests__/stores/auth.spec.ts
```

### 运行匹配模式的测试
```bash
npm run test -- --grep "Auth Store"
```

### 监听模式（自动重新运行）
```bash
npm run test -- --watch
```

### 生成测试覆盖率报告
```bash
npm run test:coverage
```

## 2. UI测试界面

### 启动Vitest UI
```bash
npm run test:ui
```

然后在浏览器中打开显示的URL（通常是 `http://localhost:51204`）

## 3. 代码检查

### 运行ESLint检查
```bash
npm run lint
```

### 自动修复ESLint问题
```bash
npm run lint
```

### 格式化代码
```bash
npm run format
```

## 4. 构建和预览

### 开发模式
```bash
npm run dev
```

### 生产构建
```bash
npm run build
```

### 预览生产构建
```bash
npm run preview
```

## 5. 测试场景

### 场景1：快速验证（1分钟）
```bash
# 1. 启动开发服务器
npm run dev

# 2. 打开浏览器访问 http://localhost:5173
# 3. 按照 QUICK_TEST.md 进行快速测试
```

### 场景2：完整功能测试（15分钟）
```bash
# 1. 启动开发服务器
npm run dev

# 2. 打开浏览器访问 http://localhost:5173
# 3. 按照 TESTING_GUIDE.md 进行完整测试
```

### 场景3：单元测试（5分钟）
```bash
# 1. 运行所有单元测试
npm run test

# 2. 查看测试结果
# 3. 生成覆盖率报告
npm run test:coverage
```

### 场景4：代码质量检查（2分钟）
```bash
# 1. 运行ESLint检查
npm run lint

# 2. 格式化代码
npm run format

# 3. 查看检查结果
```

## 6. 调试技巧

### 在浏览器中调试
1. 打开浏览器开发者工具（F12）
2. 进入"Sources"标签
3. 在代码中设置断点
4. 执行操作触发断点

### 在VS Code中调试
1. 创建 `.vscode/launch.json`：
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "chrome",
      "request": "launch",
      "name": "Launch Chrome",
      "url": "http://localhost:5173",
      "webRoot": "${workspaceFolder}/frontend",
      "sourceMaps": true
    }
  ]
}
```
2. 按F5启动调试

### 查看API请求
1. 打开浏览器开发者工具（F12）
2. 进入"Network"标签
3. 执行操作
4. 查看请求和响应

### 查看控制台日志
1. 打开浏览器开发者工具（F12）
2. 进入"Console"标签
3. 查看日志信息

## 7. 常用测试命令组合

### 完整测试流程
```bash
# 1. 安装依赖
npm install

# 2. 运行代码检查
npm run lint

# 3. 运行单元测试
npm run test

# 4. 生成覆盖率报告
npm run test:coverage

# 5. 构建项目
npm run build

# 6. 预览生产版本
npm run preview
```

### 快速开发流程
```bash
# 1. 启动开发服务器
npm run dev

# 2. 在另一个终端运行测试（监听模式）
npm run test -- --watch

# 3. 在第三个终端运行ESLint（监听模式）
npm run lint
```

## 8. 测试覆盖率目标

| 类型 | 目标 | 当前 |
|------|------|------|
| 语句覆盖率 | > 80% | - |
| 分支覆盖率 | > 75% | - |
| 函数覆盖率 | > 80% | - |
| 行覆盖率 | > 80% | - |

## 9. 持续集成（CI）

### GitHub Actions示例
```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-node@v2
        with:
          node-version: '18'
      - run: npm install
      - run: npm run lint
      - run: npm run test
      - run: npm run build
```

## 10. 故障排除

### 测试失败
1. 检查错误信息
2. 查看测试文件
3. 运行单个测试进行调试
4. 检查mock配置

### 覆盖率低
1. 识别未覆盖的代码
2. 编写相应的测试
3. 运行覆盖率报告验证

### 性能问题
1. 检查测试执行时间
2. 优化mock配置
3. 并行运行测试
