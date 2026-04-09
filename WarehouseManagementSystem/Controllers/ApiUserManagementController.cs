using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/usermanagement")]
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
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string DisplayName { get; set; }
            public bool IsAdmin { get; set; }
        }

        public class UserUpdateRequest
        {
            public string Email { get; set; }
            public string DisplayName { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsActive { get; set; }
        }

        public class AssignPermissionsRequest
        {
            public int[] PermissionIds { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string NewPassword { get; set; }
        }

        public class ToggleStatusRequest
        {
            public bool IsActive { get; set; }
        }

        [HttpPost("user")]
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
                // 注意：这里我们可能不想在普通更新中覆盖 IsActive，除非明确传了
                // 但为了简单起见，且前端传了 isActive，我们可以更新它
                // 或者我们可以决定这个接口只更新基本信息
                
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
        public async Task<IActionResult> AssignPermissions(int userId, [FromBody] AssignPermissionsRequest request)
        {
            try
            {
                int[] permissionIds = request.PermissionIds;

                // 获取当前权限
                var currentPermissions = await _userManagementService.GetUserPermissionsAsync(userId);
                var currentPermissionIds = currentPermissions.Select(p => p.Id).ToList();

                // 添加新权限
                var permissionsToAdd = permissionIds.Where(id => !currentPermissionIds.Contains(id)).ToList();
                foreach (var permissionId in permissionsToAdd)
                {
                    await _userManagementService.AssignPermissionAsync(userId, permissionId, userId);
                }

                // 移除取消的权限
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
