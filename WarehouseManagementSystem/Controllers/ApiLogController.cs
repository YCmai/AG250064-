using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/log")]
    /// <summary>
    /// 系统日志管理接口。
    /// </summary>
    public class ApiLogController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ApiLogController> _logger;

        public ApiLogController(IWebHostEnvironment env, ILogger<ApiLogController> logger)
        {
            _env = env;
            _logger = logger;
        }

        [HttpGet("files")]
        /// <summary>
        /// 获取 Logs 目录下的所有日志文件，按修改时间降序排序（最新在最前）。
        /// </summary>
        public IActionResult GetLogFiles()
        {
            try
            {
                var logDirectory = Path.Combine(_env.ContentRootPath, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    return Ok(new { success = true, data = Array.Empty<object>() });
                }

                var files = Directory.GetFiles(logDirectory, "*.log")
                    .Select(filePath => new FileInfo(filePath))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => new
                    {
                        filename = f.Name,
                        size = f.Length,
                        lastModified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    })
                    .ToList();

                return Ok(new { success = true, data = files });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取日志文件列表失败");
                return StatusCode(500, new { success = false, message = "获取日志文件列表失败: " + ex.Message });
            }
        }

        [HttpGet("content")]
        /// <summary>
        /// 读取指定日志文件的内容。支持 limitLines 来限制行数以提升性能。
        /// </summary>
        public IActionResult GetLogContent([FromQuery] string filename, [FromQuery] int? limitLines)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filename))
                {
                    return BadRequest(new { success = false, message = "文件名不能为空" });
                }

                // 安全校验，防止目录遍历攻击 (Directory Traversal)
                if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
                {
                    return BadRequest(new { success = false, message = "无效的文件名，禁止越权访问路径" });
                }

                var logDirectory = Path.Combine(_env.ContentRootPath, "Logs");
                var filePath = Path.Combine(logDirectory, filename);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { success = false, message = "指定的日志文件不存在" });
                }

                var lines = new List<string>();

                // 核心安全且可并发读取设计：使用 FileShare.ReadWrite，防止与 Serilog 日志写入发生文件独占冲突。
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }

                // 若设置了 limitLines，仅返回最新的最后 N 行数据
                if (limitLines.HasValue && limitLines.Value > 0 && lines.Count > limitLines.Value)
                {
                    lines = lines.Skip(lines.Count - limitLines.Value).ToList();
                }

                return Ok(new { success = true, data = lines });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取日志文件 {Filename} 失败", filename);
                return StatusCode(500, new { success = false, message = $"读取日志文件失败: {ex.Message}" });
            }
        }
    }
}
