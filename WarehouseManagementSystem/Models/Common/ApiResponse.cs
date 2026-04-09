using System.Collections.Generic;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 统一的API响应模型
    /// </summary>
    /// <typeparam name="T">响应数据类型</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 响应消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 响应数据
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 验证错误信息（仅在验证失败时使用）
        /// </summary>
        public Dictionary<string, string[]> Errors { get; set; }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static ApiResponse<T> SuccessResponse(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 创建失败响应
        /// </summary>
        public static ApiResponse<T> FailureResponse(string message, Dictionary<string, string[]> errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }

    /// <summary>
    /// 不带数据的API响应模型
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 响应消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 验证错误信息（仅在验证失败时使用）
        /// </summary>
        public Dictionary<string, string[]> Errors { get; set; }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static ApiResponse SuccessResponse(string message = "操作成功")
        {
            return new ApiResponse
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// 创建失败响应
        /// </summary>
        public static ApiResponse FailureResponse(string message, Dictionary<string, string[]> errors = null)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }
}
