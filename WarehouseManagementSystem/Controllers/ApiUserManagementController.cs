using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/usermanagement")]
    /// <summary>
    /// 用户与权限管理接口：用户维护、权限分配、密码重置、启停用。
    /// </summary>
    public class ApiUserManagementController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;
        private readonly IAuthService _authService;
        private readonly ILogger<ApiUserManagementController> _logger;

        public ApiUserManagementController(
            IUserManagementService userManagementService,
            IAuthService authService,
            ILogger<ApiUserManagementController> logger)
        {
            _userManagementService = userManagementService;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _userManagementService.GetAllUsersAsync();
                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("user/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "用户不存在" });
                return Ok(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户信息失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class UserCreateRequest
        {
            /// <summary>登录用户名。</summary>
            public string Username { get; set; }
            /// <summary>明文密码（入参），服务端会进行哈希存储。</summary>
            public string Password { get; set; }
            /// <summary>邮箱。</summary>
            public string Email { get; set; }
            /// <summary>显示名称。</summary>
            public string DisplayName { get; set; }
            /// <summary>是否管理员。</summary>
            public bool IsAdmin { get; set; }
        }

        public class UserUpdateRequest
        {
            /// <summary>邮箱。</summary>
            public string Email { get; set; }
            /// <summary>显示名称。</summary>
            public string DisplayName { get; set; }
            /// <summary>是否管理员。</summary>
            public bool IsAdmin { get; set; }
            /// <summary>是否启用。</summary>
            public bool IsActive { get; set; }
        }

        public class AssignPermissionsRequest
        {
            /// <summary>目标权限 ID 集合。</summary>
            public int[] PermissionIds { get; set; }
        }

        public class ResetPasswordRequest
        {
            /// <summary>新密码（入参）。</summary>
            public string NewPassword { get; set; }
        }

        public class ToggleStatusRequest
        {
            /// <summary>目标启用状态。</summary>
            public bool IsActive { get; set; }
        }

        [HttpPost("user")]
        /// <summary>
        /// 创建用户（密码会在服务端哈希后保存）。
        /// </summary>
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                    return BadRequest(new { success = false, message = "用户名和密码不能为空" });

                var user = new User
                {
                    Username = request.Username,
                    Password = _authService.HashPassword(request.Password),
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
                    DisplayName = request.DisplayName,
                    IsAdmin = request.IsAdmin,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var success = await _userManagementService.CreateUserAsync(user);
                if (success)
                    return Ok(new { success = true, data = user });
                return BadRequest(new { success = false, message = "创建用户失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建用户失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("user/{id}")]
        /// <summary>
        /// 更新用户基本信息（邮箱、显示名、管理员标记）。
        /// </summary>
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateRequest request)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "用户不存在" });

                user.Email = request.Email ?? user.Email;
                user.DisplayName = request.DisplayName ?? user.DisplayName;
                user.IsAdmin = request.IsAdmin;
                // 当前接口仅更新基础信息；启停用请走 toggle-status 接口。
                var success = await _userManagementService.UpdateUserAsync(user);
                if (success)
                    return Ok(new { success = true });
                return BadRequest(new { success = false, message = "更新用户失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新用户失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var success = await _userManagementService.DeleteUserAsync(id);
                if (success)
                    return Ok(new { success = true });
                return BadRequest(new { success = false, message = "删除用户失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除用户失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _userManagementService.GetAllPermissionsAsync();
                return Ok(new { success = true, data = permissions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取权限列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("user/{userId}/permissions")]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var permissions = await _userManagementService.GetUserPermissionsAsync(userId);
                return Ok(new { success = true, data = permissions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户权限失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("user/{userId}/permissions")]
        /// <summary>
        /// 同步用户权限：前端传入目标集合，后端自动增量添加/移除。
        /// </summary>
        public async Task<IActionResult> AssignPermissions(int userId, [FromBody] AssignPermissionsRequest request)
        {
            try
            {
                int[] permissionIds = request.PermissionIds;

                // 读取当前权限，用于计算增量变化。
                var currentPermissions = await _userManagementService.GetUserPermissionsAsync(userId);
                var currentPermissionIds = currentPermissions.Select(p => p.Id).ToList();

                // 添加新增权限。
                var permissionsToAdd = permissionIds.Where(id => !currentPermissionIds.Contains(id)).ToList();
                foreach (var permissionId in permissionsToAdd)
                {
                    await _userManagementService.AssignPermissionAsync(userId, permissionId, userId);
                }

                // 移除被取消的权限。
                var permissionsToRemove = currentPermissionIds.Where(id => !permissionIds.Contains(id)).ToList();
                foreach (var permissionId in permissionsToRemove)
                {
                    await _userManagementService.RemovePermissionAsync(userId, permissionId);
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分配权限失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("user/{id}/reset-password")]
        /// <summary>
        /// 重置用户密码（服务端哈希后写入）。
        /// </summary>
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NewPassword))
                    return BadRequest(new { success = false, message = "新密码不能为空" });

                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "用户不存在" });

                var hashedPassword = _authService.HashPassword(request.NewPassword);
                var success = await _userManagementService.UpdateUserPasswordAsync(id, hashedPassword);
                
                if (success)
                    return Ok(new { success = true });
                return BadRequest(new { success = false, message = "重置密码失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重置密码失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("user/{userId}/toggle-status")]
        /// <summary>
        /// 启用或停用指定用户账号。
        /// </summary>
        public async Task<IActionResult> ToggleUserStatus(int userId, [FromBody] ToggleStatusRequest request)
        {
            try
            {
                bool isActive = request.IsActive;

                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "用户不存在" });

                user.IsActive = isActive;
                var success = await _userManagementService.UpdateUserAsync(user);
                if (success)
                    return Ok(new { success = true });
                return BadRequest(new { success = false, message = "更新用户状态失败" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新用户状态失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
