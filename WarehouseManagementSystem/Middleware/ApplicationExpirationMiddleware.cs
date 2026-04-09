using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WarehouseManagementSystem.Middleware
{
    /// <summary>
    /// 应用程序时间限制中间件
    /// 检查当前时间是否超过配置的截止时间
    /// </summary>
    public class ApplicationExpirationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApplicationExpirationMiddleware> _logger;
        private readonly bool _enabled;
        private readonly DateTime _expirationDate;
        private readonly string _message;

        public ApplicationExpirationMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ApplicationExpirationMiddleware> logger)
        {
            _next = next;
            _logger = logger;

            // 从配置文件读取设置
            _enabled = configuration.GetValue<bool>("ApplicationExpiration:Enabled", false);
            
            var expirationDateStr = configuration.GetValue<string>("ApplicationExpiration:ExpirationDate");
            if (!string.IsNullOrEmpty(expirationDateStr) && DateTime.TryParse(expirationDateStr, out var expDate))
            {
                _expirationDate = expDate;
            }
            else
            {
                _expirationDate = DateTime.MaxValue;
            }

            _message = configuration.GetValue<string>("ApplicationExpiration:Message") 
                ?? "应用程序已过期，请联系管理员。";

            if (_enabled)
            {
                _logger.LogInformation($"应用程序时间限制已启用，截止时间: {_expirationDate:yyyy-MM-dd HH:mm:ss}");
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 如果未启用或未过期，继续处理请求
            if (!_enabled || DateTime.Now <= _expirationDate)
            {
                await _next(context);
                return;
            }

            // 已过期，返回错误信息
            _logger.LogWarning($"应用程序已过期。当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}, 截止时间: {_expirationDate:yyyy-MM-dd HH:mm:ss}");

            // 对于API请求返回JSON
            if (context.Request.Path.StartsWithSegments("/api") || 
                context.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync($"{{\"error\": \"{_message}\", \"expirationDate\": \"{_expirationDate:yyyy-MM-dd HH:mm:ss}\"}}");
            }
            else
            {
                // 对于页面请求返回HTML
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>应用程序已过期</title>
    <style>
        body {{
            font-family: 'Microsoft YaHei', Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 500px;
        }}
        h1 {{
            color: #e74c3c;
            margin-bottom: 20px;
        }}
        p {{
            color: #555;
            font-size: 16px;
            line-height: 1.6;
        }}
        .date {{
            color: #e74c3c;
            font-weight: bold;
            font-size: 18px;
            margin: 20px 0;
        }}
        .icon {{
            font-size: 60px;
            margin-bottom: 20px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>⏰</div>
        <h1>应用程序已过期</h1>
        <p>{_message}</p>
        <div class='date'>截止时间: {_expirationDate:yyyy-MM-dd HH:mm:ss}</div>
        <p style='color: #999; font-size: 14px;'>当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
</body>
</html>");
            }
        }
    }
}
