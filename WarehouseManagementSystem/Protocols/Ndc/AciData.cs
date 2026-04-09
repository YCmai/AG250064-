using WarehouseManagementSystem.Shared.Ndc;
using SuperPortLibrary;

namespace WarehouseManagementSystem.Protocols.Ndc
{
    /// <summary>
    /// ȫ��Aci����
    /// </summary>
    public class GeneralAciData : ApplicationData
    {
        public FunctionCode DataFunction { get; set; }
        public MessageType DataType { get; set; }
        public int CompileErrorCode { get; set; }
        /***************************************************
         *    Compile Error Code
         *  0.None
         *  1.Unexpected Function Code
         *  2.Unexpected Message Type
         *  3.Unexpected Order Acknowledge Data Length
         *  4.Unexpected Order Event Data Length
         *  5.Unexpected Order Status Header Data Length
         *  6.Unexpected Order Status Main Data Length
         *  7.Unexpected Local Param Contents Length
         *  8.Unexpected Global Param Status Header Length
         *  9.Unexpected Global Param Status Data Length
         * 10.Unexpected Acknowledge Code
         * 11.Unexpected EventStatus Code
         * 12.Unexpected Order Item Code
         * 13.Unexpected Order List Code
         * 14.Unexpected Order State Code
         * 15.Unexpected Order Status Code
         * 16.Unexpected Order Trigger Code
         * 17.Unexpected Carrier Main Status Code
         * 18.Unexpected Carrier Move State Code
         * 19.Unexpected Order Error Code
         * 20.Unexpected Global Param Status Code
         * 21.Unexpected Magic Code (0x0000 - 0x7FFF)
         * 22.Unexpected Global Parameter Number (1 - 16)
         * 23.Unexpected Global Parameter Index (0 - 10000)
         ***************************************************/
        public int DecompileErrorCode { get; set; }
        /***************************************************
         *    Decompile Error Code
         *  0.None
         *  1.Unexpected Function Code
         *  2.Unexpected Message Type
         *  3.Unexpected Transport Structure (1 - 255)
         *  4.Unexpected Order Priority (0 - 99)
         *  5.Unexpected Order Item Code
         *  6.Unexpected Magic Code (0x0000 - 0x7FFF)
         *  7.Unexpected Item Offset (0x0000 - 0xFFFF)
         *  8.Unexpected Item Number (0x0001 - 0xFFFF)
         *  9.Unexpected Order Index (0x0000 - 0xFFFF)
         * 10.Unexpected Carrier ID (1 - 255)
         * 11.Unexpected Local Param Command
         * 12.Unexpected Local Param Index (0 - 31)
         * 13.Unexpected Local Param Values (Null)
         * 14.Unexpected Global Parameter Number (1 - 16)
         * 15.Unexpected Global Parameter Index (0 - 10000)
         * 16.Unexpected Global Param Command 
         ***************************************************/
    }
    /// <summary>
    /// �������Aci����
    /// </summary>
    class OrderInitiateAciData : GeneralAciData
    {
        public OrderInitiateAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderInitiate;
        }

