using System.ComponentModel.DataAnnotations;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 系统权限与菜单项定义。对应数据库中的 Permissions 表。
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }
        
        /// <summary>权限唯一标识代码 (如: UserManagement.View)</summary>
        [Required]
        [StringLength(50)]
        public string Code { get; set; }
        
        /// <summary>权限描述名称 (菜单显示文本)</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        /// <summary>详细描述说明</summary>
        [StringLength(200)]
        public string? Description { get; set; }
        
        /// <summary>对应的后端控制器名称 (可选，用于 API 拦截)</summary>
        [StringLength(100)]
        public string? Controller { get; set; }
        
        /// <summary>对应的 Action 名称 (可选)</summary>
        [StringLength(100)]
        public string? Action { get; set; }
        
        /// <summary>是否激活此项菜单/权限</summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>前端菜单排序权重</summary>
        public int SortOrder { get; set; } = 0;
    }
}
