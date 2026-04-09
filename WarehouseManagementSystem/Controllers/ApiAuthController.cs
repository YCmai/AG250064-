using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API认证控制器，提供REST API认证接口
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class ApiAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<ApiAuthController> _logger;

        public ApiAuthController(
            IAuthService authService,
            IJwtTokenService jwtTokenService,
            IUserManagementService userManagementService,
            ILogger<ApiAuthController> logger)
        {
            _authService = authService;
            _jwtTokenService = jwtTokenService;
            _userManagementService = userManagementService;
            _logger = logger;
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求</param>
        /// <returns>登录响应，包含JWT令牌</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation($"用户登录尝试: {request.Username}");

            // 验证请求
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("登录请求参数无效");
                return BadRequest(ApiResponseHelper.Failure<LoginResponse>("用户名和密码不能为空"));
            }

            // 验证用户
            var user = await _authService.ValidateUserAsync(request.Username, request.Password);
            if (user == null)
            {
                _logger.LogWarning($"用户登录失败: {request.Username} - 用户名或密码错误");
                return Unauthorized(ApiResponseHelper.Failure<LoginResponse>("用户名或密码错误"));
            }

            // 生成JWT令牌
            var token = _jwtTokenService.GenerateToken(user);

            _logger.LogInformation($"用户登录成功: {request.Username}");

            // 获取用户权限
            var permissions = await _userManagementService.GetUserPermissionsAsync(user.Id);
            var permissionCodes = permissions.Select(p => p.Code).ToList();

            var response = new LoginResponse
            {
                Token = token,
                User = new UserResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    IsActive = user.IsActive,
                    Permissions = permissionCodes
                }
            };

            return Ok(ApiResponseHelper.Success(response, "登录成功"));
        }

        /// <summary>
        /// 用户登出
        /// </summary>
        /// <returns>登出响应</returns>
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"用户登出: {userId}");

            return Ok(ApiResponseHelper.Success("登出成功"));
        }

        /// <summary>
        /// 获取当前用户信息
        /// </summary>
        /// <returns>用户信息</returns>
        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetProfile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("无法从令牌中获取用户ID");
                return Unauthorized(ApiResponseHelper.Failure<UserResponse>("无效的令牌"));
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"用户不存在: {userId}");
                return NotFound(ApiResponseHelper.Failure<UserResponse>("用户不存在"));
            }

            var permissions = await _userManagementService.GetUserPermissionsAsync(user.Id);
            var permissionCodes = permissions.Select(p => p.Code).ToList();

            var response = new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsActive = user.IsActive,
                Permissions = permissionCodes
            };

            return Ok(ApiResponseHelper.Success(response, "获取用户信息成功"));
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <returns>新的JWT令牌</returns>
        [HttpPost("refresh-token")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> RefreshToken()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("无法从令牌中获取用户ID");
                return Unauthorized(ApiResponseHelper.Failure<RefreshTokenResponse>("无效的令牌"));
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"用户不存在: {userId}");
                return NotFound(ApiResponseHelper.Failure<RefreshTokenResponse>("用户不存在"));
            }

            var newToken = _jwtTokenService.GenerateToken(user);
            _logger.LogInformation($"令牌已刷新: {userId}");

            var response = new RefreshTokenResponse
            {
                Token = newToken
            };

            return Ok(ApiResponseHelper.Success(response, "令牌刷新成功"));
        }
    }

    /// <summary>
    /// 登录请求模型
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 是否记住我
        /// </summary>
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// 登录响应模型
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// JWT令牌
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 用户信息
        /// </summary>
        public UserResponse User { get; set; }
    }

    /// <summary>
    /// 用户响应模型
    /// </summary>
    public class UserResponse
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 用户权限列表
        /// </summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }

    /// <summary>
    /// 刷新令牌响应模型
    /// </summary>
    public class RefreshTokenResponse
    {
        /// <summary>
        /// 新的JWT令牌
        /// </summary>
        public string Token { get; set; }
    }
}
