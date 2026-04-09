IF OBJECT_ID('dbo.RCS_WmsRequestLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RCS_WmsRequestLog
    (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BusinessType INT NOT NULL,
        BusinessKey NVARCHAR(100) NOT NULL,
        TaskNumber NVARCHAR(50) NULL,
        OrderNumber NVARCHAR(50) NULL,
        RequestUrl NVARCHAR(500) NULL,
        RequestJson NVARCHAR(MAX) NULL,
        ResponseJson NVARCHAR(MAX) NULL,
        RequestStatus INT NOT NULL CONSTRAINT DF_RCS_WmsRequestLog_RequestStatus DEFAULT (0),
        RetryCount INT NOT NULL CONSTRAINT DF_RCS_WmsRequestLog_RetryCount DEFAULT (0),
        LastRequestTime DATETIME NULL,
        LastResponseTime DATETIME NULL,
        NextRetryTime DATETIME NULL,
        ErrorMsg NVARCHAR(1024) NULL,
        CreateTime DATETIME NOT NULL CONSTRAINT DF_RCS_WmsRequestLog_CreateTime DEFAULT (GETDATE()),
        UpdateTime DATETIME NULL
    );
END
GO

IF COL_LENGTH('dbo.RCS_WmsRequestLog', 'RequestUrl') IS NULL
BEGIN
    ALTER TABLE dbo.RCS_WmsRequestLog
    ADD RequestUrl NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID('dbo.RCS_WmsMaterialArrival', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RCS_WmsMaterialArrival
    (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestLogId INT NOT NULL,
        OrderNumber NVARCHAR(14) NOT NULL,
        PalletNumber NVARCHAR(17) NOT NULL,
        CreateTime DATETIME NOT NULL CONSTRAINT DF_RCS_WmsMaterialArrival_CreateTime DEFAULT (GETDATE()),
        CONSTRAINT FK_RCS_WmsMaterialArrival_RequestLog FOREIGN KEY (RequestLogId) REFERENCES dbo.RCS_WmsRequestLog(ID)
    );
END
GO

IF OBJECT_ID('dbo.RCS_WmsMaterialArrivalItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RCS_WmsMaterialArrivalItems
    (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaterialArrivalId INT NOT NULL,
        Barcode NVARCHAR(12) NOT NULL,
        CONSTRAINT FK_RCS_WmsMaterialArrivalItems_Arrival FOREIGN KEY (MaterialArrivalId) REFERENCES dbo.RCS_WmsMaterialArrival(ID)
    );
END
GO

IF OBJECT_ID('dbo.RCS_WmsSafetySignal', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RCS_WmsSafetySignal
    (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestLogId INT NOT NULL,
        TaskNumber NVARCHAR(50) NOT NULL,
        RequestDate DATETIME NOT NULL,
        Room NVARCHAR(20) NOT NULL,
        SafeFlag NVARCHAR(1) NULL,
        CreateTime DATETIME NOT NULL CONSTRAINT DF_RCS_WmsSafetySignal_CreateTime DEFAULT (GETDATE()),
        CONSTRAINT FK_RCS_WmsSafetySignal_RequestLog FOREIGN KEY (RequestLogId) REFERENCES dbo.RCS_WmsRequestLog(ID)
    );
END
GO

IF OBJECT_ID('dbo.RCS_WmsJobFeedback', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RCS_WmsJobFeedback
    (
        ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestLogId INT NOT NULL,
        TaskNumber NVARCHAR(50) NOT NULL,
        Status NVARCHAR(1) NOT NULL,
        CreateTime DATETIME NOT NULL CONSTRAINT DF_RCS_WmsJobFeedback_CreateTime DEFAULT (GETDATE()),
        CONSTRAINT FK_RCS_WmsJobFeedback_RequestLog FOREIGN KEY (RequestLogId) REFERENCES dbo.RCS_WmsRequestLog(ID)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsRequestLog_BusinessType_Status' AND object_id = OBJECT_ID('dbo.RCS_WmsRequestLog'))
BEGIN
    CREATE INDEX IX_RCS_WmsRequestLog_BusinessType_Status
    ON dbo.RCS_WmsRequestLog(BusinessType, RequestStatus, CreateTime DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsRequestLog_TaskNumber' AND object_id = OBJECT_ID('dbo.RCS_WmsRequestLog'))
BEGIN
    CREATE INDEX IX_RCS_WmsRequestLog_TaskNumber
    ON dbo.RCS_WmsRequestLog(TaskNumber);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsRequestLog_NextRetryTime' AND object_id = OBJECT_ID('dbo.RCS_WmsRequestLog'))
BEGIN
    CREATE INDEX IX_RCS_WmsRequestLog_NextRetryTime
    ON dbo.RCS_WmsRequestLog(BusinessType, NextRetryTime, RequestStatus);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsMaterialArrival_OrderNumber' AND object_id = OBJECT_ID('dbo.RCS_WmsMaterialArrival'))
BEGIN
    CREATE INDEX IX_RCS_WmsMaterialArrival_OrderNumber
    ON dbo.RCS_WmsMaterialArrival(OrderNumber);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsSafetySignal_TaskNumber' AND object_id = OBJECT_ID('dbo.RCS_WmsSafetySignal'))
BEGIN
    CREATE INDEX IX_RCS_WmsSafetySignal_TaskNumber
    ON dbo.RCS_WmsSafetySignal(TaskNumber, RequestDate DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsSafetySignal_RequestLogId' AND object_id = OBJECT_ID('dbo.RCS_WmsSafetySignal'))
BEGIN
    CREATE INDEX IX_RCS_WmsSafetySignal_RequestLogId
    ON dbo.RCS_WmsSafetySignal(RequestLogId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RCS_WmsJobFeedback_TaskNumber' AND object_id = OBJECT_ID('dbo.RCS_WmsJobFeedback'))
BEGIN
    CREATE INDEX IX_RCS_WmsJobFeedback_TaskNumber
    ON dbo.RCS_WmsJobFeedback(TaskNumber, CreateTime DESC);
END
GO
