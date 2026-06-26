# Cinema Booking Database Schema  
  
`sql  
/*
============================================================
CinemaBookingDB - FIXED VERSION FOR SQL SERVER
============================================================
Main fixes applied from the original DB.txt:
1. Replaced FLOAT money fields with DECIMAL(18,2).
2. Replaced NVARCHAR date/time fields with DATE or DATETIME2.
3. Added NOT NULL for mandatory foreign keys and core business fields.
4. Added UNIQUE constraints to prevent duplicate email, duplicate seat, duplicate ticket, duplicate voucher usage, etc.
5. Added CHECK constraints for enum/status fields.
6. Added BIT fields for boolean values such as emailVerified and isRead.
7. Added EMAIL_VERIFICATION_TOKEN for register + email verification in Sprint 1.
8. Added REFRESH_TOKEN for logout/session revocation support.
9. Added filtered indexes for one successful payment per booking and unique nullable values.
10. Added indexes for common FK/search columns.
11. Added customer/staff profile fields required by the legacy Movie-Theater SRS
    while keeping USER focused on authentication data.
12. Added token purpose/attempt count so email verification OTP and password reset
    OTP can share one token table without mixing flows.
13. Store refresh token hashes instead of raw refresh tokens.
14. Added counter-sale support for Staff selling tickets at the cinema counter.
15. Added cancellation actor by USER so Manager/Admin cancellation does not require
    every admin account to also have a STAFF_PROFILE row.
16. Added voucher/promotion campaign metadata and payment audit fields for provider
    reconciliation, callbacks, and refund investigation.

Note:
- This reset script drops CinemaBookingDB if it already exists, then recreates it from zero.
  Use this while designing the database. Do not use it on production data unless you have a backup.
- Showtime overlap validation still needs to be handled in backend service/transaction because
  SQL Server CHECK/UNIQUE constraints cannot fully prevent time-range overlap by themselves.
============================================================
*/

USE [master];
GO

IF DB_ID(N'CinemaBookingDB') IS NOT NULL
BEGIN
    ALTER DATABASE [CinemaBookingDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [CinemaBookingDB];
END
GO

CREATE DATABASE [CinemaBookingDB];
GO

USE [CinemaBookingDB];
GO

-- =========================
-- 1. BASE TABLES
-- =========================

CREATE TABLE [ROLE] (
    [roleId] NVARCHAR(50) PRIMARY KEY,
    [roleName] NVARCHAR(100) NOT NULL,
    [description] NVARCHAR(500) NULL,

    CONSTRAINT [UQ_ROLE_ROLE_NAME] UNIQUE ([roleName])
);
GO

CREATE TABLE [CINEMA] (
    [cinemaId] NVARCHAR(50) PRIMARY KEY,
    [cinemaName] NVARCHAR(255) NOT NULL,
    [address] NVARCHAR(500) NOT NULL,
    [city] NVARCHAR(100) NOT NULL,
    [phoneNumber] NVARCHAR(30) NULL,
    [cinemaStatus] NVARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT [CK_CINEMA_STATUS]
        CHECK ([cinemaStatus] IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE'))
);
GO

CREATE TABLE [SEAT_TYPE] (
    [seatTypeId] NVARCHAR(50) PRIMARY KEY,
    [typeName] NVARCHAR(100) NOT NULL,
    [extraFee] DECIMAL(18,2) NOT NULL DEFAULT 0,

    CONSTRAINT [UQ_SEAT_TYPE_NAME] UNIQUE ([typeName]),
    CONSTRAINT [CK_SEAT_TYPE_EXTRA_FEE] CHECK ([extraFee] >= 0)
);
GO

CREATE TABLE [MOVIE] (
    [movieId] NVARCHAR(50) PRIMARY KEY,
    [title] NVARCHAR(255) NOT NULL,
    [durationMinutes] INT NOT NULL,
    [genre] NVARCHAR(255) NULL,
    [language] NVARCHAR(100) NULL,
    [releaseDate] DATE NULL,
    [ageRating] NVARCHAR(30) NULL,
    [description] NVARCHAR(MAX) NULL,
    [posterUrl] NVARCHAR(1000) NULL,
    [trailerUrl] NVARCHAR(1000) NULL,
    [highlight] NVARCHAR(30) NULL,
    [viewCount] INT NOT NULL DEFAULT 0,
    [movieStatus] NVARCHAR(30) NOT NULL DEFAULT 'COMING_SOON',
    [viewCount] INT NOT NULL DEFAULT 0,
    [averageRating] DECIMAL(3,2) NOT NULL DEFAULT 0.00,
    [totalReviews] INT NOT NULL DEFAULT 0,
    [totalViews] INT NOT NULL DEFAULT 0,
    [dailyViews] INT NOT NULL DEFAULT 0,

    CONSTRAINT [CK_MOVIE_DURATION] CHECK ([durationMinutes] > 0),
    CONSTRAINT [CK_MOVIE_HIGHLIGHT] CHECK ([highlight] IS NULL OR [highlight] IN ('HOT', 'NEW', 'TRENDING')),
    CONSTRAINT [CK_MOVIE_STATUS]
        CHECK ([movieStatus] IN ('COMING_SOON', 'NOW_SHOWING', 'ENDED', 'INACTIVE', 'ARCHIVED'))
);
GO

CREATE TABLE [PAYMENT_PROVIDER] (
    [paymentProviderId] NVARCHAR(50) PRIMARY KEY,
    [providerName] NVARCHAR(100) NOT NULL,
    [apiEndpoint] NVARCHAR(1000) NULL,
    [providerStatus] NVARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT [UQ_PAYMENT_PROVIDER_NAME] UNIQUE ([providerName]),
    CONSTRAINT [CK_PAYMENT_PROVIDER_STATUS]
        CHECK ([providerStatus] IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE'))
);
GO

CREATE TABLE [VOUCHER] (
    [voucherId] NVARCHAR(50) PRIMARY KEY,
    [voucherCode] NVARCHAR(100) NOT NULL,
    [title] NVARCHAR(255) NULL,
    [description] NVARCHAR(1000) NULL,
    [imageUrl] NVARCHAR(1000) NULL,
    [discountType] NVARCHAR(30) NOT NULL,
    [discountValue] DECIMAL(18,2) NOT NULL,
    [minOrderAmount] DECIMAL(18,2) NULL,
    [maxDiscountAmount] DECIMAL(18,2) NULL,
    [usageLimit] INT NOT NULL,
    [perCustomerLimit] INT NULL,
    [usedCount] INT NOT NULL DEFAULT 0,
    [startDate] DATETIME2 NOT NULL,
    [endDate] DATETIME2 NOT NULL,
    [voucherStatus] NVARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT [UQ_VOUCHER_CODE] UNIQUE ([voucherCode]),
    CONSTRAINT [CK_VOUCHER_DISCOUNT_TYPE]
        CHECK ([discountType] IN ('AMOUNT', 'PERCENT')),
    CONSTRAINT [CK_VOUCHER_DISCOUNT_VALUE] CHECK ([discountValue] > 0),
    CONSTRAINT [CK_VOUCHER_MIN_ORDER_AMOUNT]
        CHECK ([minOrderAmount] IS NULL OR [minOrderAmount] >= 0),
    CONSTRAINT [CK_VOUCHER_MAX_DISCOUNT_AMOUNT]
        CHECK ([maxDiscountAmount] IS NULL OR [maxDiscountAmount] > 0),
    CONSTRAINT [CK_VOUCHER_USAGE_LIMIT] CHECK ([usageLimit] >= 0),
    CONSTRAINT [CK_VOUCHER_PER_CUSTOMER_LIMIT]
        CHECK ([perCustomerLimit] IS NULL OR [perCustomerLimit] > 0),
    CONSTRAINT [CK_VOUCHER_USED_COUNT] CHECK ([usedCount] >= 0),
    CONSTRAINT [CK_VOUCHER_DATE_RANGE] CHECK ([endDate] > [startDate]),
    CONSTRAINT [CK_VOUCHER_STATUS]
        CHECK ([voucherStatus] IN ('ACTIVE', 'INACTIVE', 'EXPIRED'))
);
GO

CREATE TABLE [FB_ITEM] (
    [fbItemId] NVARCHAR(50) PRIMARY KEY,
    [itemName] NVARCHAR(255) NOT NULL,
    [price] DECIMAL(18,2) NOT NULL,
    [itemStatus] NVARCHAR(30) NOT NULL DEFAULT 'AVAILABLE',

    CONSTRAINT [CK_FB_ITEM_PRICE] CHECK ([price] >= 0),
    CONSTRAINT [CK_FB_ITEM_STATUS]
        CHECK ([itemStatus] IN ('AVAILABLE', 'UNAVAILABLE', 'INACTIVE'))
);
GO

-- =========================
-- 2. USER / AUTH / PROFILE
-- =========================

CREATE TABLE [USER] (
    [userId] NVARCHAR(50) PRIMARY KEY,
    [roleId] NVARCHAR(50) NOT NULL,
    [email] NVARCHAR(255) NOT NULL,
    [passwordHash] NVARCHAR(500) NOT NULL,
    [fullName] NVARCHAR(255) NOT NULL,
    [phoneNumber] NVARCHAR(30) NULL,
    [status] NVARCHAR(30) NOT NULL DEFAULT 'PENDING_VERIFICATION',
    [emailVerified] BIT NOT NULL DEFAULT 0,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NULL,
    [spamViolationCount] INT NOT NULL DEFAULT 0,
    [isBlocked] BIT NOT NULL DEFAULT 0,
    [blockedUntil] DATETIME2 NULL,

    CONSTRAINT [UQ_USER_EMAIL] UNIQUE ([email]),
    CONSTRAINT [CK_USER_STATUS]
        CHECK ([status] IN ('PENDING_VERIFICATION', 'ACTIVE', 'INACTIVE', 'BANNED')),
    CONSTRAINT [FK_USER_ROLE]
        FOREIGN KEY ([roleId]) REFERENCES [ROLE]([roleId])
);
GO

CREATE TABLE [EMAIL_VERIFICATION_TOKEN] (
    [tokenId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NOT NULL,
    [token] NVARCHAR(255) NOT NULL,
    [purpose] NVARCHAR(30) NOT NULL DEFAULT 'EMAIL_VERIFICATION',
    [attemptCount] INT NOT NULL DEFAULT 0,
    [expiredAt] DATETIME2 NOT NULL,
    [verifiedAt] DATETIME2 NULL,
    [isUsed] BIT NOT NULL DEFAULT 0,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [UQ_EMAIL_VERIFICATION_TOKEN] UNIQUE ([token]),
    CONSTRAINT [CK_EMAIL_VERIFICATION_TOKEN_PURPOSE]
        CHECK ([purpose] IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET', 'EMAIL_UPDATE', 'PHONE_UPDATE', 'REGISTER', 'FORGOT_PASSWORD', 'CHANGE_EMAIL', 'UPDATE_EMAIL')),
    CONSTRAINT [CK_EMAIL_VERIFICATION_TOKEN_ATTEMPT_COUNT]
        CHECK ([attemptCount] >= 0),
    CONSTRAINT [CK_EMAIL_VERIFICATION_EXPIRED_AT]
        CHECK ([expiredAt] > [createdAt]),
    CONSTRAINT [FK_EMAIL_VERIFICATION_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO

CREATE TABLE [REFRESH_TOKEN] (
    [refreshTokenId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NOT NULL,
    [tokenHash] NVARCHAR(450) NOT NULL,
    [issuedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [expiresAt] DATETIME2 NOT NULL,
    [revokedAt] DATETIME2 NULL,
    [isRevoked] BIT NOT NULL DEFAULT 0,

    CONSTRAINT [UQ_REFRESH_TOKEN_HASH] UNIQUE ([tokenHash]),
    CONSTRAINT [CK_REFRESH_TOKEN_EXPIRES_AT] CHECK ([expiresAt] > [issuedAt]),
    CONSTRAINT [FK_REFRESH_TOKEN_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO

CREATE TABLE [CUSTOMER_PROFILE] (
    [customerProfileId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NOT NULL,
    [memberLevel] NVARCHAR(30) NOT NULL DEFAULT 'STANDARD',
    [rewardPoints] INT NOT NULL DEFAULT 0,
    [dateOfBirth] DATE NULL,
    [gender] NVARCHAR(20) NULL,
    [identityCard] NVARCHAR(50) NULL,
    [address] NVARCHAR(500) NULL,
    [avatarUrl] NVARCHAR(1000) NULL,

    CONSTRAINT [UQ_CUSTOMER_PROFILE_USER] UNIQUE ([userId]),
    CONSTRAINT [CK_CUSTOMER_PROFILE_MEMBER_LEVEL]
        CHECK ([memberLevel] IN ('STANDARD', 'SILVER', 'GOLD', 'PLATINUM')),
    CONSTRAINT [CK_CUSTOMER_PROFILE_REWARD_POINTS] CHECK ([rewardPoints] >= 0),
    CONSTRAINT [FK_CUSTOMER_PROFILE_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO

CREATE TABLE [STAFF_PROFILE] (
    [staffProfileId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NOT NULL,
    [cinemaId] NVARCHAR(50) NOT NULL,
    [position] NVARCHAR(100) NOT NULL,
    [hireDate] DATE NULL,
    [dateOfBirth] DATE NULL,
    [gender] NVARCHAR(20) NULL,
    [identityCard] NVARCHAR(50) NULL,
    [address] NVARCHAR(500) NULL,
    [avatarUrl] NVARCHAR(1000) NULL,
    [employmentStatus] NVARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT [UQ_STAFF_PROFILE_USER] UNIQUE ([userId]),
    CONSTRAINT [CK_STAFF_PROFILE_EMPLOYMENT_STATUS]
        CHECK ([employmentStatus] IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')),
    CONSTRAINT [FK_STAFF_PROFILE_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId]),
    CONSTRAINT [FK_STAFF_PROFILE_CINEMA]
        FOREIGN KEY ([cinemaId]) REFERENCES [CINEMA]([cinemaId])
);
GO

-- =========================
-- 3. CINEMA STRUCTURE
-- =========================

CREATE TABLE [ROOM] (
    [roomId] NVARCHAR(50) PRIMARY KEY,
    [cinemaId] NVARCHAR(50) NOT NULL,
    [roomName] NVARCHAR(100) NOT NULL,
    [capacity] INT NOT NULL,
    [roomStatus] NVARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT [UQ_ROOM_CINEMA_ROOM_NAME] UNIQUE ([cinemaId], [roomName]),
    CONSTRAINT [CK_ROOM_CAPACITY] CHECK ([capacity] > 0),
    CONSTRAINT [CK_ROOM_STATUS]
        CHECK ([roomStatus] IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE')),
    CONSTRAINT [FK_ROOM_CINEMA]
        FOREIGN KEY ([cinemaId]) REFERENCES [CINEMA]([cinemaId])
);
GO

CREATE TABLE [SEAT] (
    [seatId] NVARCHAR(50) PRIMARY KEY,
    [roomId] NVARCHAR(50) NOT NULL,
    [seatTypeId] NVARCHAR(50) NOT NULL,
    [seatCode] NVARCHAR(20) NOT NULL,
    [rowLabel] NVARCHAR(10) NOT NULL,
    [seatNumber] INT NOT NULL,
    [isActive] BIT NOT NULL DEFAULT 1,

    CONSTRAINT [UQ_SEAT_ROOM_SEAT_CODE] UNIQUE ([roomId], [seatCode]),
    CONSTRAINT [UQ_SEAT_ROOM_ROW_NUMBER] UNIQUE ([roomId], [rowLabel], [seatNumber]),
    CONSTRAINT [CK_SEAT_NUMBER] CHECK ([seatNumber] > 0),
    CONSTRAINT [FK_SEAT_ROOM]
        FOREIGN KEY ([roomId]) REFERENCES [ROOM]([roomId]),
    CONSTRAINT [FK_SEAT_SEAT_TYPE]
        FOREIGN KEY ([seatTypeId]) REFERENCES [SEAT_TYPE]([seatTypeId])
);
GO

-- =========================
-- 4. MOVIE / SHOWTIME
-- =========================

CREATE TABLE [SHOWTIME] (
    [showtimeId] NVARCHAR(50) PRIMARY KEY,
    [movieId] NVARCHAR(50) NOT NULL,
    [roomId] NVARCHAR(50) NOT NULL,
    [startTime] DATETIME2 NOT NULL,
    [endTime] DATETIME2 NOT NULL,
    [basePrice] DECIMAL(18,2) NOT NULL DEFAULT 0,
    [status] NVARCHAR(30) NOT NULL DEFAULT 'OPEN',
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [CK_SHOWTIME_TIME_RANGE] CHECK ([endTime] > [startTime]),
    CONSTRAINT [CK_SHOWTIME_BASE_PRICE] CHECK ([basePrice] >= 0),
    CONSTRAINT [CK_SHOWTIME_STATUS]
        CHECK ([status] IN ('OPEN', 'CLOSED', 'CANCELLED', 'COMPLETED', 'SUSPENDED', 'PROCESSING_UNSTABLE')),
    CONSTRAINT [FK_SHOWTIME_MOVIE]
        FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId]),
    CONSTRAINT [FK_SHOWTIME_ROOM]
        FOREIGN KEY ([roomId]) REFERENCES [ROOM]([roomId])
);
GO

CREATE TABLE [SHOWTIME_SEAT] (
    [showtimeSeatId] NVARCHAR(50) PRIMARY KEY,
    [showtimeId] NVARCHAR(50) NOT NULL,
    [seatId] NVARCHAR(50) NOT NULL,
    [seatStatus] NVARCHAR(30) NOT NULL DEFAULT 'AVAILABLE',
    [lockedUntil] DATETIME2 NULL,
    [lockedByUserId] NVARCHAR(50) NULL,
    [rowVersion] ROWVERSION,

    CONSTRAINT [UQ_SHOWTIME_SEAT_SHOWTIME_SEAT] UNIQUE ([showtimeId], [seatId]),
    CONSTRAINT [CK_SHOWTIME_SEAT_STATUS]
        CHECK ([seatStatus] IN ('AVAILABLE', 'LOCKED', 'BOOKED', 'RELEASED', 'UNAVAILABLE')),
    CONSTRAINT [FK_SHOWTIME_SEAT_SHOWTIME]
        FOREIGN KEY ([showtimeId]) REFERENCES [SHOWTIME]([showtimeId]),
    CONSTRAINT [FK_SHOWTIME_SEAT_SEAT]
        FOREIGN KEY ([seatId]) REFERENCES [SEAT]([seatId]),
    CONSTRAINT [FK_SHOWTIME_SEAT_LOCKED_BY_USER]
        FOREIGN KEY ([lockedByUserId]) REFERENCES [USER]([userId])
);
GO

-- =========================
-- 5. BOOKING / TICKET
-- =========================

CREATE TABLE [BOOKING] (
    [bookingId] NVARCHAR(50) PRIMARY KEY,
    [customerProfileId] NVARCHAR(50) NULL,
    [showtimeId] NVARCHAR(50) NOT NULL,
    [createdByStaffProfileId] NVARCHAR(50) NULL,
    [bookingChannel] NVARCHAR(30) NOT NULL DEFAULT 'ONLINE',
    [guestName] NVARCHAR(255) NULL,
    [guestPhone] NVARCHAR(30) NULL,
    [guestEmail] NVARCHAR(255) NULL,
    [bookingStatus] NVARCHAR(30) NOT NULL DEFAULT 'CREATED',
    [totalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [expiredAt] DATETIME2 NULL,

    CONSTRAINT [CK_BOOKING_STATUS]
        CHECK ([bookingStatus] IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'CANCELLED', 'REFUND_PENDING', 'REFUNDED', 'COMPLETED', 'PROCESSING_UNSTABLE')),
    CONSTRAINT [CK_BOOKING_CHANNEL]
        CHECK ([bookingChannel] IN ('ONLINE', 'COUNTER')),
    CONSTRAINT [CK_BOOKING_ONLINE_CUSTOMER_REQUIRED]
        CHECK ([bookingChannel] <> 'ONLINE' OR [customerProfileId] IS NOT NULL),
    CONSTRAINT [CK_BOOKING_TOTAL_AMOUNT] CHECK ([totalAmount] >= 0),
    CONSTRAINT [FK_BOOKING_CUSTOMER_PROFILE]
        FOREIGN KEY ([customerProfileId]) REFERENCES [CUSTOMER_PROFILE]([customerProfileId]),
    CONSTRAINT [FK_BOOKING_SHOWTIME]
        FOREIGN KEY ([showtimeId]) REFERENCES [SHOWTIME]([showtimeId]),
    CONSTRAINT [FK_BOOKING_CREATED_BY_STAFF]
        FOREIGN KEY ([createdByStaffProfileId]) REFERENCES [STAFF_PROFILE]([staffProfileId])
);
GO

CREATE TABLE [BOOKING_SEAT] (
    [bookingSeatId] NVARCHAR(50) PRIMARY KEY,
    [bookingId] NVARCHAR(50) NOT NULL,
    [showtimeSeatId] NVARCHAR(50) NOT NULL,
    [seatPrice] DECIMAL(18,2) NOT NULL,

    CONSTRAINT [UQ_BOOKING_SEAT_SHOWTIME_SEAT] UNIQUE ([showtimeSeatId]),
    CONSTRAINT [CK_BOOKING_SEAT_PRICE] CHECK ([seatPrice] >= 0),
    CONSTRAINT [FK_BOOKING_SEAT_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId]),
    CONSTRAINT [FK_BOOKING_SEAT_SHOWTIME_SEAT]
        FOREIGN KEY ([showtimeSeatId]) REFERENCES [SHOWTIME_SEAT]([showtimeSeatId])
);
GO

CREATE TABLE [TICKET] (
    [ticketId] NVARCHAR(50) PRIMARY KEY,
    [bookingSeatId] NVARCHAR(50) NOT NULL,
    [qrCode] NVARCHAR(450) NOT NULL,
    [ticketStatus] NVARCHAR(30) NOT NULL DEFAULT 'UNUSED',
    [generatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [UQ_TICKET_BOOKING_SEAT] UNIQUE ([bookingSeatId]),
    CONSTRAINT [UQ_TICKET_QR_CODE] UNIQUE ([qrCode]),
    CONSTRAINT [CK_TICKET_STATUS]
        CHECK ([ticketStatus] IN ('GENERATED', 'UNUSED', 'CHECKED_IN', 'CANCELLED', 'REFUNDED')),
    CONSTRAINT [FK_TICKET_BOOKING_SEAT]
        FOREIGN KEY ([bookingSeatId]) REFERENCES [BOOKING_SEAT]([bookingSeatId])
);
GO

CREATE TABLE [CHECKIN_LOG] (
    [checkInLogId] NVARCHAR(50) PRIMARY KEY,
    [ticketId] NVARCHAR(50) NULL,
    [staffProfileId] NVARCHAR(50) NOT NULL,
    [scanTime] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [result] NVARCHAR(30) NOT NULL,
    [failureReason] NVARCHAR(500) NULL,
    [rawQrCode] NVARCHAR(450) NULL,

    CONSTRAINT [CK_CHECKIN_LOG_RESULT]
        CHECK ([result] IN ('SUCCESS', 'FAILED')),
    CONSTRAINT [FK_CHECKIN_LOG_TICKET]
        FOREIGN KEY ([ticketId]) REFERENCES [TICKET]([ticketId]),
    CONSTRAINT [FK_CHECKIN_LOG_STAFF_PROFILE]
        FOREIGN KEY ([staffProfileId]) REFERENCES [STAFF_PROFILE]([staffProfileId])
);
GO

-- =========================
-- 6. PAYMENT / REFUND
-- =========================

CREATE TABLE [PAYMENT] (
    [paymentId] NVARCHAR(50) PRIMARY KEY,
    [bookingId] NVARCHAR(50) NOT NULL,
    [paymentProviderId] NVARCHAR(50) NOT NULL,
    [amount] DECIMAL(18,2) NOT NULL,
    [paymentMethod] NVARCHAR(50) NULL,
    [transactionCode] NVARCHAR(255) NULL,
    [providerTransactionCode] NVARCHAR(255) NULL,
    [paymentStatus] NVARCHAR(30) NOT NULL DEFAULT 'PENDING',
    [failureReason] NVARCHAR(1000) NULL,
    [rawCallbackPayload] NVARCHAR(MAX) NULL,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NULL,
    [paidAt] DATETIME2 NULL,

    CONSTRAINT [CK_PAYMENT_AMOUNT] CHECK ([amount] >= 0),
    CONSTRAINT [CK_PAYMENT_STATUS]
        CHECK ([paymentStatus] IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED', 'EXPIRED')),
    CONSTRAINT [FK_PAYMENT_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId]),
    CONSTRAINT [FK_PAYMENT_PAYMENT_PROVIDER]
        FOREIGN KEY ([paymentProviderId]) REFERENCES [PAYMENT_PROVIDER]([paymentProviderId])
);
GO

CREATE TABLE [SHOWTIME_CANCELLATION] (
    [showtimeCancellationId] NVARCHAR(50) PRIMARY KEY,
    [showtimeId] NVARCHAR(50) NOT NULL,
    [cancelledByUserId] NVARCHAR(50) NOT NULL,
    [cancelledByStaffId] NVARCHAR(50) NULL,
    [cancelReason] NVARCHAR(1000) NOT NULL,
    [cancelledAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [UQ_SHOWTIME_CANCELLATION_SHOWTIME] UNIQUE ([showtimeId]),
    CONSTRAINT [FK_SHOWTIME_CANCELLATION_SHOWTIME]
        FOREIGN KEY ([showtimeId]) REFERENCES [SHOWTIME]([showtimeId]),
    CONSTRAINT [FK_SHOWTIME_CANCELLATION_USER]
        FOREIGN KEY ([cancelledByUserId]) REFERENCES [USER]([userId]),
    CONSTRAINT [FK_SHOWTIME_CANCELLATION_STAFF_PROFILE]
        FOREIGN KEY ([cancelledByStaffId]) REFERENCES [STAFF_PROFILE]([staffProfileId])
);
GO

CREATE TABLE [REFUND] (
    [refundId] NVARCHAR(50) PRIMARY KEY,
    [bookingId] NVARCHAR(50) NOT NULL,
    [paymentId] NVARCHAR(50) NOT NULL,
    [paymentProviderId] NVARCHAR(50) NOT NULL,
    [showtimeCancellationId] NVARCHAR(50) NULL,
    [refundAmount] DECIMAL(18,2) NOT NULL,
    [refundStatus] NVARCHAR(30) NOT NULL DEFAULT 'PENDING',
    [refundReason] NVARCHAR(1000) NULL,
    [providerRefundCode] NVARCHAR(255) NULL,
    [failureReason] NVARCHAR(1000) NULL,
    [requestedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [refundedAt] DATETIME2 NULL,

    CONSTRAINT [CK_REFUND_AMOUNT] CHECK ([refundAmount] > 0),
    CONSTRAINT [CK_REFUND_STATUS]
        CHECK ([refundStatus] IN ('PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'REQUESTED')),
    CONSTRAINT [FK_REFUND_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId]),
    CONSTRAINT [FK_REFUND_PAYMENT]
        FOREIGN KEY ([paymentId]) REFERENCES [PAYMENT]([paymentId]),
    CONSTRAINT [FK_REFUND_PAYMENT_PROVIDER]
        FOREIGN KEY ([paymentProviderId]) REFERENCES [PAYMENT_PROVIDER]([paymentProviderId]),
    CONSTRAINT [FK_REFUND_SHOWTIME_CANCELLATION]
        FOREIGN KEY ([showtimeCancellationId]) REFERENCES [SHOWTIME_CANCELLATION]([showtimeCancellationId])
);
GO

-- =========================
-- 7. VOUCHER
-- =========================

CREATE TABLE [VOUCHER_USAGE] (
    [voucherUsageId] NVARCHAR(50) PRIMARY KEY,
    [voucherId] NVARCHAR(50) NOT NULL,
    [customerProfileId] NVARCHAR(50) NOT NULL,
    [bookingId] NVARCHAR(50) NOT NULL,
    [discountAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
    [usageStatus] NVARCHAR(30) NOT NULL DEFAULT 'APPLIED',
    [usedAt] DATETIME2 NULL,

    CONSTRAINT [UQ_VOUCHER_USAGE_BOOKING] UNIQUE ([bookingId]),
    CONSTRAINT [CK_VOUCHER_USAGE_DISCOUNT_AMOUNT] CHECK ([discountAmount] >= 0),
    CONSTRAINT [CK_VOUCHER_USAGE_STATUS]
        CHECK ([usageStatus] IN ('APPLIED', 'CONFIRMED', 'CANCELLED')),
    CONSTRAINT [FK_VOUCHER_USAGE_VOUCHER]
        FOREIGN KEY ([voucherId]) REFERENCES [VOUCHER]([voucherId]),
    CONSTRAINT [FK_VOUCHER_USAGE_CUSTOMER_PROFILE]
        FOREIGN KEY ([customerProfileId]) REFERENCES [CUSTOMER_PROFILE]([customerProfileId]),
    CONSTRAINT [FK_VOUCHER_USAGE_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId])
);
GO

-- =========================
-- 8. F&B
-- =========================

CREATE TABLE [BOOKING_FB_ITEM] (
    [bookingFBItemId] NVARCHAR(50) PRIMARY KEY,
    [bookingId] NVARCHAR(50) NOT NULL,
    [fbItemId] NVARCHAR(50) NOT NULL,
    [quantity] INT NOT NULL,
    [unitPrice] DECIMAL(18,2) NOT NULL,
    [subtotal] DECIMAL(18,2) NOT NULL,

    CONSTRAINT [CK_BOOKING_FB_ITEM_QUANTITY] CHECK ([quantity] > 0),
    CONSTRAINT [CK_BOOKING_FB_ITEM_UNIT_PRICE] CHECK ([unitPrice] >= 0),
    CONSTRAINT [CK_BOOKING_FB_ITEM_SUBTOTAL] CHECK ([subtotal] >= 0),
    CONSTRAINT [FK_BOOKING_FB_ITEM_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId]),
    CONSTRAINT [FK_BOOKING_FB_ITEM_FB_ITEM]
        FOREIGN KEY ([fbItemId]) REFERENCES [FB_ITEM]([fbItemId])
);
GO

CREATE TABLE [CINEMA_FB_INVENTORY] (
    [cinemaInventoryId] NVARCHAR(50) PRIMARY KEY,
    [cinemaId] NVARCHAR(50) NOT NULL,
    [fbItemId] NVARCHAR(50) NOT NULL,
    [quantity] INT NOT NULL DEFAULT 0,

    CONSTRAINT [UQ_CINEMA_FB_INVENTORY] UNIQUE ([cinemaId], [fbItemId]),
    CONSTRAINT [CK_CINEMA_FB_INVENTORY_QUANTITY] CHECK ([quantity] >= 0),
    CONSTRAINT [FK_CINEMA_FB_INVENTORY_CINEMA]
        FOREIGN KEY ([cinemaId]) REFERENCES [CINEMA]([cinemaId]),
    CONSTRAINT [FK_CINEMA_FB_INVENTORY_FB_ITEM]
        FOREIGN KEY ([fbItemId]) REFERENCES [FB_ITEM]([fbItemId])
);
GO

-- =========================
-- 9. REWARD / REVIEW / NOTIFICATION / AUDIT
-- =========================

CREATE TABLE [REWARD_POINT_TRANSACTION] (
    [rewardTransactionId] NVARCHAR(50) PRIMARY KEY,
    [customerProfileId] NVARCHAR(50) NOT NULL,
    [bookingId] NVARCHAR(50) NULL,
    [transactionType] NVARCHAR(30) NOT NULL,
    [points] INT NOT NULL,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [CK_REWARD_POINT_TRANSACTION_TYPE]
        CHECK ([transactionType] IN ('EARN', 'REDEEM', 'REVERT', 'ADJUST')),
    CONSTRAINT [CK_REWARD_POINT_TRANSACTION_POINTS] CHECK ([points] <> 0),
    CONSTRAINT [FK_REWARD_POINT_TRANSACTION_CUSTOMER_PROFILE]
        FOREIGN KEY ([customerProfileId]) REFERENCES [CUSTOMER_PROFILE]([customerProfileId]),
    CONSTRAINT [FK_REWARD_POINT_TRANSACTION_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId])
);
GO

CREATE TABLE [REVIEW] (
    [reviewId] NVARCHAR(50) PRIMARY KEY,
    [customerProfileId] NVARCHAR(50) NOT NULL,
    [movieId] NVARCHAR(50) NOT NULL,
    [bookingId] NVARCHAR(50) NULL,
    [rating] INT NOT NULL,
    [comment] NVARCHAR(1000) NULL,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [CK_REVIEW_RATING] CHECK ([rating] BETWEEN 0 AND 5),
    CONSTRAINT [FK_REVIEW_MODERATED_BY]
        FOREIGN KEY ([moderatedBy]) REFERENCES [USER]([userId]),
    CONSTRAINT [FK_REVIEW_CUSTOMER_PROFILE]
        FOREIGN KEY ([customerProfileId]) REFERENCES [CUSTOMER_PROFILE]([customerProfileId]),
    CONSTRAINT [FK_REVIEW_MOVIE]
        FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId]),
    CONSTRAINT [FK_REVIEW_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId])
);
GO

CREATE TABLE [MOVIE_VIEW_LOG] (
    [movieViewLogId] NVARCHAR(50) PRIMARY KEY,
    [movieId] NVARCHAR(50) NOT NULL,
    [userId] NVARCHAR(50) NULL,
    [viewedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [ipAddress] NVARCHAR(100) NULL,

    CONSTRAINT [FK_MOVIE_VIEW_LOG_MOVIE]
        FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId])
);
GO

CREATE TABLE [NOTIFICATION] (
    [notificationId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NOT NULL,
    [bookingId] NVARCHAR(50) NULL,
    [title] NVARCHAR(255) NOT NULL,
    [message] NVARCHAR(1000) NOT NULL,
    [isRead] BIT NOT NULL DEFAULT 0,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_NOTIFICATION_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId]),
    CONSTRAINT [FK_NOTIFICATION_BOOKING]
        FOREIGN KEY ([bookingId]) REFERENCES [BOOKING]([bookingId])
);
GO

CREATE TABLE [AUDIT_LOG] (
    [auditLogId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NULL,
    [action] NVARCHAR(100) NOT NULL,
    [entityName] NVARCHAR(100) NOT NULL,
    [entityId] NVARCHAR(50) NULL,
    [oldValue] NVARCHAR(MAX) NULL,
    [newValue] NVARCHAR(MAX) NULL,
    [ipAddress] NVARCHAR(100) NULL,
    [userAgent] NVARCHAR(500) NULL,
    [correlationId] NVARCHAR(100) NULL,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_AUDIT_LOG_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO


-- =========================
-- 9.1. NEW TABLES (CHAT / VIEW / HISTORY)
-- =========================

CREATE TABLE [CHAT_HISTORY] (
    [chatHistoryId] NVARCHAR(50) PRIMARY KEY,
    [userId] NVARCHAR(50) NULL,
    [sessionId] NVARCHAR(100) NULL,
    [message] NVARCHAR(MAX) NOT NULL,
    [isUserMessage] BIT NOT NULL DEFAULT 1,
    [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_CHAT_HISTORY_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO

CREATE TABLE [MOVIE_VIEW_LOG] (
    [viewLogId] NVARCHAR(50) PRIMARY KEY,
    [movieId] NVARCHAR(50) NOT NULL,
    [userId] NVARCHAR(50) NULL,
    [viewTime] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [ipAddress] NVARCHAR(100) NULL,

    CONSTRAINT [FK_MOVIE_VIEW_LOG_MOVIE]
        FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId]),
    CONSTRAINT [FK_MOVIE_VIEW_LOG_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId])
);
GO

CREATE TABLE [MOVIE_DAILY_VIEW] (
    [dailyViewId] NVARCHAR(50) PRIMARY KEY,
    [movieId] NVARCHAR(50) NOT NULL,
    [viewDate] DATE NOT NULL,
    [viewCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT [UQ_MOVIE_DAILY_VIEW] UNIQUE ([movieId], [viewDate]),
    CONSTRAINT [FK_MOVIE_DAILY_VIEW_MOVIE]
        FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId])
);
GO

CREATE TABLE [REVIEW_EDIT_HISTORY] (
    [editHistoryId] NVARCHAR(50) PRIMARY KEY,
    [reviewId] NVARCHAR(50) NOT NULL,
    [oldComment] NVARCHAR(1000) NULL,
    [newComment] NVARCHAR(1000) NULL,
    [oldRating] INT NULL,
    [newRating] INT NULL,
    [editedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_REVIEW_EDIT_HISTORY_REVIEW]
        FOREIGN KEY ([reviewId]) REFERENCES [REVIEW]([reviewId])
);
GO

CREATE TABLE [REVIEW_MODERATION_HISTORY] (
    [moderationHistoryId] NVARCHAR(50) PRIMARY KEY,
    [reviewId] NVARCHAR(50) NOT NULL,
    [moderatedBy] NVARCHAR(50) NOT NULL,
    [oldStatus] VARCHAR(20) NULL,
    [newStatus] VARCHAR(20) NOT NULL,
    [reason] NVARCHAR(1000) NULL,
    [moderatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_REVIEW_MODERATION_HISTORY_REVIEW]
        FOREIGN KEY ([reviewId]) REFERENCES [REVIEW]([reviewId]),
    CONSTRAINT [FK_REVIEW_MODERATION_HISTORY_USER]
        FOREIGN KEY ([moderatedBy]) REFERENCES [USER]([userId])
);
GO

-- =========================
-- 10. FILTERED UNIQUE INDEXES
-- =========================

-- One booking can have many payment attempts, but only one SUCCESS payment.
CREATE UNIQUE INDEX [UX_PAYMENT_ONE_SUCCESS_PER_BOOKING]
ON [PAYMENT]([bookingId])
WHERE [paymentStatus] = 'SUCCESS';
GO

-- Transaction code may be null before provider callback, but must be unique when provided.
CREATE UNIQUE INDEX [UX_PAYMENT_TRANSACTION_CODE]
ON [PAYMENT]([transactionCode])
WHERE [transactionCode] IS NOT NULL;
GO

-- Provider transaction code may be null before callback, but must be unique when provided.
CREATE UNIQUE INDEX [UX_PAYMENT_PROVIDER_TRANSACTION_CODE]
ON [PAYMENT]([providerTransactionCode])
WHERE [providerTransactionCode] IS NOT NULL;
GO

-- Provider refund code may be null before refund callback, but must be unique when provided.
CREATE UNIQUE INDEX [UX_REFUND_PROVIDER_REFUND_CODE]
ON [REFUND]([providerRefundCode])
WHERE [providerRefundCode] IS NOT NULL;
GO

-- If a review is linked to a booking, that booking can only create one review.
CREATE UNIQUE INDEX [UX_REVIEW_BOOKING]
ON [REVIEW]([bookingId])
WHERE [bookingId] IS NOT NULL;
GO

-- =========================
-- 11. COMMON PERFORMANCE INDEXES
-- =========================

CREATE INDEX [IX_USER_ROLE_ID] ON [USER]([roleId]);
CREATE UNIQUE INDEX [UX_CUSTOMER_PROFILE_IDENTITY_CARD]
ON [CUSTOMER_PROFILE]([identityCard])
WHERE [identityCard] IS NOT NULL;
CREATE UNIQUE INDEX [UX_STAFF_PROFILE_IDENTITY_CARD]
ON [STAFF_PROFILE]([identityCard])
WHERE [identityCard] IS NOT NULL;
CREATE INDEX [IX_STAFF_PROFILE_CINEMA_ID] ON [STAFF_PROFILE]([cinemaId]);
CREATE INDEX [IX_ROOM_CINEMA_ID] ON [ROOM]([cinemaId]);
CREATE INDEX [IX_SEAT_ROOM_ID] ON [SEAT]([roomId]);
CREATE INDEX [IX_SHOWTIME_MOVIE_ID] ON [SHOWTIME]([movieId]);
CREATE INDEX [IX_SHOWTIME_ROOM_TIME] ON [SHOWTIME]([roomId], [startTime], [endTime]);
CREATE UNIQUE INDEX [UQ_SHOWTIME_ROOM_STARTTIME] ON [SHOWTIME]([roomId], [startTime]);
CREATE INDEX [IX_SHOWTIME_SEAT_SHOWTIME_ID] ON [SHOWTIME_SEAT]([showtimeId]);
CREATE INDEX [IX_SHOWTIME_SEAT_STATUS] ON [SHOWTIME_SEAT]([showtimeId], [seatStatus]);
CREATE INDEX [IX_BOOKING_CUSTOMER_PROFILE_ID] ON [BOOKING]([customerProfileId]);
CREATE INDEX [IX_BOOKING_CREATED_BY_STAFF_PROFILE_ID] ON [BOOKING]([createdByStaffProfileId]);
CREATE INDEX [IX_BOOKING_CHANNEL] ON [BOOKING]([bookingChannel]);
CREATE INDEX [IX_BOOKING_SHOWTIME_ID] ON [BOOKING]([showtimeId]);
CREATE INDEX [IX_BOOKING_STATUS] ON [BOOKING]([bookingStatus]);
CREATE INDEX [IX_PAYMENT_BOOKING_ID] ON [PAYMENT]([bookingId]);
CREATE INDEX [IX_REFUND_BOOKING_ID] ON [REFUND]([bookingId]);
CREATE INDEX [IX_CHECKIN_LOG_TICKET_ID] ON [CHECKIN_LOG]([ticketId]);
CREATE INDEX [IX_CHECKIN_LOG_RAW_QR_CODE] ON [CHECKIN_LOG]([rawQrCode]) WHERE [rawQrCode] IS NOT NULL;
CREATE INDEX [IX_NOTIFICATION_USER_READ] ON [NOTIFICATION]([userId], [isRead]);
CREATE INDEX [IX_AUDIT_LOG_USER_CREATED_AT] ON [AUDIT_LOG]([userId], [createdAt]);
GO

-- =========================
-- 12. DEVELOPMENT SEED DATA
-- =========================
-- This seed matches the current backend constants and EF mappings.
-- Order matters because MOVIE, SEAT, SHOWTIME and SHOWTIME_SEAT depend on
-- ROLE, CINEMA, ROOM and SEAT_TYPE foreign keys.

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_CUSTOMER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_CUSTOMER', 'CUSTOMER', N'Customer account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_STAFF')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_STAFF', 'STAFF', N'Cinema staff account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_MANAGER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_MANAGER', 'MANAGER', N'Cinema manager account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_ADMIN')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_ADMIN', 'ADMIN', N'System administrator account');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_NORMAL')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_NORMAL', 'NORMAL', 0.00);

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_VIP')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_VIP', 'VIP', 30000.00);

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_SWEETBOX')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_SWEETBOX', 'SWEETBOX', 50000.00);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[CINEMA] WHERE [cinemaId] = 'CIN_ND_Q1')
    INSERT INTO dbo.[CINEMA] ([cinemaId], [cinemaName], [address], [city], [phoneNumber], [cinemaStatus])
    VALUES ('CIN_ND_Q1', N'Rap Nguyen Du - Quan 1', N'116 Nguyen Du, Phuong Ben Thanh, Quan 1', N'Ho Chi Minh', '02838273111', 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[CINEMA] WHERE [cinemaId] = 'CIN_BH_DN')
    INSERT INTO dbo.[CINEMA] ([cinemaId], [cinemaName], [address], [city], [phoneNumber], [cinemaStatus])
    VALUES ('CIN_BH_DN', N'Rap Bien Hoa - Dong Nai', N'Khu pho 2, Phuong Tan Tien, TP Bien Hoa', N'Dong Nai', '02513822111', 'ACTIVE');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM01')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM01', 'CIN_ND_Q1', N'Phong 1 - 2D Dolby', 40, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM02')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM02', 'CIN_ND_Q1', N'Phong 2 - 3D IMAX', 30, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM03')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM03', 'CIN_BH_DN', N'Phong VIP', 20, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM04')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM04', 'CIN_BH_DN', N'Phong Sweetbox', 12, 'ACTIVE');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DOCTOR_STRANGE_3')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [genre], [language], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DOCTOR_STRANGE_3', N'Doctor Strange 3', 120, N'Hanh dong, Vien tuong',
         N'Tieng Anh - Phu de Tieng Viet', '2026-05-01', 'T16',
         N'Phan phim tiep theo ve Phu Thuy Toi Thuong.',
         'https://image.example.com/doctor-strange-3.jpg',
         'https://youtube.com/watch?v=doctor-strange-3', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_LAT_MAT_8')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [genre], [language], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_LAT_MAT_8', N'Lat Mat 8', 115, N'Hai, Gia dinh, Tam ly',
         N'Tieng Viet', '2026-04-28', 'P',
         N'Tac pham dien anh moi voi cau chuyen gia dinh va hanh trinh hoa giai.',
         'https://image.example.com/lat-mat-8.jpg',
         'https://youtube.com/watch?v=lat-mat-8', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_AVENGERS_SECRET_WARS')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [genre], [language], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_AVENGERS_SECRET_WARS', N'Avengers: Secret Wars', 150, N'Hanh dong, Sieu anh hung',
         N'Tieng Anh - Phu de Tieng Viet', '2026-06-20', 'T13',
         N'Biet doi sieu anh hung doi mat moi de doa da vu tru.',
         'https://image.example.com/avengers-secret-wars.jpg',
         'https://youtube.com/watch?v=avengers-secret-wars', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DORAEMON_2026')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [genre], [language], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DORAEMON_2026', N'Doraemon Movie 2026', 105, N'Hoat hinh, Phieu luu, Gia dinh',
         N'Long tieng Viet', '2026-06-01', 'P',
         N'Doraemon va nhom ban trong chuyen phieu luu moi.',
         'https://image.example.com/doraemon-2026.jpg',
         'https://youtube.com/watch?v=doraemon-2026', 'NOW_SHOWING');
GO

DECLARE @RoomId NVARCHAR(50);
DECLARE @Rows TABLE ([rowLabel] NVARCHAR(10), [seatCount] INT);
DECLARE @RowLabel NVARCHAR(10);
DECLARE @SeatCount INT;
DECLARE @SeatNumber INT;
DECLARE @SeatTypeId NVARCHAR(50);
DECLARE @SeatId NVARCHAR(50);
DECLARE @SeatCode NVARCHAR(20);

DECLARE room_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT [roomId] FROM (VALUES ('RM01'), ('RM02'), ('RM03'), ('RM04')) AS seededRooms([roomId]);

OPEN room_cursor;
FETCH NEXT FROM room_cursor INTO @RoomId;

WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE FROM @Rows;

    IF @RoomId = 'RM01'
        INSERT INTO @Rows VALUES ('A', 10), ('B', 10), ('C', 10), ('D', 10);
    ELSE IF @RoomId = 'RM02'
        INSERT INTO @Rows VALUES ('A', 10), ('B', 10), ('C', 10);
    ELSE IF @RoomId = 'RM03'
        INSERT INTO @Rows VALUES ('A', 8), ('B', 8), ('C', 4);
    ELSE
        INSERT INTO @Rows VALUES ('S', 12);

    DECLARE row_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [rowLabel], [seatCount] FROM @Rows;

    OPEN row_cursor;
    FETCH NEXT FROM row_cursor INTO @RowLabel, @SeatCount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SeatNumber = 1;

        WHILE @SeatNumber <= @SeatCount
        BEGIN
            SET @SeatCode = CONCAT(@RowLabel, @SeatNumber);
            SET @SeatId = CONCAT('SEAT_', @RoomId, '_', @RowLabel, RIGHT(CONCAT('0', @SeatNumber), 2));
            SET @SeatTypeId =
                CASE
                    WHEN @RoomId = 'RM04' THEN 'SEAT_TYPE_SWEETBOX'
                    WHEN @RowLabel IN ('C', 'D', 'S') THEN 'SEAT_TYPE_VIP'
                    ELSE 'SEAT_TYPE_NORMAL'
                END;

            IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT] WHERE [seatId] = @SeatId)
                INSERT INTO dbo.[SEAT]
                    ([seatId], [roomId], [seatTypeId], [seatCode], [rowLabel], [seatNumber], [isActive])
                VALUES
                    (@SeatId, @RoomId, @SeatTypeId, @SeatCode, @RowLabel, @SeatNumber, 1);

            SET @SeatNumber += 1;
        END;

        FETCH NEXT FROM row_cursor INTO @RowLabel, @SeatCount;
    END;

    CLOSE row_cursor;
    DEALLOCATE row_cursor;

    FETCH NEXT FROM room_cursor INTO @RoomId;
END;

CLOSE room_cursor;
DEALLOCATE room_cursor;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW001')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW001', 'MOV_DOCTOR_STRANGE_3', 'RM01', '2026-07-01T10:00:00', '2026-07-01T12:15:00', 80000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW002')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW002', 'MOV_LAT_MAT_8', 'RM01', '2026-07-01T13:00:00', '2026-07-01T15:10:00', 85000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW003')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW003', 'MOV_AVENGERS_SECRET_WARS', 'RM02', '2026-07-01T14:30:00', '2026-07-01T17:15:00', 120000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW004')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW004', 'MOV_DORAEMON_2026', 'RM03', '2026-07-01T19:00:00', '2026-07-01T21:00:00', 90000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW005')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW005', 'MOV_DOCTOR_STRANGE_3', 'RM04', '2026-07-02T20:00:00', '2026-07-02T22:15:00', 150000.00, 'OPEN');
GO

INSERT INTO dbo.[SHOWTIME_SEAT] ([showtimeSeatId], [showtimeId], [seatId], [seatStatus], [lockedUntil], [lockedByUserId])
SELECT
    CONCAT('STS_', showtime.[showtimeId], '_', seat.[seatId]),
    showtime.[showtimeId],
    seat.[seatId],
    CASE
        WHEN showtime.[showtimeId] = 'SHW001' AND seat.[seatCode] IN ('A1', 'A2') THEN 'BOOKED'
        WHEN showtime.[showtimeId] = 'SHW001' AND seat.[seatCode] IN ('B1', 'B2') THEN 'UNAVAILABLE'
        WHEN showtime.[showtimeId] = 'SHW003' AND seat.[seatCode] IN ('C1', 'C2') THEN 'LOCKED'
        ELSE 'AVAILABLE'
    END,
    CASE
        WHEN showtime.[showtimeId] = 'SHW003' AND seat.[seatCode] IN ('C1', 'C2')
            THEN DATEADD(MINUTE, 10, SYSUTCDATETIME())
        ELSE NULL
    END,
    NULL
FROM dbo.[SHOWTIME] AS showtime
INNER JOIN dbo.[SEAT] AS seat
    ON seat.[roomId] = showtime.[roomId]
    AND seat.[isActive] = 1
WHERE showtime.[showtimeId] IN ('SHW001', 'SHW002', 'SHW003', 'SHW004', 'SHW005')
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.[SHOWTIME_SEAT] AS existing
        WHERE existing.[showtimeId] = showtime.[showtimeId]
            AND existing.[seatId] = seat.[seatId]
    );
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_POPCORN_PEPSI_L')
    INSERT INTO dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
    VALUES ('FB_POPCORN_PEPSI_L', N'Combo bap ngot va Pepsi lon', 75000.00, 'AVAILABLE');

IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_CHEESE_POPCORN_M')
    INSERT INTO dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
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
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.[CINEMA_FB_INVENTORY] AS existing
        WHERE existing.[cinemaId] = cinema.[cinemaId]
            AND existing.[fbItemId] = item.[fbItemId]
    );

UPDATE inventory
SET [quantity] = 500
FROM dbo.[CINEMA_FB_INVENTORY] AS inventory
WHERE inventory.[fbItemId] IN ('FB_POPCORN_PEPSI_L', 'FB_CHEESE_POPCORN_M')
    AND inventory.[quantity] < 500;

IF NOT EXISTS (SELECT 1 FROM dbo.[PAYMENT_PROVIDER] WHERE [paymentProviderId] = 'PP_SEPAY')
    INSERT INTO dbo.[PAYMENT_PROVIDER] ([paymentProviderId], [providerName], [apiEndpoint], [providerStatus])
    VALUES ('PP_SEPAY', 'SEPAY', 'https://my.sepay.vn', 'ACTIVE');
GO
`  
