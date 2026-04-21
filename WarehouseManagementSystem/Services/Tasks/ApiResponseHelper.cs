using System.Collections.Generic;
using WarehouseManagementSystem.Models;

namespace WarehouseManagementSystem.Services.Tasks
{
    /// <summary>
    /// API响应助手类，简化API响应的创建
    /// </summary>
    public static class ApiResponseHelper
    {
        /// <summary>
        /// 创建成功响应（带数据）
        /// </summary>
        public static ApiResponse<T> Success<T>(T data, string message = "操作成功")
        {
            return ApiResponse<T>.SuccessResponse(data, message);
        }

        /// <summary>
        /// 创建成功响应（不带数据）
        /// </summary>
        public static ApiResponse Success(string message = "操作成功")
        {
            return ApiResponse.SuccessResponse(message);
        }

        /// <summary>
        /// 创建失败响应（带数据）
        /// </summary>
        public static ApiResponse<T> Failure<T>(string message, Dictionary<string, string[]> errors = null)
        {
            return ApiResponse<T>.FailureResponse(message, errors);
        }

        /// <summary>
        /// 创建失败响应（不带数据）
        /// </summary>
        public static ApiResponse Failure(string message, Dictionary<string, string[]> errors = null)
        {
            return ApiResponse.FailureResponse(message, errors);
        }

        /// <summary>
        /// 创建分页成功响应
        /// </summary>
        public static ApiResponse<PaginatedResponse<T>> SuccessPaginated<T>(
            List<T> items, int total, int page, int pageSize, string message = "操作成功")
        {
            var paginatedData = PaginatedResponse<T>.Create(items, total, page, pageSize);
            return ApiResponse<PaginatedResponse<T>>.SuccessResponse(paginatedData, message);
        }

        /// <summary>
        /// 创建验证错误响应
        /// </summary>
        public static ApiResponse<T> ValidationError<T>(Dictionary<string, string[]> errors)
        {
            return ApiResponse<T>.FailureResponse("数据验证失败", errors);
        }

        /// <summary>
        /// 创建验证错误响应（不带数据）
        /// </summary>
        public static ApiResponse ValidationError(Dictionary<string, string[]> errors)
        {
            return ApiResponse.FailureResponse("数据验证失败", errors);
        }
    }
}
