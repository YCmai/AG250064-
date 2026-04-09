-- =============================================
-- 用户权限管理相关建表脚本
-- 请在数据库中执行此脚本后重启程序
-- =============================================

-- 1. 用户表
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Users' AND xtype = 'U')
BEGIN
    CREATE TABLE Users (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Username    NVARCHAR(50)  NOT NULL UNIQUE,
        Password    NVARCHAR(255) NOT NULL,
        DisplayName NVARCHAR(100) NULL,
        Email       NVARCHAR(100) NULL,
        IsActive    BIT           NOT NULL DEFAULT 1,
        IsAdmin     BIT           NOT NULL DEFAULT 0,
        CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
        UpdatedAt   DATETIME      NULL,
        LastLoginAt DATETIME      NULL
    );
    PRINT '创建 Users 表成功';

    -- 插入默认管理员账号（密码: admin123，需要程序层加密后替换）
    INSERT INTO Users (Username, Password, DisplayName, IsActive, IsAdmin)
    VALUES ('admin', 'AQAAAAEAACcQAAAAELx...', '管理员', 1, 1);
    PRINT '插入默认管理员成功';
END
ELSE
    PRINT 'Users 表已存在，跳过';

-- 2. 权限表
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Permissions' AND xtype = 'U')
BEGIN
    CREATE TABLE Permissions (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Code        NVARCHAR(50)  NOT NULL UNIQUE,
        Name        NVARCHAR(100) NOT NULL,
        Description NVARCHAR(200) NULL,
        Controller  NVARCHAR(100) NULL,
        [Action]    NVARCHAR(100) NULL,
        IsActive    BIT           NOT NULL DEFAULT 1,
        SortOrder   INT           NOT NULL DEFAULT 0
    );
    PRINT '创建 Permissions 表成功';
END
ELSE
    PRINT 'Permissions 表已存在，跳过';

-- 3. 用户-权限关联表
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'UserPermissions' AND xtype = 'U')
BEGIN
    CREATE TABLE UserPermissions (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        UserId       INT      NOT NULL,
        PermissionId INT      NOT NULL,
        GrantedAt    DATETIME NOT NULL DEFAULT GETDATE(),
        GrantedBy    INT      NULL,
        AssignedBy   INT      NULL,
        CONSTRAINT FK_UserPermissions_Users       FOREIGN KEY (UserId)       REFERENCES Users(Id),
        CONSTRAINT FK_UserPermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES Permissions(Id),
        CONSTRAINT UQ_UserPermissions UNIQUE (UserId, PermissionId)
    );
    PRINT '创建 UserPermissions 表成功';
END
ELSE
    PRINT 'UserPermissions 表已存在，跳过';

PRINT '--- 所有表创建完毕 ---';
