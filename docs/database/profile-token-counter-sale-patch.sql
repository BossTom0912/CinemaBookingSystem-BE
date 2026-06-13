/*
============================================================
CinemaBookingDB local patch - profile/token/counter-sale/audit
============================================================
Use this script when your local CinemaBookingDB already exists and you do not
want to run the full reset script in DB_CinemaBookingDB.txt.

What this patch adds:
1. Customer/Staff profile fields from Movie-Theater_SRS_v1.2:
   gender, identity card, address, avatar, and Staff date of birth.
2. OTP purpose and attempt count:
   EMAIL_VERIFICATION_TOKEN can safely store email verification OTP and password
   reset OTP without mixing the two flows.
3. Refresh token hash column:
   REFRESH_TOKEN.token is renamed to tokenHash. Existing local values remain as-is,
   so for a real environment revoke/delete existing refresh tokens and issue new
   hashed tokens from backend code.
4. Counter-sale support:
   BOOKING can represent ONLINE booking by Customer or COUNTER sale created by Staff.
5. Showtime cancellation actor:
   cancellation records point to USER, with optional STAFF_PROFILE when applicable.
6. Voucher/payment audit fields:
   enough data to support promotion-style display, callback reconciliation, and
   refund/payment investigation.
7. AuditLog request metadata:
   optional IP, user agent, correlation id, and nullable userId for background jobs.
8. Ticket QR key length fix:
   qrCode is limited to NVARCHAR(450) so its UNIQUE constraint is valid on SQL Server.

Run order:
- Run this after the original DB_CinemaBookingDB.txt has already created the DB.
- Review existing data before running on anything other than local/dev.
============================================================
*/

USE [CinemaBookingDB];
GO

-- =========================
-- 1. VOUCHER / PROMOTION
-- =========================

IF COL_LENGTH('dbo.VOUCHER', 'title') IS NULL
    ALTER TABLE [VOUCHER] ADD [title] NVARCHAR(255) NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'description') IS NULL
    ALTER TABLE [VOUCHER] ADD [description] NVARCHAR(1000) NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'imageUrl') IS NULL
    ALTER TABLE [VOUCHER] ADD [imageUrl] NVARCHAR(1000) NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'minOrderAmount') IS NULL
    ALTER TABLE [VOUCHER] ADD [minOrderAmount] DECIMAL(18,2) NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'maxDiscountAmount') IS NULL
    ALTER TABLE [VOUCHER] ADD [maxDiscountAmount] DECIMAL(18,2) NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'perCustomerLimit') IS NULL
    ALTER TABLE [VOUCHER] ADD [perCustomerLimit] INT NULL;
GO

