/*
CinemaBookingDB - data-preserving schema upgrade

Run this script against the existing target database, for example:
  sqlcmd -S SERVER -d CinemaBookingDB -E -b -f 65001
    -i "docs\database\cinema-booking-schema-upgrade.sql"

Safety contract:
- No DROP DATABASE, DROP TABLE, DELETE, TRUNCATE, or destructive data rewrite.
- Every schema change is guarded, so the script is safe to re-run.
- It contains every supported additive schema change and only idempotent
  reference-data seeds (for example, BANK_DIRECTORY). It does not add dev
  movies, bookings, payments, tickets, or compensation test fixtures.
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
   OR OBJECT_ID(N'dbo.[ROLE]', N'U') IS NULL
   OR OBJECT_ID(N'dbo.STAFF_PROFILE', N'U') IS NULL
   OR OBJECT_ID(N'dbo.REFUND', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CUSTOMER_PROFILE', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TICKET', N'U') IS NULL
   OR OBJECT_ID(N'dbo.PAYMENT_PROVIDER', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CINEMA', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FB_ITEM', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CINEMA_FB_INVENTORY', N'U') IS NULL
   OR OBJECT_ID(N'dbo.VOUCHER', N'U') IS NULL
   OR OBJECT_ID(N'dbo.VOUCHER_USAGE', N'U') IS NULL
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

    -- Banner module. Kept here instead of a standalone patch so an existing
    -- database receives the same schema as the canonical reset script.
    IF OBJECT_ID(N'dbo.BANNER', N'U') IS NULL
        CREATE TABLE dbo.[BANNER]
        (
            [bannerId] VARCHAR(50) NOT NULL PRIMARY KEY,
            [title] NVARCHAR(200) NOT NULL,
            [imageUrl] NVARCHAR(1000) NOT NULL,
            [linkUrl] NVARCHAR(1000) NULL,
            [bannerType] VARCHAR(50) NOT NULL,
            [displayOrder] INT NOT NULL CONSTRAINT [DF_BANNER_DISPLAY_ORDER] DEFAULT 0,
            [isActive] BIT NOT NULL CONSTRAINT [DF_BANNER_IS_ACTIVE] DEFAULT 1,
            [createdAt] DATETIME NOT NULL CONSTRAINT [DF_BANNER_CREATED_AT] DEFAULT GETDATE()
        );

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

-- SQL Server resolves column names when a batch is compiled. Add the nullable
-- column in its own harmless, rerunnable phase before compiling the backfill.
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.VOUCHER_USAGE', N'customerVoucherId') IS NULL
        ALTER TABLE dbo.[VOUCHER_USAGE]
            ADD [customerVoucherId] NVARCHAR(50) NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Link a checkout reservation to the exact claimed voucher. Existing rows are
-- backfilled only when exactly one used wallet claim is a safe match.
BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH exactClaim AS
    (
        SELECT
            usage.[voucherUsageId],
            MIN(claim.[customerVoucherId]) AS [customerVoucherId]
        FROM dbo.[VOUCHER_USAGE] AS usage
        INNER JOIN dbo.[CUSTOMER_VOUCHER] AS claim
            ON claim.[voucherId] = usage.[voucherId]
           AND claim.[customerProfileId] = usage.[customerProfileId]
           AND claim.[isUsed] = 1
        WHERE usage.[customerVoucherId] IS NULL
          AND usage.[usageStatus] IN (N'APPLIED', N'CONFIRMED')
        GROUP BY usage.[voucherUsageId]
        HAVING COUNT_BIG(*) = 1
    )
    UPDATE usage
    SET [customerVoucherId] = exactClaim.[customerVoucherId]
    FROM dbo.[VOUCHER_USAGE] AS usage
    INNER JOIN exactClaim
        ON exactClaim.[voucherUsageId] = usage.[voucherUsageId];

    -- APPLIED is a temporary reservation, not a consumed wallet claim.
    UPDATE claim
    SET [isUsed] = 0,
        [usedAt] = NULL
    FROM dbo.[CUSTOMER_VOUCHER] AS claim
    INNER JOIN dbo.[VOUCHER_USAGE] AS usage
        ON usage.[customerVoucherId] = claim.[customerVoucherId]
    WHERE usage.[usageStatus] = N'APPLIED'
      AND claim.[isUsed] = 1;

    IF EXISTS
    (
        SELECT usage.[customerVoucherId]
        FROM dbo.[VOUCHER_USAGE] AS usage
        WHERE usage.[customerVoucherId] IS NOT NULL
          AND usage.[usageStatus] <> N'CANCELLED'
        GROUP BY usage.[customerVoucherId]
        HAVING COUNT_BIG(*) > 1
    )
        THROW 52004, 'A customer voucher is linked to multiple non-cancelled usages. Reconcile those rows, then re-run.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
          AND name = N'FK_VOUCHER_USAGE_CUSTOMER_VOUCHER'
    )
        ALTER TABLE dbo.[VOUCHER_USAGE] WITH CHECK
            ADD CONSTRAINT [FK_VOUCHER_USAGE_CUSTOMER_VOUCHER]
            FOREIGN KEY ([customerVoucherId])
            REFERENCES dbo.[CUSTOMER_VOUCHER]([customerVoucherId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.VOUCHER_USAGE')
          AND name = N'UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER'
    )
        CREATE UNIQUE INDEX [UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER]
            ON dbo.[VOUCHER_USAGE]([customerVoucherId])
            WHERE [customerVoucherId] IS NOT NULL
              AND [usageStatus] <> N'CANCELLED';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Showtime-cancellation compensation policy. The booking column is added in a
-- separate batch because SQL Server resolves new column names at compile time.
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.BOOKING', N'compensationDiscountAmount') IS NULL
        ALTER TABLE dbo.[BOOKING]
            ADD [compensationDiscountAmount] DECIMAL(18,2) NOT NULL
                CONSTRAINT [DF_BOOKING_COMPENSATION_DISCOUNT_AMOUNT] DEFAULT 0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
          AND name = N'CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT'
    )
        ALTER TABLE dbo.[BOOKING] WITH CHECK
            ADD CONSTRAINT [CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT]
            CHECK ([compensationDiscountAmount] >= 0);

    IF OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION', N'U') IS NULL
        CREATE TABLE dbo.[CANCELLATION_COMPENSATION]
        (
            [cancellationCompensationId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [sourceBookingId] NVARCHAR(50) NOT NULL,
            [showtimeCancellationId] NVARCHAR(50) NOT NULL,
            [customerProfileId] NVARCHAR(50) NULL,
            [status] NVARCHAR(30) NOT NULL
                CONSTRAINT [DF_CANCELLATION_COMPENSATION_STATUS] DEFAULT 'ISSUED',
            [policyVersion] NVARCHAR(50) NOT NULL,
            [issuedAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_CANCELLATION_COMPENSATION_ISSUED_AT] DEFAULT SYSUTCDATETIME(),
            [expiresAt] DATETIME2 NOT NULL,
            CONSTRAINT [UQ_CANCELLATION_COMPENSATION_BOOKING] UNIQUE ([sourceBookingId]),
            CONSTRAINT [CK_CANCELLATION_COMPENSATION_STATUS]
                CHECK ([status] IN ('ISSUED', 'PARTIALLY_USED', 'USED', 'EXPIRED', 'VOIDED')),
            CONSTRAINT [CK_CANCELLATION_COMPENSATION_EXPIRY]
                CHECK ([expiresAt] > [issuedAt]),
            CONSTRAINT [FK_CANCELLATION_COMPENSATION_BOOKING]
                FOREIGN KEY ([sourceBookingId]) REFERENCES dbo.[BOOKING]([bookingId]),
            CONSTRAINT [FK_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION]
                FOREIGN KEY ([showtimeCancellationId]) REFERENCES dbo.[SHOWTIME_CANCELLATION]([showtimeCancellationId]),
            CONSTRAINT [FK_CANCELLATION_COMPENSATION_CUSTOMER_PROFILE]
                FOREIGN KEY ([customerProfileId]) REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId])
        );

    IF OBJECT_ID(N'dbo.COMPENSATION_TICKET', N'U') IS NULL
        CREATE TABLE dbo.[COMPENSATION_TICKET]
        (
            [compensationTicketId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [cancellationCompensationId] NVARCHAR(50) NOT NULL,
            [voucherCode] NVARCHAR(100) NOT NULL,
            [status] NVARCHAR(30) NOT NULL
                CONSTRAINT [DF_COMPENSATION_TICKET_STATUS] DEFAULT 'ISSUED',
            [reservedBookingId] NVARCHAR(50) NULL,
            [reservedBookingSeatId] NVARCHAR(50) NULL,
            [reservedAt] DATETIME2 NULL,
            [redeemedAt] DATETIME2 NULL,
            [rowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [UQ_COMPENSATION_TICKET_CODE] UNIQUE ([voucherCode]),
            CONSTRAINT [CK_COMPENSATION_TICKET_STATUS]
                CHECK ([status] IN ('ISSUED', 'RESERVED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
            CONSTRAINT [FK_COMPENSATION_TICKET_COMPENSATION]
                FOREIGN KEY ([cancellationCompensationId])
                REFERENCES dbo.[CANCELLATION_COMPENSATION]([cancellationCompensationId])
                ON DELETE CASCADE,
            CONSTRAINT [FK_COMPENSATION_TICKET_RESERVED_BOOKING]
                FOREIGN KEY ([reservedBookingId]) REFERENCES dbo.[BOOKING]([bookingId]),
            CONSTRAINT [FK_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT]
                FOREIGN KEY ([reservedBookingSeatId]) REFERENCES dbo.[BOOKING_SEAT]([bookingSeatId])
        );

    IF OBJECT_ID(N'dbo.COMPENSATION_COMBO', N'U') IS NULL
        CREATE TABLE dbo.[COMPENSATION_COMBO]
        (
            [compensationComboId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [cancellationCompensationId] NVARCHAR(50) NOT NULL,
            [voucherCode] NVARCHAR(100) NOT NULL,
            [displayName] NVARCHAR(255) NOT NULL,
            [status] NVARCHAR(30) NOT NULL
                CONSTRAINT [DF_COMPENSATION_COMBO_STATUS] DEFAULT 'ISSUED',
            [redeemedAt] DATETIME2 NULL,
            [redeemedAtCinemaId] NVARCHAR(50) NULL,
            [redeemedByStaffProfileId] NVARCHAR(50) NULL,
            [rowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [UQ_COMPENSATION_COMBO_COMPENSATION] UNIQUE ([cancellationCompensationId]),
            CONSTRAINT [UQ_COMPENSATION_COMBO_CODE] UNIQUE ([voucherCode]),
            CONSTRAINT [CK_COMPENSATION_COMBO_STATUS]
                CHECK ([status] IN ('ISSUED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
            CONSTRAINT [FK_COMPENSATION_COMBO_COMPENSATION]
                FOREIGN KEY ([cancellationCompensationId])
                REFERENCES dbo.[CANCELLATION_COMPENSATION]([cancellationCompensationId])
                ON DELETE CASCADE,
            CONSTRAINT [FK_COMPENSATION_COMBO_CINEMA]
                FOREIGN KEY ([redeemedAtCinemaId]) REFERENCES dbo.[CINEMA]([cinemaId]),
            CONSTRAINT [FK_COMPENSATION_COMBO_STAFF_PROFILE]
                FOREIGN KEY ([redeemedByStaffProfileId]) REFERENCES dbo.[STAFF_PROFILE]([staffProfileId])
        );

    IF COL_LENGTH(N'dbo.COMPENSATION_TICKET', N'rowVersion') IS NULL
        ALTER TABLE dbo.[COMPENSATION_TICKET]
            ADD [rowVersion] ROWVERSION NOT NULL;

    IF COL_LENGTH(N'dbo.COMPENSATION_COMBO', N'rowVersion') IS NULL
        ALTER TABLE dbo.[COMPENSATION_COMBO]
            ADD [rowVersion] ROWVERSION NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION')
          AND name = N'IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION'
    )
        CREATE INDEX [IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION]
            ON dbo.[CANCELLATION_COMPENSATION]([showtimeCancellationId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION')
          AND name = N'IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS'
    )
        CREATE INDEX [IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS]
            ON dbo.[CANCELLATION_COMPENSATION]([customerProfileId], [status]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
          AND name = N'IX_COMPENSATION_TICKET_COMPENSATION'
    )
        CREATE INDEX [IX_COMPENSATION_TICKET_COMPENSATION]
            ON dbo.[COMPENSATION_TICKET]([cancellationCompensationId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
          AND name = N'IX_COMPENSATION_TICKET_RESERVED_BOOKING'
    )
        CREATE INDEX [IX_COMPENSATION_TICKET_RESERVED_BOOKING]
            ON dbo.[COMPENSATION_TICKET]([reservedBookingId]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
          AND name = N'UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT'
    )
        CREATE UNIQUE INDEX [UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT]
            ON dbo.[COMPENSATION_TICKET]([reservedBookingSeatId])
            WHERE [reservedBookingSeatId] IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Role provisioning is data-driven: policies define account/profile shape and
-- assignment rules define which actor role may create each target role.
BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_CUSTOMER')
        INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
        VALUES (N'ROLE_CUSTOMER', N'CUSTOMER', N'Customer account');

    IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_STAFF')
        INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
        VALUES (N'ROLE_STAFF', N'STAFF', N'Cinema staff account');

    IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_MANAGER')
        INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
        VALUES (N'ROLE_MANAGER', N'MANAGER', N'Cinema manager account');

    IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_ADMIN')
        INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
        VALUES (N'ROLE_ADMIN', N'ADMIN', N'System administrator account');

    IF OBJECT_ID(N'dbo.ROLE_PROVISIONING_POLICY', N'U') IS NULL
        CREATE TABLE dbo.[ROLE_PROVISIONING_POLICY]
        (
            [roleId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [profileKind] NVARCHAR(20) NOT NULL,
            [requiresCinema] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_REQUIRES_CINEMA] DEFAULT 0,
            [defaultStaffPosition] NVARCHAR(100) NULL,
            [isActive] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_ACTIVE] DEFAULT 1,
            [isPublicRegistrationAllowed] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER] DEFAULT 0,
            CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PROFILE]
                CHECK ([profileKind] IN (N'CUSTOMER', N'STAFF', N'NONE')),
            CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE]
                CHECK
                (
                    ([profileKind] = N'STAFF' AND [requiresCinema] = 1 AND [defaultStaffPosition] IS NOT NULL)
                    OR ([profileKind] = N'CUSTOMER' AND [requiresCinema] = 0 AND [defaultStaffPosition] IS NULL)
                    OR ([profileKind] = N'NONE' AND [requiresCinema] = 0 AND [defaultStaffPosition] IS NULL)
                ),
            CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER]
                CHECK ([isPublicRegistrationAllowed] = 0 OR [profileKind] = N'CUSTOMER'),
            CONSTRAINT [FK_ROLE_PROVISIONING_POLICY_ROLE]
                FOREIGN KEY ([roleId]) REFERENCES dbo.[ROLE]([roleId])
        );

    IF OBJECT_ID(N'dbo.ROLE_ASSIGNMENT_RULE', N'U') IS NULL
        CREATE TABLE dbo.[ROLE_ASSIGNMENT_RULE]
        (
            [grantorRoleId] NVARCHAR(50) NOT NULL,
            [granteeRoleId] NVARCHAR(50) NOT NULL,
            [isActive] BIT NOT NULL CONSTRAINT [DF_ROLE_ASSIGNMENT_RULE_ACTIVE] DEFAULT 1,
            CONSTRAINT [PK_ROLE_ASSIGNMENT_RULE] PRIMARY KEY ([grantorRoleId], [granteeRoleId]),
            CONSTRAINT [CK_ROLE_ASSIGNMENT_RULE_DIFFERENT_ROLES]
                CHECK ([grantorRoleId] <> [granteeRoleId]),
            CONSTRAINT [FK_ROLE_ASSIGNMENT_RULE_GRANTOR]
                FOREIGN KEY ([grantorRoleId]) REFERENCES dbo.[ROLE]([roleId]),
            CONSTRAINT [FK_ROLE_ASSIGNMENT_RULE_GRANTEE]
                FOREIGN KEY ([granteeRoleId]) REFERENCES dbo.[ROLE]([roleId])
        );

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ROLE_PROVISIONING_POLICY')
          AND name = N'IX_ROLE_PROVISIONING_POLICY_PUBLIC'
    )
        CREATE INDEX [IX_ROLE_PROVISIONING_POLICY_PUBLIC]
            ON dbo.[ROLE_PROVISIONING_POLICY]([isActive], [isPublicRegistrationAllowed]);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ROLE_ASSIGNMENT_RULE')
          AND name = N'IX_ROLE_ASSIGNMENT_RULE_GRANTEE'
    )
        CREATE INDEX [IX_ROLE_ASSIGNMENT_RULE_GRANTEE]
            ON dbo.[ROLE_ASSIGNMENT_RULE]([granteeRoleId]);

    MERGE dbo.[ROLE_PROVISIONING_POLICY] AS target
    USING
    (
        VALUES
            (N'ROLE_CUSTOMER', N'CUSTOMER', CONVERT(bit, 0), CAST(NULL AS NVARCHAR(100)), CONVERT(bit, 1), CONVERT(bit, 1)),
            (N'ROLE_STAFF', N'STAFF', CONVERT(bit, 1), N'Staff', CONVERT(bit, 1), CONVERT(bit, 0)),
            (N'ROLE_MANAGER', N'STAFF', CONVERT(bit, 1), N'Manager', CONVERT(bit, 1), CONVERT(bit, 0)),
            (N'ROLE_ADMIN', N'NONE', CONVERT(bit, 0), CAST(NULL AS NVARCHAR(100)), CONVERT(bit, 1), CONVERT(bit, 0))
    ) AS source ([roleId], [profileKind], [requiresCinema], [defaultStaffPosition], [isActive], [isPublicRegistrationAllowed])
    ON target.[roleId] = source.[roleId]
    WHEN NOT MATCHED THEN INSERT
        ([roleId], [profileKind], [requiresCinema], [defaultStaffPosition], [isActive], [isPublicRegistrationAllowed])
    VALUES
        (source.[roleId], source.[profileKind], source.[requiresCinema], source.[defaultStaffPosition], source.[isActive], source.[isPublicRegistrationAllowed]);

    MERGE dbo.[ROLE_ASSIGNMENT_RULE] AS target
    USING
    (
        VALUES
            (N'ROLE_ADMIN', N'ROLE_CUSTOMER', CONVERT(bit, 1)),
            (N'ROLE_ADMIN', N'ROLE_STAFF', CONVERT(bit, 1)),
            (N'ROLE_ADMIN', N'ROLE_MANAGER', CONVERT(bit, 1))
    ) AS source ([grantorRoleId], [granteeRoleId], [isActive])
    ON target.[grantorRoleId] = source.[grantorRoleId]
       AND target.[granteeRoleId] = source.[granteeRoleId]
    WHEN NOT MATCHED THEN INSERT ([grantorRoleId], [granteeRoleId], [isActive])
    VALUES (source.[grantorRoleId], source.[granteeRoleId], source.[isActive]);

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
WHERE [name] IN (N'BANK_DIRECTORY', N'CUSTOMER_VOUCHER', N'REFUND_CLAIM', N'REFUND_CLAIM_TOKEN', N'CUSTOMER_REFUND_REQUEST', N'MANUAL_REFUND_PROCESS', N'REFUND_PAYOUT_ATTEMPT', N'EMAIL_OUTBOX', N'CANCELLATION_COMPENSATION', N'COMPENSATION_TICKET', N'COMPENSATION_COMBO', N'ROLE_PROVISIONING_POLICY', N'ROLE_ASSIGNMENT_RULE')
ORDER BY [name];
GO
