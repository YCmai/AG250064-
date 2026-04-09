using System.ComponentModel.DataAnnotations;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 系统用户信息实体。对应数据库中的 Users 表。
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        
        /// <summary>登录用户名 (唯一)</summary>
        [Required]
        [StringLength(50)]
        public string Username { get; set; }
        
        /// <summary>加密后的登录密码</summary>
        [Required]
        [StringLength(255)]
        public string Password { get; set; }
        
        /// <summary>用户显示名称/真实姓名</summary>
        [StringLength(100)]
        public string? DisplayName { get; set; }
        
        /// <summary>邮箱地址</summary>
        [StringLength(100)]
        public string? Email { get; set; }
        
        /// <summary>账号是否启用</summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>最后一次登录时间</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>最后一次更新信息时间</summary>
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>是否为系统管理员角色</summary>
        public bool IsAdmin { get; set; } = false;
    }
}
