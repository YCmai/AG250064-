using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using WarehouseManagementSystem.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WarehouseManagementSystem.Middleware
{
    public class PermissionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PermissionMiddleware> _logger;

        public PermissionMiddleware(RequestDelegate next, ILogger<PermissionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();
            
            _logger.LogInformation($"PermissionMiddleware: 请求路径 = {path}");
            
            // API 路由使用 JWT 认证，完全跳过 Session 权限检查
            if (path != null && (path.StartsWith("/api/") || path == "/api"))
            {
                _logger.LogInformation($"PermissionMiddleware: API 路由，直接跳过所有权限检查 - {path}");
                await _next(context);
                return;
            }
            
            // 跳过不需要权限检查的路径
            if (ShouldSkipAuth(path))
            {
                _logger.LogInformation($"PermissionMiddleware: 跳过权限检查 - {path}");
                await _next(context);
                return;
            }

            var userId = context.Session.GetInt32("UserId");
            if (userId == null)
            {
                // 添加小延迟确保Session完全写入
                await Task.Delay(50);
                userId = context.Session.GetInt32("UserId");
                
                if (userId == null)
                {
                    // 未登录，重定向到登录页
                    _logger.LogInformation($"PermissionMiddleware: 用户未登录，重定向到登录页");
                    context.Response.Redirect("/Auth/Login?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                    return;
                }
            }

            // 从服务容器中获取AuthService
            var authService = context.RequestServices.GetRequiredService<IAuthService>();
            
            // 检查用户是否存在
            var user = await authService.GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                _logger.LogInformation($"PermissionMiddleware: 用户不存在，清除 Session");
                context.Session.Clear();
                context.Response.Redirect("/Auth/Login");
                return;
            }

            // 管理员拥有所有权限
            if (user.IsAdmin)
            {
                _logger.LogInformation($"PermissionMiddleware: 用户是管理员，允许访问");
                await _next(context);
                return;
            }

            // 检查权限
            var hasPermission = await CheckPermission(context, userId.Value, authService);
            if (!hasPermission)
            {
                _logger.LogInformation($"PermissionMiddleware: 用户没有权限，重定向到拒绝页面");
                context.Response.Redirect("/Home/AccessDenied");
                return;
            }

            _logger.LogInformation($"PermissionMiddleware: 权限检查通过，允许访问");
            await _next(context);
        }

        private bool ShouldSkipAuth(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _logger.LogInformation($"PermissionMiddleware.ShouldSkipAuth: path 为空");
                return true;
            }

            // API 路由使用 JWT 认证，不需要 Session 检查
            if (path.StartsWith("/api/"))
            {
                _logger.LogInformation($"PermissionMiddleware.ShouldSkipAuth: path 以 /api/ 开头，跳过权限检查");
                return true;
            }

            // SPA 前端路由支持 - 允许访问根路径、index.html 和静态资源
            if (path.Equals("/", StringComparison.OrdinalIgnoreCase) || 
                path.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/assets/"))
            {
                _logger.LogInformation($"PermissionMiddleware.ShouldSkipAuth: SPA 路径，跳过权限检查");
                return true;
            }

            var skipPaths = new[]
            {
                "/auth/login",
                "/auth/logout",
                "/auth/ajaxlogin",
                "/auth/passwordgenerator",
                "/home/accessdenied",
                "/testlocalization",
                "/language/setlanguage",
                "/css/",
                "/js/",
                "/lib/",
                "/images/",
                "/favicon.ico",
                "/api/connectionstatus"
            };

            var shouldSkip = skipPaths.Any(skipPath => path.StartsWith(skipPath));
            _logger.LogInformation($"PermissionMiddleware.ShouldSkipAuth: path={path}, shouldSkip={shouldSkip}");
            return shouldSkip;
        }

        private async Task<bool> CheckPermission(HttpContext context, int userId, IAuthService authService)
        {
            var controller = context.Request.RouteValues["controller"]?.ToString();
            var action = context.Request.RouteValues["action"]?.ToString();

            if (string.IsNullOrEmpty(controller))
                return true;

            // 根据控制器和动作确定需要的权限
            var permissionCode = GetPermissionCode(controller, action);
            if (string.IsNullOrEmpty(permissionCode))
                return true;

            return await authService.HasPermissionAsync(userId, permissionCode);
        }

        private string GetPermissionCode(string controller, string? action)
        {
            return controller.ToLower() switch
            {
                "displaylocation" => "DISPLAY_LOCATION",
                "location" => "LOCATION_MANAGE",
                "tasks" => "TASK_MANAGE",
                "plcsignalstatus" => "PLC_SIGNAL_STATUS",
                "autoplctask" => "PLC_TASK_INTERACTION",
                "plcsignal" => "PLC_SIGNAL_MANAGE",
                "iomonitor" => "IO_SIGNAL_MANAGE",
                "apitask" => "API_TASK_MANAGE",
                "logs" => "SYSTEM_LOG",
                "usermanagement" => "USER_MANAGEMENT",
                _ => ""
            };
        }
    }
}
