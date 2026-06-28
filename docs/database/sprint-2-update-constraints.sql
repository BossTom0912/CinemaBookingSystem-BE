/*
============================================================
Sprint 2 - status constraint alignment patch
============================================================
Adds statuses required by the Admin re-seat/time-change flows.
This patch is safe to rerun against CinemaBookingDB.
============================================================
*/

USE [CinemaBookingDB];

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.SHOWTIME', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.check_constraints
            WHERE [name] = N'CK_SHOWTIME_STATUS'
              AND [parent_object_id] = OBJECT_ID(N'dbo.SHOWTIME')
        )
            ALTER TABLE dbo.[SHOWTIME] DROP CONSTRAINT [CK_SHOWTIME_STATUS];

        ALTER TABLE dbo.[SHOWTIME] WITH CHECK
        ADD CONSTRAINT [CK_SHOWTIME_STATUS]
        CHECK ([status] IN
            ('OPEN', 'CLOSED', 'CANCELLED', 'COMPLETED',
             'SUSPENDED', 'PROCESSING_UNSTABLE'));
    END;

    IF OBJECT_ID(N'dbo.BOOKING', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.check_constraints
            WHERE [name] = N'CK_BOOKING_STATUS'
              AND [parent_object_id] = OBJECT_ID(N'dbo.BOOKING')
        )
            ALTER TABLE dbo.[BOOKING] DROP CONSTRAINT [CK_BOOKING_STATUS];

        ALTER TABLE dbo.[BOOKING] WITH CHECK
        ADD CONSTRAINT [CK_BOOKING_STATUS]
        CHECK ([bookingStatus] IN
            ('CREATED', 'PENDING_PAYMENT', 'PAID', 'CANCELLED',
             'REFUND_PENDING', 'REFUNDED', 'COMPLETED',
             'PROCESSING_UNSTABLE'));
    END;

    IF OBJECT_ID(N'dbo.MOVIE', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.check_constraints
            WHERE [name] = N'CK_MOVIE_STATUS'
              AND [parent_object_id] = OBJECT_ID(N'dbo.MOVIE')
        )
            ALTER TABLE dbo.[MOVIE] DROP CONSTRAINT [CK_MOVIE_STATUS];

        ALTER TABLE dbo.[MOVIE] WITH CHECK
        ADD CONSTRAINT [CK_MOVIE_STATUS]
        CHECK ([movieStatus] IN
            ('COMING_SOON', 'NOW_SHOWING', 'ENDED', 'INACTIVE', 'ARCHIVED'));
    END;

    COMMIT TRANSACTION;
    PRINT 'Sprint 2 status constraints aligned successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
