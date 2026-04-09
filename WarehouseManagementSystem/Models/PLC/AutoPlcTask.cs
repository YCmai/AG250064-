using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Client100.Entity
{
    
    public class AutoPlcTask
    {
       
        public int Id { get; set; }
        /// <summary>关联的业务订单编号</summary>
        public string OrderCode { get; set; }
        /// <summary>
        /// 任务执行状态：1:写入bool, 2:重置bool, 3:写入INT, 4:重置INT, 5:写入String, 6:重置String
        /// </summary>
        public int Status { get; set; }
        /// <summary>信号是否已下发 (0:未发, 1:已发, 2:失败)</summary>
        public int IsSend { get; set; }
        /// <summary>信号逻辑名称 (如: 请求进入, 小车到位)</summary>
        public string Signal { get; set; }

        public DateTime? CreatingTime { get; set; }

        /// <summary>备注描述或异常信息</summary>
        public string Remark { get; set; }

        public DateTime? UpdateTime { get; set; }

        /// <summary>PLC 逻辑类型标识</summary>
        public string PlcType { get; set; }

        /// <summary>数据块 DB 标识</summary>
        public string PLCTypeDb { get; set; }
    }

   

}
