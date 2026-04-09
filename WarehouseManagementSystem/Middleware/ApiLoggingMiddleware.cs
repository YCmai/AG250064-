using System.Diagnostics;
using System.Text;

namespace WarehouseManagementSystem.Middleware
{
    /// <summary>
    /// API日志记录中间件
    /// </summary>
    public class ApiLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiLoggingMiddleware> _logger;

        public ApiLoggingMiddleware(RequestDelegate next, ILogger<ApiLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 只记录API请求
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;

            // 记录请求信息
            var request = context.Request;
            var requestBody = await ReadRequestBodyAsync(request);

            _logger.LogInformation(
                "API请求开始 [RequestId: {RequestId}] {Method} {Path} {QueryString}",
                requestId,
                request.Method,
                request.Path,
                request.QueryString);

            if (!string.IsNullOrEmpty(requestBody))
            {
                _logger.LogDebug(
                    "API请求体 [RequestId: {RequestId}] {Body}",
                    requestId,
                    requestBody);
            }

            // 保存原始响应流
            var originalBodyStream = context.Response.Body;

            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);

                    stopwatch.Stop();

                    // 读取响应体
                    var response = context.Response;
                    var responseContent = await ReadResponseBodyAsync(response);

                    // 记录响应信息
                    _logger.LogInformation(
                        "API请求完成 [RequestId: {RequestId}] {Method} {Path} {StatusCode} {ElapsedMilliseconds}ms",
                        requestId,
                        request.Method,
                        request.Path,
                        response.StatusCode,
                        stopwatch.ElapsedMilliseconds);

                    if (!string.IsNullOrEmpty(responseContent) && response.StatusCode >= 400)
                    {
                        _logger.LogWarning(
                            "API请求错误 [RequestId: {RequestId}] {StatusCode} {Body}",
                            requestId,
                            response.StatusCode,
                            responseContent);
                    }

                    // 将响应体写回原始流
                    await responseBody.CopyToAsync(originalBodyStream);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _logger.LogError(
                        ex,
                        "API请求异常 [RequestId: {RequestId}] {Method} {Path} {ElapsedMilliseconds}ms",
                        requestId,
                        request.Method,
                        request.Path,
                        stopwatch.ElapsedMilliseconds);

                    throw;
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }
            }
        }

        private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            request.EnableBuffering();

            var body = request.Body;
            var buffer = new byte[Convert.ToInt32(request.ContentLength ?? 0)];

            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            var bodyAsText = Encoding.UTF8.GetString(buffer);

            request.Body.Position = 0;

            return bodyAsText;
        }

        private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            return text;
        }
    }

    /// <summary>
    /// API日志记录中间件扩展
    /// </summary>
    public static class ApiLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiLoggingMiddleware>();
        }
    }
}
