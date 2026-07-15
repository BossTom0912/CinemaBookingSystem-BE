/*
CinemaBookingDB - data-preserving schema upgrade

Run this script against the existing target database, for example:
  sqlcmd -S SERVER -d CinemaBookingDB -E -b -f 65001
    -i "docs\database\cinema-booking-schema-upgrade.sql"

Safety contract:
- No DROP DATABASE, DROP TABLE, DELETE, TRUNCATE, or destructive data rewrite.
- Every schema change is guarded, so the script is safe to re-run.
- Each upgrade phase runs in a transaction. The retry-safe checkout contract
  commits first, so an optional later migration cannot leave the running API
  without its required booking columns.
- If historic CHECKIN_LOG rows cannot be mapped to a user, that later phase
  throws and rolls back instead of guessing.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Commit the checkout contract first. This phase only depends on BOOKING and
-- remains usable even when an older database lacks optional workflow tables.
IF OBJECT_ID(N'dbo.BOOKING', N'U') IS NULL
BEGIN
    THROW 52000, 'Required table dbo.BOOKING is missing. Use the reset schema only for a new local database.', 1;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.BOOKING', N'clientRequestId') IS NULL
        ALTER TABLE dbo.[BOOKING] ADD [clientRequestId] UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH(N'dbo.BOOKING', N'requestFingerprint') IS NULL
        ALTER TABLE dbo.[BOOKING] ADD [requestFingerprint] VARCHAR(64) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.BOOKING')
          AND name = N'UX_BOOKING_CUSTOMER_CLIENT_REQUEST'
    )
        CREATE UNIQUE INDEX [UX_BOOKING_CUSTOMER_CLIENT_REQUEST]
            ON dbo.[BOOKING]([customerProfileId], [clientRequestId])
            WHERE [clientRequestId] IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Apply the remaining workflow upgrades only when their base tables exist.
IF OBJECT_ID(N'dbo.BOOKING', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CHECKIN_LOG', N'U') IS NULL
   OR OBJECT_ID(N'dbo.[USER]', N'U') IS NULL
   OR OBJECT_ID(N'dbo.STAFF_PROFILE', N'U') IS NULL
   OR OBJECT_ID(N'dbo.REFUND', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CUSTOMER_PROFILE', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TICKET', N'U') IS NULL
   OR OBJECT_ID(N'dbo.PAYMENT_PROVIDER', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CINEMA', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FB_ITEM', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CINEMA_FB_INVENTORY', N'U') IS NULL
   OR OBJECT_ID(N'dbo.VOUCHER', N'U') IS NULL
BEGIN
    THROW 52001, 'Required base tables are missing. Use the reset schema only for a new local database.', 1;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- Booking fulfillment columns.
    IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfillmentStatus') IS NULL
        ALTER TABLE dbo.[BOOKING]
            ADD [fbFulfillmentStatus] NVARCHAR(30) NOT NULL
                CONSTRAINT [DF_BOOKING_FB_FULFILLMENT_STATUS]
                DEFAULT N'NOT_REQUIRED' WITH VALUES;

    IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfilledAt') IS NULL
        ALTER TABLE dbo.[BOOKING] ADD [fbFulfilledAt] DATETIME2 NULL;

    IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfilledByStaffProfileId') IS NULL
        ALTER TABLE dbo.[BOOKING] ADD [fbFulfilledByStaffProfileId] NVARCHAR(50) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
          AND name = N'CK_BOOKING_FB_FULFILLMENT_STATUS'
    )
        ALTER TABLE dbo.[BOOKING] WITH CHECK
            ADD CONSTRAINT [CK_BOOKING_FB_FULFILLMENT_STATUS]
            CHECK ([fbFulfillmentStatus] IN
                ('NOT_REQUIRED', 'PENDING', 'PREPARING', 'FULFILLED', 'CANCELLED'));

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
          AND name = N'FK_BOOKING_FB_FULFILLED_BY_STAFF'
    )
        ALTER TABLE dbo.[BOOKING]
            ADD CONSTRAINT [FK_BOOKING_FB_FULFILLED_BY_STAFF]
            FOREIGN KEY ([fbFulfilledByStaffProfileId])
            REFERENCES dbo.[STAFF_PROFILE]([staffProfileId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.BOOKING')
          AND name = N'IX_BOOKING_FB_FULFILLMENT_STATUS'
    )
        CREATE INDEX [IX_BOOKING_FB_FULFILLMENT_STATUS]
            ON dbo.[BOOKING]([fbFulfillmentStatus]);

    -- Preserve actor attribution for ticket scans. Never invent a user ID.
    IF COL_LENGTH(N'dbo.CHECKIN_LOG', N'scannedByUserId') IS NULL
        ALTER TABLE dbo.[CHECKIN_LOG] ADD [scannedByUserId] NVARCHAR(50) NULL;

    UPDATE logEntry
    SET [scannedByUserId] = staff.[userId]
    FROM dbo.[CHECKIN_LOG] AS logEntry
    INNER JOIN dbo.[STAFF_PROFILE] AS staff
        ON staff.[staffProfileId] = logEntry.[staffProfileId]
    WHERE logEntry.[scannedByUserId] IS NULL;

    IF EXISTS (SELECT 1 FROM dbo.[CHECKIN_LOG] WHERE [scannedByUserId] IS NULL)
        THROW 52002, 'CHECKIN_LOG has rows without a resolvable user. Map them manually, then re-run.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.CHECKIN_LOG')
          AND name = N'scannedByUserId' AND is_nullable = 1
    )
        ALTER TABLE dbo.[CHECKIN_LOG]
            ALTER COLUMN [scannedByUserId] NVARCHAR(50) NOT NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.CHECKIN_LOG')
          AND name = N'staffProfileId' AND is_nullable = 0
    )
        ALTER TABLE dbo.[CHECKIN_LOG]
            ALTER COLUMN [staffProfileId] NVARCHAR(50) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.CHECKIN_LOG')
          AND name = N'FK_CHECKIN_LOG_SCANNED_BY_USER'
    )
        ALTER TABLE dbo.[CHECKIN_LOG]
            ADD CONSTRAINT [FK_CHECKIN_LOG_SCANNED_BY_USER]
            FOREIGN KEY ([scannedByUserId]) REFERENCES dbo.[USER]([userId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CHECKIN_LOG')
          AND name = N'IX_CHECKIN_LOG_SCANNED_BY_USER_TIME'
    )
        CREATE INDEX [IX_CHECKIN_LOG_SCANNED_BY_USER_TIME]
            ON dbo.[CHECKIN_LOG]([scannedByUserId], [scanTime]);

    -- Voucher wallet introduced by the customer voucher feature.
    IF OBJECT_ID(N'dbo.CUSTOMER_VOUCHER', N'U') IS NULL
        CREATE TABLE dbo.[CUSTOMER_VOUCHER]
        (
            [customerVoucherId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [customerProfileId] NVARCHAR(50) NOT NULL,
            [voucherId] NVARCHAR(50) NOT NULL,
            [claimedAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_CUSTOMER_VOUCHER_CLAIMED_AT] DEFAULT SYSUTCDATETIME(),
            [isUsed] BIT NOT NULL
                CONSTRAINT [DF_CUSTOMER_VOUCHER_IS_USED] DEFAULT 0,
            [usedAt] DATETIME2 NULL,
            CONSTRAINT [CK_CUSTOMER_VOUCHER_USAGE_STATE] CHECK
            (
                ([isUsed] = 0 AND [usedAt] IS NULL)
                OR ([isUsed] = 1 AND [usedAt] IS NOT NULL)
            ),
            CONSTRAINT [FK_CUSTOMER_VOUCHER_CUSTOMER_PROFILE]
                FOREIGN KEY ([customerProfileId])
                REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId]),
            CONSTRAINT [FK_CUSTOMER_VOUCHER_VOUCHER]
                FOREIGN KEY ([voucherId]) REFERENCES dbo.[VOUCHER]([voucherId])
        );

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CUSTOMER_VOUCHER')
          AND name = N'IX_CUSTOMER_VOUCHER_CUSTOMER_USED'
    )
        CREATE INDEX [IX_CUSTOMER_VOUCHER_CUSTOMER_USED]
            ON dbo.[CUSTOMER_VOUCHER]([customerProfileId], [isUsed]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CUSTOMER_VOUCHER')
          AND name = N'IX_CUSTOMER_VOUCHER_VOUCHER'
    )
        CREATE INDEX [IX_CUSTOMER_VOUCHER_VOUCHER]
            ON dbo.[CUSTOMER_VOUCHER]([voucherId]);

    -- Customer-assisted refund workflow.
    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.REFUND')
          AND name = N'CK_REFUND_STATUS'
          AND definition NOT LIKE N'%MANUAL_REQUIRED%'
    )
        THROW 52003, 'CK_REFUND_STATUS does not support MANUAL_REQUIRED. Use a separately reviewed constraint migration.', 1;

    IF OBJECT_ID(N'dbo.BANK_DIRECTORY', N'U') IS NULL
        CREATE TABLE dbo.[BANK_DIRECTORY]
        (
            [bankCode] NVARCHAR(20) NOT NULL PRIMARY KEY,
            [bankBin] NVARCHAR(20) NOT NULL UNIQUE,
            [shortName] NVARCHAR(100) NOT NULL,
            [fullName] NVARCHAR(255) NOT NULL,
            [isActive] BIT NOT NULL CONSTRAINT [DF_BANK_DIRECTORY_IS_ACTIVE] DEFAULT 1,
            [supportsAccountInquiry] BIT NOT NULL CONSTRAINT [DF_BANK_DIRECTORY_ACCOUNT_INQUIRY] DEFAULT 0,
            [supportsPayout] BIT NOT NULL CONSTRAINT [DF_BANK_DIRECTORY_PAYOUT] DEFAULT 0,
            [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_BANK_DIRECTORY_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            [updatedAt] DATETIME2 NULL
        );

    IF OBJECT_ID(N'dbo.REFUND_CLAIM', N'U') IS NULL
        CREATE TABLE dbo.[REFUND_CLAIM]
        (
            [refundClaimId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [refundId] NVARCHAR(50) NOT NULL UNIQUE,
            [customerProfileId] NVARCHAR(50) NOT NULL,
            [bankCode] NVARCHAR(20) NULL,
            [claimStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_REFUND_CLAIM_STATUS] DEFAULT 'PENDING_INFO',
            [accountValidationStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_REFUND_CLAIM_VALIDATION] DEFAULT 'NOT_STARTED',
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
            [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_REFUND_CLAIM_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            [updatedAt] DATETIME2 NULL,
            [rowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [CK_REFUND_CLAIM_STATUS] CHECK ([claimStatus] IN ('PENDING_INFO', 'VERIFIED', 'SUBMITTED', 'PROCESSING', 'COMPLETED', 'EXPIRED', 'MANUAL_REQUIRED', 'REVOKED')),
            CONSTRAINT [CK_REFUND_CLAIM_ACCOUNT_VALIDATION_STATUS] CHECK ([accountValidationStatus] IN ('NOT_STARTED', 'VERIFIED', 'FAILED', 'UNAVAILABLE')),
            CONSTRAINT [FK_REFUND_CLAIM_REFUND] FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
            CONSTRAINT [FK_REFUND_CLAIM_CUSTOMER_PROFILE] FOREIGN KEY ([customerProfileId]) REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId]),
            CONSTRAINT [FK_REFUND_CLAIM_BANK_DIRECTORY] FOREIGN KEY ([bankCode]) REFERENCES dbo.[BANK_DIRECTORY]([bankCode])
        );

    IF OBJECT_ID(N'dbo.REFUND_CLAIM_TOKEN', N'U') IS NULL
        CREATE TABLE dbo.[REFUND_CLAIM_TOKEN]
        (
            [refundClaimTokenId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [refundClaimId] NVARCHAR(50) NOT NULL,
            [tokenHash] CHAR(64) NOT NULL UNIQUE,
            [expiresAt] DATETIME2 NOT NULL,
            [usedAt] DATETIME2 NULL,
            [revokedAt] DATETIME2 NULL,
            [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_REFUND_CLAIM_TOKEN_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [FK_REFUND_CLAIM_TOKEN_CLAIM] FOREIGN KEY ([refundClaimId]) REFERENCES dbo.[REFUND_CLAIM]([refundClaimId])
        );

    IF OBJECT_ID(N'dbo.CUSTOMER_REFUND_REQUEST', N'U') IS NULL
        CREATE TABLE dbo.[CUSTOMER_REFUND_REQUEST]
        (
            [customerRefundRequestId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [refundId] NVARCHAR(50) NOT NULL,
            [customerProfileId] NVARCHAR(50) NOT NULL,
            [ticketId] NVARCHAR(50) NULL,
            [requestReason] NVARCHAR(1000) NOT NULL,
            [requestStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_CUSTOMER_REFUND_REQUEST_STATUS] DEFAULT 'PENDING',
            [processedByUserId] NVARCHAR(50) NULL,
            [processedAt] DATETIME2 NULL,
            [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_CUSTOMER_REFUND_REQUEST_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [CK_CUSTOMER_REFUND_REQUEST_STATUS] CHECK ([requestStatus] IN ('PENDING', 'FULFILLED', 'REJECTED')),
            CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_REFUND] FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
            CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_CUSTOMER_PROFILE] FOREIGN KEY ([customerProfileId]) REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId]),
            CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_TICKET] FOREIGN KEY ([ticketId]) REFERENCES dbo.[TICKET]([ticketId]),
            CONSTRAINT [FK_CUSTOMER_REFUND_REQUEST_PROCESSED_BY_USER] FOREIGN KEY ([processedByUserId]) REFERENCES dbo.[USER]([userId])
        );

    IF OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS', N'U') IS NULL
        CREATE TABLE dbo.[MANUAL_REFUND_PROCESS]
        (
            [manualRefundProcessId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [refundId] NVARCHAR(50) NOT NULL UNIQUE,
            [refundClaimId] NVARCHAR(50) NOT NULL UNIQUE,
            [assignedToUserId] NVARCHAR(50) NULL,
            [processStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_MANUAL_REFUND_PROCESS_STATUS] DEFAULT 'OPEN',
            [bankTransactionCode] NVARCHAR(255) NULL,
            [transferredAmount] DECIMAL(18,2) NULL,
            [proofUrl] NVARCHAR(1000) NULL,
            [adminNote] NVARCHAR(1000) NULL,
            [assignedAt] DATETIME2 NULL,
            [confirmedAt] DATETIME2 NULL,
            [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_MANUAL_REFUND_PROCESS_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            [rowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [CK_MANUAL_REFUND_PROCESS_STATUS] CHECK ([processStatus] IN ('OPEN', 'IN_PROGRESS', 'CONFIRMED', 'REJECTED')),
            CONSTRAINT [CK_MANUAL_REFUND_TRANSFERRED_AMOUNT] CHECK ([transferredAmount] IS NULL OR [transferredAmount] > 0),
            CONSTRAINT [FK_MANUAL_REFUND_PROCESS_REFUND] FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
            CONSTRAINT [FK_MANUAL_REFUND_PROCESS_CLAIM] FOREIGN KEY ([refundClaimId]) REFERENCES dbo.[REFUND_CLAIM]([refundClaimId]),
            CONSTRAINT [FK_MANUAL_REFUND_PROCESS_ASSIGNED_USER] FOREIGN KEY ([assignedToUserId]) REFERENCES dbo.[USER]([userId])
        );

    IF OBJECT_ID(N'dbo.REFUND_PAYOUT_ATTEMPT', N'U') IS NULL
        CREATE TABLE dbo.[REFUND_PAYOUT_ATTEMPT]
        (
            [refundPayoutAttemptId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [refundId] NVARCHAR(50) NOT NULL,
            [refundClaimId] NVARCHAR(50) NOT NULL,
            [paymentProviderId] NVARCHAR(50) NOT NULL,
            [idempotencyKey] NVARCHAR(100) NOT NULL UNIQUE,
            [attemptNumber] INT NOT NULL,
            [attemptStatus] NVARCHAR(30) NOT NULL DEFAULT 'CREATED',
            [providerRequestId] NVARCHAR(255) NULL,
            [providerTransactionCode] NVARCHAR(255) NULL,
            [failureReason] NVARCHAR(1000) NULL,
            [requestedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            [respondedAt] DATETIME2 NULL,
            [confirmedAt] DATETIME2 NULL,
            CONSTRAINT [UQ_REFUND_PAYOUT_ATTEMPT_NUMBER] UNIQUE ([refundId], [attemptNumber]),
            CONSTRAINT [CK_REFUND_PAYOUT_ATTEMPT_NUMBER] CHECK ([attemptNumber] > 0),
            CONSTRAINT [CK_REFUND_PAYOUT_ATTEMPT_STATUS] CHECK ([attemptStatus] IN ('CREATED', 'SUBMITTED', 'ACCEPTED', 'CONFIRMED', 'FAILED', 'UNKNOWN')),
            CONSTRAINT [FK_REFUND_PAYOUT_ATTEMPT_REFUND] FOREIGN KEY ([refundId]) REFERENCES dbo.[REFUND]([refundId]),
            CONSTRAINT [FK_REFUND_PAYOUT_ATTEMPT_CLAIM] FOREIGN KEY ([refundClaimId]) REFERENCES dbo.[REFUND_CLAIM]([refundClaimId]),
            CONSTRAINT [FK_REFUND_PAYOUT_ATTEMPT_PAYMENT_PROVIDER] FOREIGN KEY ([paymentProviderId]) REFERENCES dbo.[PAYMENT_PROVIDER]([paymentProviderId])
        );

    IF OBJECT_ID(N'dbo.EMAIL_OUTBOX', N'U') IS NULL
        CREATE TABLE dbo.[EMAIL_OUTBOX]
        (
            [emailOutboxId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [messageType] NVARCHAR(100) NOT NULL,
            [recipientEmail] NVARCHAR(255) NOT NULL,
            [relatedEntityId] NVARCHAR(50) NULL,
            [payloadEncrypted] VARBINARY(MAX) NOT NULL,
            [outboxStatus] NVARCHAR(30) NOT NULL DEFAULT 'PENDING',
            [attemptCount] INT NOT NULL DEFAULT 0,
            [nextAttemptAt] DATETIME2 NULL,
            [lastError] NVARCHAR(1000) NULL,
            [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            [sentAt] DATETIME2 NULL,
            CONSTRAINT [CK_EMAIL_OUTBOX_STATUS] CHECK ([outboxStatus] IN ('PENDING', 'PROCESSING', 'SENT', 'FAILED')),
            CONSTRAINT [CK_EMAIL_OUTBOX_ATTEMPT_COUNT] CHECK ([attemptCount] >= 0)
        );

    -- F&B and payment reference data: insert only missing rows. Existing
    -- inventory quantities are deliberately left unchanged.
    IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_POPCORN_PEPSI_L')
        INSERT dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
        VALUES ('FB_POPCORN_PEPSI_L', N'Combo bap ngot va Pepsi lon', 75000.00, 'AVAILABLE');

    IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_CHEESE_POPCORN_M')
        INSERT dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
        VALUES ('FB_CHEESE_POPCORN_M', N'Bap pho mai co vua', 55000.00, 'AVAILABLE');

    INSERT INTO dbo.[CINEMA_FB_INVENTORY] ([cinemaInventoryId], [cinemaId], [fbItemId], [quantity])
    SELECT
        CONCAT('CFI_', cinema.[cinemaId], '_', item.[fbItemId]),
        cinema.[cinemaId],
        item.[fbItemId],
        500
    FROM dbo.[CINEMA] AS cinema
    CROSS JOIN dbo.[FB_ITEM] AS item
    WHERE cinema.[cinemaStatus] = 'ACTIVE'
      AND item.[fbItemId] IN ('FB_POPCORN_PEPSI_L', 'FB_CHEESE_POPCORN_M')
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.[CINEMA_FB_INVENTORY] AS existing
          WHERE existing.[cinemaId] = cinema.[cinemaId]
            AND existing.[fbItemId] = item.[fbItemId]
      );

    IF NOT EXISTS (SELECT 1 FROM dbo.[PAYMENT_PROVIDER] WHERE [paymentProviderId] = 'PP_SEPAY')
        INSERT dbo.[PAYMENT_PROVIDER] ([paymentProviderId], [providerName], [apiEndpoint], [providerStatus])
        VALUES ('PP_SEPAY', 'SEPAY', 'https://my.sepay.vn', 'ACTIVE');

    IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'VCB')
        INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName]) VALUES ('VCB', '970436', N'Vietcombank', N'Joint Stock Commercial Bank for Foreign Trade of Vietnam');
    IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'MB')
        INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName]) VALUES ('MB', '970422', N'MB Bank', N'Military Commercial Joint Stock Bank');
    IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'TCB')
        INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName]) VALUES ('TCB', '970407', N'Techcombank', N'Vietnam Technological and Commercial Joint Stock Bank');
    IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'BIDV')
        INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName]) VALUES ('BIDV', '970418', N'BIDV', N'Joint Stock Commercial Bank for Investment and Development of Vietnam');
    IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'CTG')
        INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName]) VALUES ('CTG', '970415', N'VietinBank', N'Vietnam Joint Stock Commercial Bank for Industry and Trade');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REFUND_CLAIM') AND name = N'IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID')
        CREATE INDEX [IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID] ON dbo.[REFUND_CLAIM]([customerProfileId]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REFUND_CLAIM') AND name = N'IX_REFUND_CLAIM_STATUS')
        CREATE INDEX [IX_REFUND_CLAIM_STATUS] ON dbo.[REFUND_CLAIM]([claimStatus], [expiresAt]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REFUND_CLAIM_TOKEN') AND name = N'IX_REFUND_CLAIM_TOKEN_CLAIM')
        CREATE INDEX [IX_REFUND_CLAIM_TOKEN_CLAIM] ON dbo.[REFUND_CLAIM_TOKEN]([refundClaimId], [expiresAt]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CUSTOMER_REFUND_REQUEST') AND name = N'IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS')
        CREATE INDEX [IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS] ON dbo.[CUSTOMER_REFUND_REQUEST]([customerProfileId], [requestStatus], [createdAt]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS') AND name = N'IX_MANUAL_REFUND_PROCESS_STATUS_CREATED')
        CREATE INDEX [IX_MANUAL_REFUND_PROCESS_STATUS_CREATED] ON dbo.[MANUAL_REFUND_PROCESS]([processStatus], [createdAt]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MANUAL_REFUND_PROCESS') AND name = N'UX_MANUAL_REFUND_BANK_TRANSACTION_CODE')
        CREATE UNIQUE INDEX [UX_MANUAL_REFUND_BANK_TRANSACTION_CODE] ON dbo.[MANUAL_REFUND_PROCESS]([bankTransactionCode]) WHERE [bankTransactionCode] IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REFUND_PAYOUT_ATTEMPT') AND name = N'IX_REFUND_PAYOUT_ATTEMPT_REFUND_STATUS')
        CREATE INDEX [IX_REFUND_PAYOUT_ATTEMPT_REFUND_STATUS] ON dbo.[REFUND_PAYOUT_ATTEMPT]([refundId], [attemptStatus], [requestedAt]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EMAIL_OUTBOX') AND name = N'IX_EMAIL_OUTBOX_STATUS_NEXT_ATTEMPT')
        CREATE INDEX [IX_EMAIL_OUTBOX_STATUS_NEXT_ATTEMPT] ON dbo.[EMAIL_OUTBOX]([outboxStatus], [nextAttemptAt], [createdAt]);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT N'DB_UPGRADE_APPLIED=1' AS [verification];
SELECT [name] AS [tableName]
FROM sys.tables
WHERE [name] IN (N'BANK_DIRECTORY', N'CUSTOMER_VOUCHER', N'REFUND_CLAIM', N'REFUND_CLAIM_TOKEN', N'CUSTOMER_REFUND_REQUEST', N'MANUAL_REFUND_PROCESS', N'REFUND_PAYOUT_ATTEMPT', N'EMAIL_OUTBOX')
ORDER BY [name];
GO
