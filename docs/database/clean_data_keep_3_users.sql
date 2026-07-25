-- ============================================================
-- CLEANUP SCRIPT FOR CinemaBookingDB
-- Purpose:
-- 1. Remove all seed/test data for BOOKING, SEAT, SHOWTIME.
-- 2. Remove all USERs EXCEPT:
--    - admin@gmail.com (Role: ADMIN)
--    - manager@gmail.com (Role: MANAGER)
--    - staff@gmail.com (Role: STAFF)
-- Password for all 3 users: Password1
-- ============================================================

USE [CinemaBookingDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. TEMPORARILY DISABLE ALL FOREIGN KEY CONSTRAINTS
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT ALL";
GO

-- 2. DELETE ALL BOOKINGS, TICKETS, PAYMENTS, REFUNDS, SHOWTIMES, SEATS, VOUCHERS
IF OBJECT_ID('dbo.TICKET_SCAN_LOG', 'U') IS NOT NULL DELETE FROM dbo.[TICKET_SCAN_LOG];
IF OBJECT_ID('dbo.TICKET', 'U') IS NOT NULL DELETE FROM dbo.[TICKET];
IF OBJECT_ID('dbo.BOOKING_FB_ITEM', 'U') IS NOT NULL DELETE FROM dbo.[BOOKING_FB_ITEM];
IF OBJECT_ID('dbo.BOOKING_SEAT', 'U') IS NOT NULL DELETE FROM dbo.[BOOKING_SEAT];
IF OBJECT_ID('dbo.REFUND_CLAIM_ITEM', 'U') IS NOT NULL DELETE FROM dbo.[REFUND_CLAIM_ITEM];
IF OBJECT_ID('dbo.REFUND_CLAIM', 'U') IS NOT NULL DELETE FROM dbo.[REFUND_CLAIM];
IF OBJECT_ID('dbo.CUSTOMER_REFUND_CONFIRMATION', 'U') IS NOT NULL DELETE FROM dbo.[CUSTOMER_REFUND_CONFIRMATION];
IF OBJECT_ID('dbo.STAFF_ASSISTED_REFUND_RECEIPT', 'U') IS NOT NULL DELETE FROM dbo.[STAFF_ASSISTED_REFUND_RECEIPT];
IF OBJECT_ID('dbo.CANCELLATION_COMPENSATION_EVENT', 'U') IS NOT NULL DELETE FROM dbo.[CANCELLATION_COMPENSATION_EVENT];
IF OBJECT_ID('dbo.PAYMENT', 'U') IS NOT NULL DELETE FROM dbo.[PAYMENT];
IF OBJECT_ID('dbo.BOOKING', 'U') IS NOT NULL DELETE FROM dbo.[BOOKING];
IF OBJECT_ID('dbo.SHOWTIME_SEAT', 'U') IS NOT NULL DELETE FROM dbo.[SHOWTIME_SEAT];
IF OBJECT_ID('dbo.SHOWTIME', 'U') IS NOT NULL DELETE FROM dbo.[SHOWTIME];
IF OBJECT_ID('dbo.SEAT', 'U') IS NOT NULL DELETE FROM dbo.[SEAT];
IF OBJECT_ID('dbo.VOUCHER_USAGE', 'U') IS NOT NULL DELETE FROM dbo.[VOUCHER_USAGE];
IF OBJECT_ID('dbo.CUSTOMER_VOUCHER', 'U') IS NOT NULL DELETE FROM dbo.[CUSTOMER_VOUCHER];
GO

-- 3. ENSURE ROLES EXIST
IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_ADMIN')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description]) VALUES (N'ROLE_ADMIN', N'ADMIN', N'System Administrator');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_MANAGER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description]) VALUES (N'ROLE_MANAGER', N'MANAGER', N'Cinema Manager');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_STAFF')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description]) VALUES (N'ROLE_STAFF', N'STAFF', N'Cinema Staff');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_CUSTOMER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description]) VALUES (N'ROLE_CUSTOMER', N'CUSTOMER', N'Customer Account');
GO

-- 4. ENSURE THE 3 USERS EXIST (admin@gmail.com, manager@gmail.com, staff@gmail.com)
DECLARE @PasswordHash NVARCHAR(500) = N'PBKDF2-SHA256.100000.AQIDBAUGBwgJCgsMDQ4PEA==.oA2tfdpk85oVNGhL7KlOtwvlkGWr0Id6feUHpwxbbjI='; -- Password1

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = N'admin@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified], [createdAt])
    VALUES (N'U_ADMIN_001', N'ROLE_ADMIN', N'admin@gmail.com', @PasswordHash, N'System Admin', N'ACTIVE', 1, SYSUTCDATETIME());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = N'manager@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified], [createdAt])
    VALUES (N'U_MANAGER_001', N'ROLE_MANAGER', N'manager@gmail.com', @PasswordHash, N'Cinema Manager', N'ACTIVE', 1, SYSUTCDATETIME());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = N'staff@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified], [createdAt])
    VALUES (N'U_STAFF_001', N'ROLE_STAFF', N'staff@gmail.com', @PasswordHash, N'Cinema Staff', N'ACTIVE', 1, SYSUTCDATETIME());
END;
GO

-- 5. DELETE OTHER USERS' DEPENDENT DATA & TOKENS
IF OBJECT_ID('dbo.EMAIL_VERIFICATION_TOKEN', 'U') IS NOT NULL
    DELETE FROM dbo.[EMAIL_VERIFICATION_TOKEN] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.EMAIL_VERIFICATION', 'U') IS NOT NULL
    DELETE FROM dbo.[EMAIL_VERIFICATION] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.PASSWORD_RESET_TOKEN', 'U') IS NOT NULL
    DELETE FROM dbo.[PASSWORD_RESET_TOKEN] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.REFRESH_TOKEN', 'U') IS NOT NULL
    DELETE FROM dbo.[REFRESH_TOKEN] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.VOUCHER_RESERVATION', 'U') IS NOT NULL
    DELETE FROM dbo.[VOUCHER_RESERVATION] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.NOTIFICATION_RECIPIENT', 'U') IS NOT NULL
    DELETE FROM dbo.[NOTIFICATION_RECIPIENT] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.CUSTOMER_PROFILE', 'U') IS NOT NULL
    DELETE FROM dbo.[CUSTOMER_PROFILE] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.STAFF_PROFILE', 'U') IS NOT NULL
    DELETE FROM dbo.[STAFF_PROFILE] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));

IF OBJECT_ID('dbo.MANAGER_PROFILE', 'U') IS NOT NULL
    DELETE FROM dbo.[MANAGER_PROFILE] WHERE [userId] NOT IN (SELECT [userId] FROM dbo.[USER] WHERE [email] IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com'));
GO

-- 6. DELETE USERS EXCEPT THE 3 KEPT USERS
DELETE FROM dbo.[USER] WHERE [email] NOT IN (N'admin@gmail.com', N'manager@gmail.com', N'staff@gmail.com');
GO

-- 7. RE-ENABLE ALL FOREIGN KEY CONSTRAINTS
EXEC sp_MSforeachtable "ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL";
GO

PRINT N'SUCCESS: Cleaned up DB! Retained only admin@gmail.com, manager@gmail.com, staff@gmail.com.';
