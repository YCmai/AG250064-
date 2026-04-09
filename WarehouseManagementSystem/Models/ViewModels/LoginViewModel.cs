using System.ComponentModel.DataAnnotations;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 登录请求视图模型。用于用户身份验证。
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>登录用户名</summary>
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(50, ErrorMessage = "用户名长度不能超过50个字符")]
        public string Username { get; set; }
        
        /// <summary>登录密码</summary>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, ErrorMessage = "密码长度不能超过100个字符")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        
        /// <summary>是否记住登录状态</summary>
        public bool RememberMe { get; set; }
        
        /// <summary>登录成功后跳转的 URL</summary>
        public string? ReturnUrl { get; set; }
    }
}
