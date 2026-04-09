using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseManagementSystem.Models
{
    [Flags]
    public enum HeartbeatTaskStatus
    {
        [Display(Name = "等待执行")]
        Waiting = 0,
        [Display(Name = "正在执行")]
        Working = 1,
        [Display(Name = "已经完成")]
        Finished = 4,
        [Display(Name = "取消")]
        Cancel = 8,
    }

    public enum TaskStatuEnum
    {
        Waiting = 0,
        Working = 1,
        Finished = 4,
        Cancel = 8
    }
}