IF COL_LENGTH('dbo.VOUCHER', 'usedCount') IS NULL
    ALTER TABLE [VOUCHER] ADD [usedCount] INT NOT NULL CONSTRAINT [DF_VOUCHER_USED_COUNT] DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_VOUCHER_MIN_ORDER_AMOUNT')
    ALTER TABLE [VOUCHER] ADD CONSTRAINT [CK_VOUCHER_MIN_ORDER_AMOUNT]
        CHECK ([minOrderAmount] IS NULL OR [minOrderAmount] >= 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_VOUCHER_MAX_DISCOUNT_AMOUNT')
    ALTER TABLE [VOUCHER] ADD CONSTRAINT [CK_VOUCHER_MAX_DISCOUNT_AMOUNT]
        CHECK ([maxDiscountAmount] IS NULL OR [maxDiscountAmount] > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_VOUCHER_PER_CUSTOMER_LIMIT')
    ALTER TABLE [VOUCHER] ADD CONSTRAINT [CK_VOUCHER_PER_CUSTOMER_LIMIT]
        CHECK ([perCustomerLimit] IS NULL OR [perCustomerLimit] > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_VOUCHER_USED_COUNT')
    ALTER TABLE [VOUCHER] ADD CONSTRAINT [CK_VOUCHER_USED_COUNT]
        CHECK ([usedCount] >= 0);
GO

-- =========================
-- 2. OTP TOKEN PURPOSE
-- =========================

IF COL_LENGTH('dbo.EMAIL_VERIFICATION_TOKEN', 'purpose') IS NULL
    ALTER TABLE [EMAIL_VERIFICATION_TOKEN] ADD [purpose] NVARCHAR(30) NOT NULL
        CONSTRAINT [DF_EMAIL_VERIFICATION_TOKEN_PURPOSE] DEFAULT 'EMAIL_VERIFICATION';
GO

IF COL_LENGTH('dbo.EMAIL_VERIFICATION_TOKEN', 'attemptCount') IS NULL
    ALTER TABLE [EMAIL_VERIFICATION_TOKEN] ADD [attemptCount] INT NOT NULL
        CONSTRAINT [DF_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT] DEFAULT 0;
GO

-- Drop and recreate the purpose constraint to ensure it includes EMAIL_UPDATE and PHONE_UPDATE
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_EMAIL_VERIFICATION_TOKEN_PURPOSE')
    ALTER TABLE [EMAIL_VERIFICATION_TOKEN] DROP CONSTRAINT [CK_EMAIL_VERIFICATION_TOKEN_PURPOSE];
GO

ALTER TABLE [EMAIL_VERIFICATION_TOKEN] ADD CONSTRAINT [CK_EMAIL_VERIFICATION_TOKEN_PURPOSE]
    CHECK ([purpose] IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET', 'EMAIL_UPDATE', 'PHONE_UPDATE', 'REGISTER', 'FORGOT_PASSWORD', 'CHANGE_EMAIL', 'UPDATE_EMAIL'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT')
    ALTER TABLE [EMAIL_VERIFICATION_TOKEN] ADD CONSTRAINT [CK_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT]
        CHECK ([attemptCount] >= 0);
GO

-- =========================
-- 3. REFRESH TOKEN HASH
-- =========================

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_REFRESH_TOKEN')
    ALTER TABLE [REFRESH_TOKEN] DROP CONSTRAINT [UQ_REFRESH_TOKEN];
GO

IF COL_LENGTH('dbo.REFRESH_TOKEN', 'tokenHash') IS NULL
   AND COL_LENGTH('dbo.REFRESH_TOKEN', 'token') IS NOT NULL
    EXEC sp_rename 'REFRESH_TOKEN.token', 'tokenHash', 'COLUMN';
GO

IF COL_LENGTH('dbo.REFRESH_TOKEN', 'tokenHash') IS NOT NULL
    ALTER TABLE [REFRESH_TOKEN] ALTER COLUMN [tokenHash] NVARCHAR(450) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_REFRESH_TOKEN_HASH')
   AND COL_LENGTH('dbo.REFRESH_TOKEN', 'tokenHash') IS NOT NULL
    ALTER TABLE [REFRESH_TOKEN] ADD CONSTRAINT [UQ_REFRESH_TOKEN_HASH] UNIQUE ([tokenHash]);
GO

-- Recommended for local/dev after switching backend code to hashed refresh tokens:
-- UPDATE [REFRESH_TOKEN] SET [isRevoked] = 1, [revokedAt] = SYSUTCDATETIME() WHERE [isRevoked] = 0;

-- =========================
-- 4. CUSTOMER / STAFF PROFILE
-- =========================

IF COL_LENGTH('dbo.CUSTOMER_PROFILE', 'gender') IS NULL
    ALTER TABLE [CUSTOMER_PROFILE] ADD [gender] NVARCHAR(20) NULL;
GO

IF COL_LENGTH('dbo.CUSTOMER_PROFILE', 'identityCard') IS NULL
    ALTER TABLE [CUSTOMER_PROFILE] ADD [identityCard] NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.CUSTOMER_PROFILE', 'address') IS NULL
    ALTER TABLE [CUSTOMER_PROFILE] ADD [address] NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.CUSTOMER_PROFILE', 'avatarUrl') IS NULL
    ALTER TABLE [CUSTOMER_PROFILE] ADD [avatarUrl] NVARCHAR(1000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CUSTOMER_PROFILE_IDENTITY_CARD')
    CREATE UNIQUE INDEX [UX_CUSTOMER_PROFILE_IDENTITY_CARD]
    ON [CUSTOMER_PROFILE]([identityCard])
    WHERE [identityCard] IS NOT NULL;
GO

IF COL_LENGTH('dbo.STAFF_PROFILE', 'dateOfBirth') IS NULL
    ALTER TABLE [STAFF_PROFILE] ADD [dateOfBirth] DATE NULL;
GO

IF COL_LENGTH('dbo.STAFF_PROFILE', 'gender') IS NULL
    ALTER TABLE [STAFF_PROFILE] ADD [gender] NVARCHAR(20) NULL;
GO

IF COL_LENGTH('dbo.STAFF_PROFILE', 'identityCard') IS NULL
    ALTER TABLE [STAFF_PROFILE] ADD [identityCard] NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.STAFF_PROFILE', 'address') IS NULL
    ALTER TABLE [STAFF_PROFILE] ADD [address] NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.STAFF_PROFILE', 'avatarUrl') IS NULL
    ALTER TABLE [STAFF_PROFILE] ADD [avatarUrl] NVARCHAR(1000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_STAFF_PROFILE_IDENTITY_CARD')
    CREATE UNIQUE INDEX [UX_STAFF_PROFILE_IDENTITY_CARD]
    ON [STAFF_PROFILE]([identityCard])
    WHERE [identityCard] IS NOT NULL;
GO

-- =========================
-- 5. COUNTER-SALE BOOKING
-- =========================

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BOOKING_CUSTOMER_PROFILE')
    ALTER TABLE [BOOKING] DROP CONSTRAINT [FK_BOOKING_CUSTOMER_PROFILE];
GO

IF COL_LENGTH('dbo.BOOKING', 'customerProfileId') IS NOT NULL
    ALTER TABLE [BOOKING] ALTER COLUMN [customerProfileId] NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BOOKING_CUSTOMER_PROFILE')
    ALTER TABLE [BOOKING] ADD CONSTRAINT [FK_BOOKING_CUSTOMER_PROFILE]
        FOREIGN KEY ([customerProfileId]) REFERENCES [CUSTOMER_PROFILE]([customerProfileId]);
GO

IF COL_LENGTH('dbo.BOOKING', 'createdByStaffProfileId') IS NULL
    ALTER TABLE [BOOKING] ADD [createdByStaffProfileId] NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.BOOKING', 'bookingChannel') IS NULL
    ALTER TABLE [BOOKING] ADD [bookingChannel] NVARCHAR(30) NOT NULL
        CONSTRAINT [DF_BOOKING_CHANNEL] DEFAULT 'ONLINE';
GO

IF COL_LENGTH('dbo.BOOKING', 'guestName') IS NULL
    ALTER TABLE [BOOKING] ADD [guestName] NVARCHAR(255) NULL;
GO

IF COL_LENGTH('dbo.BOOKING', 'guestPhone') IS NULL
    ALTER TABLE [BOOKING] ADD [guestPhone] NVARCHAR(30) NULL;
GO

IF COL_LENGTH('dbo.BOOKING', 'guestEmail') IS NULL
    ALTER TABLE [BOOKING] ADD [guestEmail] NVARCHAR(255) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_BOOKING_CHANNEL')
    ALTER TABLE [BOOKING] ADD CONSTRAINT [CK_BOOKING_CHANNEL]
        CHECK ([bookingChannel] IN ('ONLINE', 'COUNTER'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_BOOKING_ONLINE_CUSTOMER_REQUIRED')
    ALTER TABLE [BOOKING] ADD CONSTRAINT [CK_BOOKING_ONLINE_CUSTOMER_REQUIRED]
        CHECK ([bookingChannel] <> 'ONLINE' OR [customerProfileId] IS NOT NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BOOKING_CREATED_BY_STAFF')
    ALTER TABLE [BOOKING] ADD CONSTRAINT [FK_BOOKING_CREATED_BY_STAFF]
        FOREIGN KEY ([createdByStaffProfileId]) REFERENCES [STAFF_PROFILE]([staffProfileId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BOOKING_CREATED_BY_STAFF_PROFILE_ID')
    CREATE INDEX [IX_BOOKING_CREATED_BY_STAFF_PROFILE_ID] ON [BOOKING]([createdByStaffProfileId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BOOKING_CHANNEL')
    CREATE INDEX [IX_BOOKING_CHANNEL] ON [BOOKING]([bookingChannel]);
GO

-- =========================
-- 6. PAYMENT AUDIT
-- =========================

IF COL_LENGTH('dbo.PAYMENT', 'paymentMethod') IS NULL
    ALTER TABLE [PAYMENT] ADD [paymentMethod] NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.PAYMENT', 'providerTransactionCode') IS NULL
    ALTER TABLE [PAYMENT] ADD [providerTransactionCode] NVARCHAR(255) NULL;
GO

IF COL_LENGTH('dbo.PAYMENT', 'failureReason') IS NULL
    ALTER TABLE [PAYMENT] ADD [failureReason] NVARCHAR(1000) NULL;
GO

IF COL_LENGTH('dbo.PAYMENT', 'rawCallbackPayload') IS NULL
    ALTER TABLE [PAYMENT] ADD [rawCallbackPayload] NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH('dbo.PAYMENT', 'updatedAt') IS NULL
    ALTER TABLE [PAYMENT] ADD [updatedAt] DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PAYMENT_PROVIDER_TRANSACTION_CODE')
    CREATE UNIQUE INDEX [UX_PAYMENT_PROVIDER_TRANSACTION_CODE]
    ON [PAYMENT]([providerTransactionCode])
    WHERE [providerTransactionCode] IS NOT NULL;
GO

-- =========================
-- 7. SHOWTIME CANCELLATION ACTOR
-- =========================

IF COL_LENGTH('dbo.SHOWTIME_CANCELLATION', 'cancelledByUserId') IS NULL
    ALTER TABLE [SHOWTIME_CANCELLATION] ADD [cancelledByUserId] NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.SHOWTIME_CANCELLATION', 'cancelledByStaffId') IS NOT NULL
BEGIN
    UPDATE sc
    SET [cancelledByUserId] = sp.[userId]
    FROM [SHOWTIME_CANCELLATION] sc
    INNER JOIN [STAFF_PROFILE] sp ON sp.[staffProfileId] = sc.[cancelledByStaffId]
    WHERE sc.[cancelledByUserId] IS NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SHOWTIME_CANCELLATION')
      AND name = 'cancelledByUserId'
      AND is_nullable = 1
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SHOWTIME_CANCELLATION] WHERE [cancelledByUserId] IS NULL)
        ALTER TABLE [SHOWTIME_CANCELLATION] ALTER COLUMN [cancelledByUserId] NVARCHAR(50) NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SHOWTIME_CANCELLATION_USER')
    ALTER TABLE [SHOWTIME_CANCELLATION] ADD CONSTRAINT [FK_SHOWTIME_CANCELLATION_USER]
        FOREIGN KEY ([cancelledByUserId]) REFERENCES [USER]([userId]);
GO

-- After cancelledByUserId is populated, Staff profile becomes optional.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SHOWTIME_CANCELLATION_STAFF_PROFILE')
    ALTER TABLE [SHOWTIME_CANCELLATION] DROP CONSTRAINT [FK_SHOWTIME_CANCELLATION_STAFF_PROFILE];
GO

IF COL_LENGTH('dbo.SHOWTIME_CANCELLATION', 'cancelledByStaffId') IS NOT NULL
    ALTER TABLE [SHOWTIME_CANCELLATION] ALTER COLUMN [cancelledByStaffId] NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SHOWTIME_CANCELLATION_STAFF_PROFILE')
    ALTER TABLE [SHOWTIME_CANCELLATION] ADD CONSTRAINT [FK_SHOWTIME_CANCELLATION_STAFF_PROFILE]
        FOREIGN KEY ([cancelledByStaffId]) REFERENCES [STAFF_PROFILE]([staffProfileId]);
GO

-- =========================
-- 8. VOUCHER USAGE SNAPSHOT
-- =========================

IF COL_LENGTH('dbo.VOUCHER_USAGE', 'discountAmount') IS NULL
    ALTER TABLE [VOUCHER_USAGE] ADD [discountAmount] DECIMAL(18,2) NOT NULL
        CONSTRAINT [DF_VOUCHER_USAGE_DISCOUNT_AMOUNT] DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_VOUCHER_USAGE_DISCOUNT_AMOUNT')
    ALTER TABLE [VOUCHER_USAGE] ADD CONSTRAINT [CK_VOUCHER_USAGE_DISCOUNT_AMOUNT]
        CHECK ([discountAmount] >= 0);
GO

-- =========================
-- 9. TICKET QR UNIQUE KEY LENGTH
-- =========================

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_TICKET_QR_CODE')
    ALTER TABLE [TICKET] DROP CONSTRAINT [UQ_TICKET_QR_CODE];
GO

IF COL_LENGTH('dbo.TICKET', 'qrCode') IS NOT NULL
    ALTER TABLE [TICKET] ALTER COLUMN [qrCode] NVARCHAR(450) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_TICKET_QR_CODE')
    ALTER TABLE [TICKET] ADD CONSTRAINT [UQ_TICKET_QR_CODE] UNIQUE ([qrCode]);
GO

-- =========================
-- 10. AUDIT LOG REQUEST METADATA
-- =========================

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AUDIT_LOG_USER')
    ALTER TABLE [AUDIT_LOG] DROP CONSTRAINT [FK_AUDIT_LOG_USER];
GO

IF COL_LENGTH('dbo.AUDIT_LOG', 'userId') IS NOT NULL
    ALTER TABLE [AUDIT_LOG] ALTER COLUMN [userId] NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AUDIT_LOG_USER')
    ALTER TABLE [AUDIT_LOG] ADD CONSTRAINT [FK_AUDIT_LOG_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId]);
GO

IF COL_LENGTH('dbo.AUDIT_LOG', 'ipAddress') IS NULL
    ALTER TABLE [AUDIT_LOG] ADD [ipAddress] NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.AUDIT_LOG', 'userAgent') IS NULL
    ALTER TABLE [AUDIT_LOG] ADD [userAgent] NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.AUDIT_LOG', 'correlationId') IS NULL
    ALTER TABLE [AUDIT_LOG] ADD [correlationId] NVARCHAR(100) NULL;
GO

PRINT 'CinemaBookingDB local patch completed.';
GO
