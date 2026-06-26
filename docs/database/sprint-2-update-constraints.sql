/*
============================================================
Sprint 2 - Update Check Constraints Patch
============================================================
This patch relaxes the existing CHECK constraints on SHOWTIME and BOOKING
tables to support the new re-seat and token-based time change flows.
It also fixes the MOVIE status constraint to support 'ARCHIVED'.

Run this script directly against your [CinemaBookingDB].
*/

USE [CinemaBookingDB];
GO

-- 1. Update SHOWTIME constraint
-- Drop old constraint
ALTER TABLE [SHOWTIME] DROP CONSTRAINT [CK_SHOWTIME_STATUS];
GO
-- Add new constraint with SUSPENDED and PROCESSING_UNSTABLE
ALTER TABLE [SHOWTIME] ADD CONSTRAINT [CK_SHOWTIME_STATUS]
CHECK ([status] IN ('OPEN', 'CLOSED', 'CANCELLED', 'COMPLETED', 'SUSPENDED', 'PROCESSING_UNSTABLE'));
GO

-- 2. Update BOOKING constraint
-- Drop old constraint
ALTER TABLE [BOOKING] DROP CONSTRAINT [CK_BOOKING_STATUS];
GO
-- Add new constraint with PROCESSING_UNSTABLE
ALTER TABLE [BOOKING] ADD CONSTRAINT [CK_BOOKING_STATUS]
CHECK ([bookingStatus] IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'CANCELLED', 'REFUND_PENDING', 'REFUNDED', 'COMPLETED', 'PROCESSING_UNSTABLE'));
GO

-- 3. Update MOVIE constraint
-- Drop old constraint
ALTER TABLE [MOVIE] DROP CONSTRAINT [CK_MOVIE_STATUS];
GO
-- Add new constraint allowing ARCHIVED instead of just INACTIVE
ALTER TABLE [MOVIE] ADD CONSTRAINT [CK_MOVIE_STATUS]
CHECK ([movieStatus] IN ('COMING_SOON', 'NOW_SHOWING', 'ENDED', 'INACTIVE', 'ARCHIVED'));
GO

PRINT 'Sprint 2 constraints successfully applied!';
GO
