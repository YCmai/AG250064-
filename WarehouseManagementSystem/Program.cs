using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Service.Io;
using WarehouseManagementSystem.Service.Plc;
using WarehouseManagementSystem.Services;
using WarehouseManagementSystem.Infrastructure.Ndc;
using WarehouseManagementSystem.Services.Ndc;
using WarehouseManagementSystem.Services.Rcs;
using WarehouseManagementSystem.Services.Integrations;
using WarehouseManagementSystem.Services.Integrations.Hosted;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Services.Tasks;

var builder = WebApplication.CreateBuilder(args);

var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "Logs");
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

var logPath = Path.Combine(logDirectory, "RCS-Pad-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Filter.ByExcluding(logEvent =>
        logEvent.MessageTemplate.Text.Contains("Request starting HTTP") ||
        logEvent.MessageTemplate.Text.Contains("Executing endpoint") ||
        logEvent.MessageTemplate.Text.Contains("Executed endpoint") ||
        logEvent.MessageTemplate.Text.Contains("Request finished") ||
        logEvent.MessageTemplate.Text.Contains("Route matched") ||
        logEvent.MessageTemplate.Text.Contains("Executing action") ||
        logEvent.MessageTemplate.Text.Contains("Executed action"))
    .Enrich.FromLogContext()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug)
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 31,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
// NDC/RCS 迁移期的数据访问适配层，当前统一走 IDatabaseService + Dapper。
builder.Services.AddScoped(typeof(IEntityRepository<>), typeof(DapperEntityRepository<>));
// NDC 对接服务。
builder.Services.AddSingleton<AciAppManager>();
builder.Services.AddScoped<IAciTaskDataService, AciTaskDataService>();
builder.Services.AddScoped<IAciLocationDataService, AciLocationDataService>();
builder.Services.AddScoped<IAciInteractionDataService, AciInteractionDataService>();

builder.Services.AddScoped<AciDataEventHandler>();
builder.Services.AddSingleton<IGroupMaxTaskCountCategory, DefaultGroupMaxTaskCountCategory>();
builder.Services.AddScoped<IRcsUserTaskService, RcsUserTaskService>();
builder.Services.AddScoped<IRcsNdcTaskService, RcsNdcTaskService>();
builder.Services.AddScoped<IRcsLocationService, RcsLocationService>();
builder.Services.AddScoped<IRcsInteractionService, RcsInteractionService>();

builder.Services.AddSingleton<IServiceToggleService, ServiceToggleService>();
builder.Services.AddSingleton<IAgvIntegrationService, AgvIntegrationService>();
builder.Services.AddScoped<IAgvOutboundQueueRepository, AgvOutboundQueueRepository>();
builder.Services.AddScoped<IAgvOutboundInteractionService, AgvOutboundInteractionService>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var key = System.Text.Encoding.ASCII.GetBytes(secretKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Bearer";
        options.DefaultChallengeScheme = "Bearer";
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddSingleton<IIOService, IOService>();
builder.Services.AddSingleton<IIODeviceService, IODeviceService>();
builder.Services.AddSingleton<IOAGVTaskProcessor>();
builder.Services.AddHostedService<IOProcessorService>();

builder.Services.AddSingleton<IPlcSignalService, PlcSignalService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PlcSignalUpdater>();
builder.Services.AddSingleton<IPlcCommunicationService, PlcCommunicationService>();
builder.Services.AddHostedService<PlcCommunicationHostedService>();
builder.Services.AddHostedService<PlcTaskProcessor>();
builder.Services.AddHostedService<HeartbeatService>();

builder.Services.AddHostedService<ApiTaskProcessorService>();
builder.Services.AddHostedService<AgvCommandInboxProcessorService>();
//接口反馈，安全信号，后台消费线程（每2秒扫一次）
builder.Services.AddHostedService<AgvOutboundProcessorService>();
// NDC/RCS 迁移后的后台服务。
builder.Services.AddHostedService<AciSendTaskHostedService>();
builder.Services.AddHostedService<RcsWmsTaskHostedService>();

builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

builder.Services.AddControllers();

var ipAddress = builder.Configuration.GetConnectionString("IPAddress") ?? "0.0.0.0";
var port = int.Parse(builder.Configuration.GetConnectionString("Port") ?? "5003");
builder.WebHost.UseUrls($"http://{ipAddress}:{port}");

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

if (!builder.Environment.IsDevelopment() && !OperatingSystem.IsWindows())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(5056);
        serverOptions.Limits.MaxConcurrentConnections = 100;
        serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
        serverOptions.Limits.MaxRequestBodySize = 30 * 1024 * 1024;
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.MaxRequestBufferSize = 1024 * 1024;
        serverOptions.Limits.MaxResponseBufferSize = 64 * 1024;
    });
}
else
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Limits.MaxConcurrentConnections = 100;
        serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
        serverOptions.Limits.MaxRequestBodySize = 30 * 1024 * 1024;
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.MaxRequestBufferSize = 1024 * 1024;
        serverOptions.Limits.MaxResponseBufferSize = 64 * 1024;
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            if (exception is not null)
            {
                Log.Error(exception, "Unhandled exception");
            }

            var response = ApiResponseHelper.Failure<object>("服务器内部错误");

            await context.Response.WriteAsJsonAsync(response);
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
    }
});

app.UseCors("AllowFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();



