using System.Collections.Generic;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 分页响应模型
    /// </summary>
    /// <typeparam name="T">列表项类型</typeparam>
    public class PaginatedResponse<T>
    {
        /// <summary>
        /// 列表项
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// 总数
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// 当前页码
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// 每页数量
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 是否有下一页
        /// </summary>
        public bool HasNextPage => Page < TotalPages;

        /// <summary>
        /// 是否有上一页
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// 创建分页响应
        /// </summary>
        public static PaginatedResponse<T> Create(List<T> items, int total, int page, int pageSize)
        {
            var totalPages = (total + pageSize - 1) / pageSize;
            return new PaginatedResponse<T>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }
    }
}
