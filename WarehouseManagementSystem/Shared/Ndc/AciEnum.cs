namespace WarehouseManagementSystem.Shared.Ndc
{
    public enum FunctionCode
    {
        Error = 0, // 错误           
        Normal = 1, // 正常          
        NotUsed = 2, // 未使用         
        Reserved = 3, // 保留        
        HeartBeatPoll = 4, // 心跳轮询检测   
        HeartBeatACK = 5, // 心跳应答回执    
    }

    public enum MessageType
    {
        None = 0x00, // 无
        OrderInitiate = 0x71, // 发起/初始化订单 (字符 q)           Message 'q' 
        OrderAcknowledge = 0x62, // 确认接受订单 (字符 b)        Message 'b'  (q())
        OrderRequest = 0x6A, // Message 'j' 
        OrderEvent = 0x73, // Message 's' 
        OrderStatus = 0x6F, // Message 'o' 
        OrderDelete = 0x6E, // Message 'n' 
        LocalParamCommand = 0x6D, // Message 'm'  ()
        LocalParamContents = 0x77, // Message 'w' ()
        LocalParamRequest = 0x72, // Message 'r' ()
        GlobalParamCommand = 0x67, // Message 'g' ()
        GlobalParamStatus = 0x70, // Message 'p' ()
        AgvOmPlcToHost = 0x3C, // Message '<'
        HostToAgvOmPlc = 0x3E, // Message '>'
    }
    /// <summary>
    /// OM-PLC
    /// </summary>
    public enum OmPlcCommandCode
    {
        None = 0,
        ReadMultiByte = 5,                 
        WriteMultiByte = 6,                
        NakReadMultiByte = 7, // (plc)
        NakWriteMultiByte = 8, // (plc)
    }
    /// <summary>
    // 
    /// </summary>
    public enum LocalParamCommandCode
    {
        InsertSpontaneous = 0,          
        InsertRequested = 1,            
        Delete = 2,                     
        Read = 3,                       
        ChangePriority = 4,             
        ConnectVehicle = 5,             
    }
    /// <summary>
    // 
    /// </summary>
    enum GlobalParamCommandCode
    {
        None = 0,                      
        Read = 1,                      
        Write = 2,                     
    }
    /// <summary>
    // 
    /// </summary>
    enum GlobalParamStatusCode
    {
        None = 0,                     
        ReadAck = 1, // Ack
        WriteAck = 2, // Ack
        ReadNak = 3, // Nak
        WriteNak = 4, // Nak
    }
    /// <summary>
    // 
    /// </summary>
    enum AckStatusCode
    {
        None = -1,                                              
        NotImplemented1 = 5,                    
        NotImplemented2 = 6,                    
        NotImplemented3 = 12,                   
        NotImplemented4 = 13,                   

        // Response Order Initial ('q')          
        OrderRejected = 0,                      
        OrderAccepted = 1,                      
        ParamNumberInvalid = 8,                 
        PriorityError = 9,                      
        StructureError = 10,                    
        FullBuffer = 11,                        
        ParamNumberHigh = 15,                   
        UpdateNotAllow = 16,                    
        MissingParam = 26,                      
        DuplicatedKey = 27,                     
        FormatCodeInvalid = 28,                 

        // Response Delete Order ('n')           
        CancelAcknowledge = 25,                 
        OrderCancelled = 4,                     
        IndexNotActivated = 14,                 
        CarrierError = 22,                      
        IndexInvalid = 24,                      

        // Response Local Parameter Update ('m') 
        ParamAcknowledge = 7,                   
        ParamDeleted = 18,                      

        // Order Finished                        
        OrderFinished = 3,                      

        // Specified Event                       
        ParamAccepted = 19,                     
        ParamRejected = 20,                     
        InputReleased = 23,                     

        // Fatal Error                           
        FatalError = 17,                        

        // Response Change Order Priority ('m')  
        PriorityAcknowledge = 35,               

        // Carrier / Order Connection Event      
        ConnectionSucceed = 39,                 
        ConnectionFail = 40,                    
        AllocationCarrier = 2,                  
        LostCarrier = 21,                       
        ConnectionCarrier = 37,                 
    }

    /// <summary>
    // 
    /// </summary>
    enum AckGroupCode
    {
        None = 0x0000, // 0
        GroupI = 0x0001, // 1
        GroupII = 0x0002, // 2
        GroupIII = 0x0004, // 4
        GroupIV = 0x0008, // 8
        GroupV = 0x0010, // 16
        GroupVI = 0x0020, // 32
        GroupVII = 0x0040, // 64
        GroupVIII = 0x0080, // 128
        GroupIX = 0x0100, // 256
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderItemCode
    {
        None = -1,                  
        NumericalInterval = 0,      
        ExternalTrigger = 1,        
        InternalTrigger = 2,        
        UsedIndex = 3,              
        CarrierNumber = 4,          
        PriorityOrder = 5,          
        OrderState = 6,             
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderErrorCode
    {
        None = 0x00, // 无                
        EndOfStream = 0x7F,         
        GeneralError = 0x80,        
        InvalidArgument = 0x81,     
        RequestPending = 0x82,      
        FullBuffer = 0x83,          
        Unknow = 0x84,              
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderUnitType
    {
        Requester = 0,              
        DEBUG = 2,                  
        ACI = 3, // ACI
        CWay = 4,                   
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderTriggerModule
    {
        SystemControl = 0x02, // SystemGo                   
        CarrierManager = 0x33, // CarWash and CarCharge      
        InputTrigger = 0x2C, // Input started order        
        OrderManager = 0x2F, // Sfork started order        
    }

    /// <summary>
    // 
    /// </summary>
    enum EventStatusCode
    {
        NotUsed = 1,                
        Pending = 2,                
        Transitory1 = 3, // 1
        Transitory2 = 4, // 2
        WaitingVehicle = 5, // ()
        Transitory3 = 6, // 3
        MovingVehicle = 7,          
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderListCode
    {
        None = 0,                   
        FreeInstance = 1,           
        PendingList = 2,            
        ActiveList = 3,             
        CmvRequestList = 4, // Cmv
        CarRequestList = 5,         
        CarReleaseList = 6,         
        CmvList = 7, // Cmv
        CallocRequestList = 8,      
        CmCommandsList = 9, // Cm
        OmDebugList = 10, // Om
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderStateCode
    {
        None = -1,                  
        EmptyInstance = 0,          
        FuncEvaluated = 1,          
        FuncNotEvaluated = 2,       
        NotUsed = 3,                
        PendingParamRequest = 4,    
        Dalayed = 5,                
        BreakEvaluated = 6,         
        TerminateOrder = 7,         
        Cancelled = 8,              
        CancelTermination = 9,      
        Retry = 10,                 
        InputPollRequest = 11,      
        SystemFuncRequest = 12,     
        ExplicitParamRequest = 13,  
        PlcRequest = 14, // plc
        BarCodeRequest = 15,        
        EvaluatedByDebugger = 16,   
        WaitingForQueue = 17,       
        WaitingForChild = 18,       
    }

    /// <summary>
    // 
    /// </summary>
    enum OrderStatusCode
    {
        None = -1,                  
        TRUE = 0,                   
        FALSE = 1,                  
        ERROR = 2,                  
    }
    /// <summary>
    // 
    /// </summary>
    enum OrderTriggerCode
    {
        None = -1,                  
        Internal = 0,               
        Debug = 2,                  
        ACI = 3, // ACI
        Cway = 4, // Cway
        Multidrop = 11, // rop
    }

    /// <summary>
    // 
    /// </summary>
    enum CarrierMainStatusCode
    {
        None = -1,                  
        Cancelled = 0,              
        CancelConnnection = 1,      
        Free = 2, // ()
        Allocated = 3,              
        Active = 4,                 
        Connected = 5,              
        Unknow = 6,                 
    }
    /// <summary>
    // 
    /// </summary>
    enum CarrierMoveStateCode
    {
        None = -1,                  
        Unknow = 0,                 
        StandOnPoint = 1,           
        MovingToEntry = 2,          
        MovingToPoint = 3,          
        MovingToExit = 4,           
        MovingToRequiredExit = 5,   
        MovingToEscape = 6,         
        WaitingForCommand = 7,      
    }
}