        public int StructureID { get; set; }
        public int OrderPriority { get; set; }
        public int OrderKey { get; set; }
        public int[] ParamValues { get; set; }
    }
    /// <summary>
    /// ����ȷ��Aci����
    /// </summary>
    class OrderAcknowledgeAciData : GeneralAciData
    {
        public OrderAcknowledgeAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderAcknowledge;
        }

        public int OrderIndex { get; set; }
        public int OrderKey { get; set; }
        public int StructureID { get; set; }
        public int AcknowledgeParam { get; set; }
        public AckStatusCode AcknowledgeStatus { get; set; }
        public AckGroupCode AcknowledgeGroup { get; set; }
    }
    /// <summary>
    /// ��������Aci����
    /// </summary>
    class OrderRequestAciData : GeneralAciData
    {
        public OrderRequestAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderRequest;
        }

        public int MagicIndex { get; set; }
        public OrderItemCode ItemCode { get; set; }
        public int ItemOffset { get; set; }
        public int ItemNumber { get; set; }
        public uint ParamFlag { get; set; }
        public int ItemParam { get; set; }
        public OrderUnitType UnitType { get; set; }
        public int LogicalLine { get; set; }
        public int LogicalUnit { get; set; }
        public OrderTriggerModule TriggerModule { get; set; }
    }
    /// <summary>
    /// �����¼�Aci����
    /// </summary>
    class OrderEventAciData : GeneralAciData
    {
        public OrderEventAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderEvent;
        }

        public int OrderIndex { get; set; }
        public int StructureID { get; set; }
        public EventStatusCode EventStatus { get; set; }
        public int MagicCode1 { get; set; }
        public int MagicCode2 { get; set; }
        public int MagicCode3 { get; set; }
        public int CarrierID { get; set; }
        public int CarrierStation { get; set; }
        public int CarrierStatus { get; set; }
        public int ParamNumber { get; set; }
        public int[] ParamValues { get; set; }
    }
    /// <summary>
    /// ����״̬Aci����
    /// </summary>
    class OrderStatusAciData : GeneralAciData
    {
        public OrderStatusAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderStatus;
        }

        public int OrderIndex { get; set; }
        public int MagicIndex { get; set; }
        public OrderItemCode ItemCode { get; set; }
        public OrderErrorCode ErrorCode { get; set; }
        public DateTime OrderStartTime { get; set; }
        public int InitialStructure { get; set; }
        public int CurrentStructure { get; set; }
        public int CurrentRow { get; set; }
        public OrderListCode OrderList { get; set; }
        public OrderStatusCode OrderStatus { get; set; }
        public OrderStateCode OrderState { get; set; }
        public int OrderPriority { get; set; }
        public OrderTriggerCode OrderTrigger { get; set; }
        public int TriggerParam1 { get; set; }
        public int TriggerParam2 { get; set; }
        public int CarrierID { get; set; }
        public CarrierMainStatusCode MainStatus { get; set; }
        public CarrierMoveStateCode MoveState { get; set; }
        public int PreviousStation { get; set; }
        public int FinalDestination { get; set; }
        public int PlcStatus { get; set; }
    }
    //����ɾ��Aci����
    class OrderDeleteAciData : GeneralAciData
    {
        public OrderDeleteAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.OrderDelete;
        }

        public int OrderIndex { get; set; }
        public int CarrierID { get; set; }
    }
    /// <summary>
    /// �����������Aci����
    /// </summary>
    class LocalParamCommandAciData : GeneralAciData
    {
        public LocalParamCommandAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.LocalParamCommand;
        }

        public int OrderIndex { get; set; }
        public LocalParamCommandCode Command { get; set; }
        public int ParamIndex { get; set; }
        public int OrderPriority { get; set; }
        public int CarrierID { get; set; }
        public int[] ParamValues { get; set; }
    }

    class LocalParamContentsAciData : GeneralAciData
    {
        public LocalParamContentsAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.LocalParamContents;
        }

        public int OrderIndex { get; set; }
        public int[] ParamIndexes { get; set; }
        public int[] ParamValues { get; set; }
    }
    /// <summary>
    /// �����������Aci����
    /// </summary>
    class LocalParamRequestAciData : GeneralAciData
    {
        public LocalParamRequestAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.LocalParamRequest;
        }

        public int OrderIndex { get; set; }
        public int ParamIndex { get; set; }
    }
    /// <summary>
    /// ȫ���������Aci����
    /// </summary>
    class GlobalParamCommandAciData : GeneralAciData
    {
        public GlobalParamCommandAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.GlobalParamCommand;
        }

        public int MagicIndex { get; set; }
        public GlobalParamCommandCode Command { get; set; }
        public int ParamNumber { get; set; }
        public int ParamIndex { get; set; }
        public int[] ParamValues { get; set; }
    }
    //ȫ��״̬����Aci����
    class GlobalParamStatusAciData : GeneralAciData
    {
        public GlobalParamStatusAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.GlobalParamStatus;
        }

        public int MagicIndex { get; set; }
        public GlobalParamStatusCode Status { get; set; }
        public int ParamNumber { get; set; }
        public int ParamIndex { get; set; }
        public int[] ParamValues { get; set; }
    }

    class OmPlcCommandAciData : GeneralAciData
    {
        public OmPlcCommandAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.HostToAgvOmPlc;
        }
        public int MagicIndex { get; set; }
        public OmPlcCommandCode Command { get; set; }
        public int ParamCarId { get; set; }
        public int ParamNumber { get; set; }
        public int ParamIndex { get; set; }
        public int[] ParamValues { get; set; }
    }
    class OmPlcStatusAciData : GeneralAciData
    {
        public OmPlcStatusAciData()
        {
            DataFunction = FunctionCode.Normal;
            DataType = MessageType.AgvOmPlcToHost;
        }
        public int MagicIndex { get; set; }
        public OmPlcCommandCode Status { get; set; }
        public int ParamNumber { get; set; }
        public int ParamIndex { get; set; }
        public int ParamCarID { get; set; }
        public int[] ParamValues { get; set; }
    }

}


