/*
SCRUM-193 customer-assisted refund patch for an existing SQL Server database.

Run this script against the target database selected by the deployment command.
The script deliberately has no USE statement, so the database name is not
hardcoded.

Security:
- Store only SHA-256 refund-claim token hashes.
- Encrypt bank account and account-holder values in the application.
- Do not log decrypted bank data, raw claim tokens, or connection secrets.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.REFUND', N'U') IS NULL
BEGIN
    THROW 50001, 'Required table dbo.REFUND does not exist.', 1;
END;

IF OBJECT_ID(N'dbo.CUSTOMER_PROFILE', N'U') IS NULL
BEGIN
    THROW 50002, 'Required table dbo.CUSTOMER_PROFILE does not exist.', 1;
END;

IF OBJECT_ID(N'dbo.[USER]', N'U') IS NULL
BEGIN
    THROW 50003, 'Required table dbo.USER does not exist.', 1;
END;

IF OBJECT_ID(N'dbo.TICKET', N'U') IS NULL
BEGIN
    THROW 50004, 'Required table dbo.TICKET does not exist.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'dbo.REFUND')
      AND [name] = N'CK_REFUND_STATUS'
      AND [definition] NOT LIKE N'%MANUAL_REQUIRED%'
)
BEGIN
    ALTER TABLE dbo.[REFUND] DROP CONSTRAINT [CK_REFUND_STATUS];
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'dbo.REFUND')
      AND [name] = N'CK_REFUND_STATUS'
)
BEGIN
    ALTER TABLE dbo.[REFUND] WITH CHECK
        ADD CONSTRAINT [CK_REFUND_STATUS]
        CHECK ([refundStatus] IN
            ('PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'REQUESTED', 'MANUAL_REQUIRED'));
END;
GO

IF OBJECT_ID(N'dbo.BANK_DIRECTORY', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[BANK_DIRECTORY]
    (
        [bankCode] NVARCHAR(20) NOT NULL,
        [bankBin] NVARCHAR(20) NOT NULL,
        [shortName] NVARCHAR(100) NOT NULL,
        [fullName] NVARCHAR(255) NOT NULL,
        [isActive] BIT NOT NULL
            CONSTRAINT [DF_BANK_DIRECTORY_IS_ACTIVE] DEFAULT 1,
        [supportsAccountInquiry] BIT NOT NULL
            CONSTRAINT [DF_BANK_DIRECTORY_ACCOUNT_INQUIRY] DEFAULT 0,
        [supportsPayout] BIT NOT NULL
            CONSTRAINT [DF_BANK_DIRECTORY_PAYOUT] DEFAULT 0,
        [createdAt] DATETIME2 NOT NULL
            CONSTRAINT [DF_BANK_DIRECTORY_CREATED_AT] DEFAULT SYSUTCDATETIME(),
        [updatedAt] DATETIME2 NULL,

        CONSTRAINT [PK_BANK_DIRECTORY] PRIMARY KEY ([bankCode]),
        CONSTRAINT [UQ_BANK_DIRECTORY_BIN] UNIQUE ([bankBin])
    );
END;
GO

IF OBJECT_ID(N'dbo.REFUND_CLAIM', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[REFUND_CLAIM]
    (
        [refundClaimId] NVARCHAR(50) NOT NULL,
        [refundId] NVARCHAR(50) NOT NULL,
        [customerProfileId] NVARCHAR(50) NOT NULL,
        [bankCode] NVARCHAR(20) NULL,
        [claimStatus] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_REFUND_CLAIM_STATUS] DEFAULT 'PENDING_INFO',
        [accountValidationStatus] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_REFUND_CLAIM_VALIDATION] DEFAULT 'NOT_STARTED',
        [bankAccountEncrypted] VARBINARY(MAX) NULL,
        [bankAccountLast4] NVARCHAR(4) NULL,
        [accountHolderNameEncrypted] VARBINARY(MAX) NULL,
        [verifiedAccountHolderNameEncrypted] VARBINARY(MAX) NULL,
        [verificationProvider] NVARCHAR(100) NULL,
        [verificationReferenceCode] NVARCHAR(255) NULL,
        [verificationFailureReason] NVARCHAR(1000) NULL,
        [expiresAt] DATETIME2 NOT NULL,
        [submittedAt] DATETIME2 NULL,
        [processingAt] DATETIME2 NULL,
        [completedAt] DATETIME2 NULL,
        [createdAt] DATETIME2 NOT NULL
            CONSTRAINT [DF_REFUND_CLAIM_CREATED_AT] DEFAULT SYSUTCDATETIME(),
        [updatedAt] DATETIME2 NULL,
        [rowVersion] ROWVERSION NOT NULL,

        CONSTRAINT [PK_REFUND_CLAIM] PRIMARY KEY ([refundClaimId]),
        CONSTRAINT [UQ_REFUND_CLAIM_REFUND] UNIQUE ([refundId]),
        CONSTRAINT [CK_REFUND_CLAIM_STATUS] CHECK ([claimStatus] IN
            ('PENDING_INFO', 'VERIFIED', 'SUBMITTED', 'PROCESSING',
             'COMPLETED', 'EXPIRED', 'MANUAL_REQUIRED', 'REVOKED')),
        CONSTRAINT [CK_REFUND_CLAIM_ACCOUNT_VALIDATION_STATUS]
            CHECK ([accountValidationStatus] IN
                ('NOT_STARTED', 'VERIFIED', 'FAILED', 'UNAVAILABLE')),
        CONSTRAINT [FK_REFUND_CLAIM_REFUND]
            FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
        CONSTRAINT [FK_REFUND_CLAIM_CUSTOMER_PROFILE]
            FOREIGN KEY ([customerProfileId])
            REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId]),
        CONSTRAINT [FK_REFUND_CLAIM_BANK_DIRECTORY]
            FOREIGN KEY ([bankCode])
            REFERENCES dbo.[BANK_DIRECTORY]([bankCode])
    );
END;
GO

IF OBJECT_ID(N'dbo.REFUND_CLAIM_TOKEN', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[REFUND_CLAIM_TOKEN]
    (
        [refundClaimTokenId] NVARCHAR(50) NOT NULL,
        [refundClaimId] NVARCHAR(50) NOT NULL,
        [tokenHash] CHAR(64) NOT NULL,
        [expiresAt] DATETIME2 NOT NULL,
        [usedAt] DATETIME2 NULL,
        [revokedAt] DATETIME2 NULL,
        [createdAt] DATETIME2 NOT NULL
            CONSTRAINT [DF_REFUND_CLAIM_TOKEN_CREATED_AT] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [PK_REFUND_CLAIM_TOKEN] PRIMARY KEY ([refundClaimTokenId]),
        CONSTRAINT [UQ_REFUND_CLAIM_TOKEN_HASH] UNIQUE ([tokenHash]),
        CONSTRAINT [FK_REFUND_CLAIM_TOKEN_CLAIM]
            FOREIGN KEY ([refundClaimId])
            REFERENCES dbo.[REFUND_CLAIM]([refundClaimId])
    );
END;
GO

IF OBJECT_ID(N'dbo.CUSTOMER_REFUND_REQUEST', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[CUSTOMER_REFUND_REQUEST]
    (
        [customerRefundRequestId] NVARCHAR(50) NOT NULL,
        [refundId] NVARCHAR(50) NOT NULL,
        [customerProfileId] NVARCHAR(50) NOT NULL,
        [ticketId] NVARCHAR(50) NULL,
        [requestReason] NVARCHAR(1000) NOT NULL,
        [requestStatus] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_CUSTOMER_REFUND_REQUEST_STATUS] DEFAULT 'PENDING',
        [processedByUserId] NVARCHAR(50) NULL,
        [processedAt] DATETIME2 NULL,
        [createdAt] DATETIME2 NOT NULL
            CONSTRAINT [DF_CUSTOMER_REFUND_REQUEST_CREATED_AT]
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [PK_CUSTOMER_REFUND_REQUEST]
            PRIMARY KEY ([customerRefundRequestId]),
        CONSTRAINT [CK_CUSTOMER_REFUND_REQUEST_STATUS]
            CHECK ([requestStatus] IN ('PENDING', 'FULFILLED', 'REJECTED')),
        CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_REFUND]
            FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
        CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_CUSTOMER_PROFILE]
            FOREIGN KEY ([customerProfileId])
            REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId]),
        CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_TICKET]
            FOREIGN KEY ([ticketId]) REFERENCES dbo.[TICKET]([ticketId]),
        CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_PROCESSED_BY_USER]
            FOREIGN KEY ([processedByUserId]) REFERENCES dbo.[USER]([userId])
    );
END;
GO

IF OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MANUAL_REFUND_PROCESS]
    (
        [manualRefundProcessId] NVARCHAR(50) NOT NULL,
        [refundId] NVARCHAR(50) NOT NULL,
        [refundClaimId] NVARCHAR(50) NOT NULL,
        [assignedToUserId] NVARCHAR(50) NULL,
        [processStatus] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_MANUAL_REFUND_PROCESS_STATUS] DEFAULT 'OPEN',
        [bankTransactionCode] NVARCHAR(255) NULL,
        [transferredAmount] DECIMAL(18,2) NULL,
        [proofUrl] NVARCHAR(1000) NULL,
        [adminNote] NVARCHAR(1000) NULL,
        [assignedAt] DATETIME2 NULL,
        [confirmedAt] DATETIME2 NULL,
        [createdAt] DATETIME2 NOT NULL
            CONSTRAINT [DF_MANUAL_REFUND_PROCESS_CREATED_AT]
            DEFAULT SYSUTCDATETIME(),
        [rowVersion] ROWVERSION NOT NULL,

        CONSTRAINT [PK_MANUAL_REFUND_PROCESS]
            PRIMARY KEY ([manualRefundProcessId]),
        CONSTRAINT [UQ_MANUAL_REFUND_PROCESS_REFUND] UNIQUE ([refundId]),
        CONSTRAINT [UQ_MANUAL_REFUND_PROCESS_CLAIM] UNIQUE ([refundClaimId]),
        CONSTRAINT [CK_MANUAL_REFUND_PROCESS_STATUS]
            CHECK ([processStatus] IN
                ('OPEN', 'IN_PROGRESS', 'CONFIRMED', 'REJECTED')),
        CONSTRAINT [CK_MANUAL_REFUND_TRANSFERRED_AMOUNT]
            CHECK ([transferredAmount] IS NULL OR [transferredAmount] > 0),
        CONSTRAINT [FK_MANUAL_REFUND_PROCESS_REFUND]
            FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
        CONSTRAINT [FK_MANUAL_REFUND_PROCESS_CLAIM]
            FOREIGN KEY ([refundClaimId])
            REFERENCES dbo.[REFUND_CLAIM]([refundClaimId]),
        CONSTRAINT [FK_MANUAL_REFUND_PROCESS_ASSIGNED_USER]
            FOREIGN KEY ([assignedToUserId]) REFERENCES dbo.[USER]([userId])
    );
END;
GO

IF COL_LENGTH(N'dbo.REFUND_CLAIM', N'refundId') IS NULL
   OR COL_LENGTH(N'dbo.REFUND_CLAIM', N'claimStatus') IS NULL
   OR COL_LENGTH(N'dbo.REFUND_CLAIM', N'rowVersion') IS NULL
BEGIN
    THROW 50011, 'dbo.REFUND_CLAIM exists but does not match the required schema.', 1;
END;

IF COL_LENGTH(N'dbo.REFUND_CLAIM_TOKEN', N'tokenHash') IS NULL
   OR COL_LENGTH(N'dbo.REFUND_CLAIM_TOKEN', N'expiresAt') IS NULL
BEGIN
    THROW 50012, 'dbo.REFUND_CLAIM_TOKEN exists but does not match the required schema.', 1;
END;

IF COL_LENGTH(N'dbo.CUSTOMER_REFUND_REQUEST', N'requestStatus') IS NULL
   OR COL_LENGTH(N'dbo.CUSTOMER_REFUND_REQUEST', N'customerProfileId') IS NULL
BEGIN
    THROW 50013, 'dbo.CUSTOMER_REFUND_REQUEST exists but does not match the required schema.', 1;
END;

IF COL_LENGTH(N'dbo.MANUAL_REFUND_PROCESS', N'processStatus') IS NULL
   OR COL_LENGTH(N'dbo.MANUAL_REFUND_PROCESS', N'rowVersion') IS NULL
BEGIN
    THROW 50014, 'dbo.MANUAL_REFUND_PROCESS exists but does not match the required schema.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.REFUND_CLAIM')
      AND [name] = N'IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID'
)
    CREATE INDEX [IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID]
        ON dbo.[REFUND_CLAIM]([customerProfileId]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.REFUND_CLAIM')
      AND [name] = N'IX_REFUND_CLAIM_STATUS'
)
    CREATE INDEX [IX_REFUND_CLAIM_STATUS]
        ON dbo.[REFUND_CLAIM]([claimStatus], [expiresAt]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.REFUND_CLAIM_TOKEN')
      AND [name] = N'IX_REFUND_CLAIM_TOKEN_CLAIM'
)
    CREATE INDEX [IX_REFUND_CLAIM_TOKEN_CLAIM]
        ON dbo.[REFUND_CLAIM_TOKEN]([refundClaimId], [expiresAt]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.CUSTOMER_REFUND_REQUEST')
      AND [name] = N'IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS'
)
    CREATE INDEX [IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS]
        ON dbo.[CUSTOMER_REFUND_REQUEST]
            ([customerProfileId], [requestStatus], [createdAt]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS')
      AND [name] = N'IX_MANUAL_REFUND_PROCESS_STATUS_CREATED'
)
    CREATE INDEX [IX_MANUAL_REFUND_PROCESS_STATUS_CREATED]
        ON dbo.[MANUAL_REFUND_PROCESS]([processStatus], [createdAt]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS')
      AND [name] = N'UX_MANUAL_REFUND_BANK_TRANSACTION_CODE'
)
    CREATE UNIQUE INDEX [UX_MANUAL_REFUND_BANK_TRANSACTION_CODE]
        ON dbo.[MANUAL_REFUND_PROCESS]([bankTransactionCode])
        WHERE [bankTransactionCode] IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'VCB')
    INSERT dbo.[BANK_DIRECTORY]
        ([bankCode], [bankBin], [shortName], [fullName])
    VALUES
        ('VCB', '970436', N'Vietcombank',
         N'Joint Stock Commercial Bank for Foreign Trade of Vietnam');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'MB')
    INSERT dbo.[BANK_DIRECTORY]
        ([bankCode], [bankBin], [shortName], [fullName])
    VALUES
        ('MB', '970422', N'MB Bank',
         N'Military Commercial Joint Stock Bank');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'TCB')
    INSERT dbo.[BANK_DIRECTORY]
        ([bankCode], [bankBin], [shortName], [fullName])
    VALUES
        ('TCB', '970407', N'Techcombank',
         N'Vietnam Technological and Commercial Joint Stock Bank');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'BIDV')
    INSERT dbo.[BANK_DIRECTORY]
        ([bankCode], [bankBin], [shortName], [fullName])
    VALUES
        ('BIDV', '970418', N'BIDV',
         N'Joint Stock Commercial Bank for Investment and Development of Vietnam');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'CTG')
    INSERT dbo.[BANK_DIRECTORY]
        ([bankCode], [bankBin], [shortName], [fullName])
    VALUES
        ('CTG', '970415', N'VietinBank',
         N'Vietnam Joint Stock Commercial Bank for Industry and Trade');
GO

SELECT N'DB_PATCH_APPLIED=1' AS [verification];

SELECT CONCAT(
    N'REFUND_WORKFLOW_TABLES_FOUND=',
    COUNT(*),
    N'/5') AS [verification]
FROM sys.tables
WHERE [name] IN
(
    N'BANK_DIRECTORY',
    N'REFUND_CLAIM',
    N'REFUND_CLAIM_TOKEN',
    N'CUSTOMER_REFUND_REQUEST',
    N'MANUAL_REFUND_PROCESS'
);

SELECT CONCAT(
    N'ACTIVE_BANKS=',
    COUNT(*)) AS [verification]
FROM dbo.[BANK_DIRECTORY]
WHERE [isActive] = 1;

SELECT CONCAT(
    N'MANUAL_REFUND_PROCESS_EXISTS=',
    CASE
        WHEN OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS', N'U') IS NULL THEN 0
        ELSE 1
    END) AS [verification];
GO
