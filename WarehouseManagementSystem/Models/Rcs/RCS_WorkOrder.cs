using System.ComponentModel.DataAnnotations;

namespace WarehouseManagementSystem.Models
{
    /// <summary>
    /// 工单信息表 - 接收MES发送的工单信息
    /// </summary>
    public class RCS_WorkOrder
    {
        public int ID { get; set; }

        /// <summary>
        /// 亚批号（工单号）
        /// </summary>
        [Required]
        [StringLength(14)]
        public string OrderNumber { get; set; }

        /// <summary>
        /// 产品编码（物料编码）
        /// </summary>
        [Required]
        [StringLength(18)]
        public string MaterialNumber { get; set; }

        /// <summary>
        /// 产品名称（物料名称）
        /// </summary>
        [Required]
        [StringLength(40)]
        public string MaterialName { get; set; }

        /// <summary>
        /// 消息类型（1=生效, 2=失效）
        /// </summary>
        [Required]
        [StringLength(1)]
        public string MsgType { get; set; }

        /// <summary>
        /// 接收时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 处理状态（0=未处理, 1=已处理）
        /// </summary>
        public int ProcessStatus { get; set; } = 0;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }
    }
}

