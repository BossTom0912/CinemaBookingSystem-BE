-- ==========================================
-- FILE: SCRUM_DB_Hotfix_Booking_FbFulfillment.sql
-- Purpose:
--   Fix runtime SQL error:
--     Invalid column name 'fbFulfilledAt'.
--     Invalid column name 'fbFulfillmentStatus'.
--
-- Safe for existing CinemaBookingDB.
-- This script does NOT drop data.
-- ==========================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [CinemaBookingDB];
GO

IF OBJECT_ID(N'dbo.BOOKING', N'U') IS NULL
BEGIN
    THROW 51001, 'Required table dbo.BOOKING does not exist. Run the full schema first.', 1;
END;
GO

IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfillmentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.[BOOKING]
        ADD [fbFulfillmentStatus] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_BOOKING_FB_FULFILLMENT_STATUS]
            DEFAULT N'NOT_REQUIRED' WITH VALUES;
END;
GO

IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfilledAt') IS NULL
BEGIN
    ALTER TABLE dbo.[BOOKING]
        ADD [fbFulfilledAt] DATETIME2 NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'dbo.BOOKING')
      AND [name] = N'IX_BOOKING_FB_FULFILLMENT_STATUS'
)
BEGIN
    CREATE INDEX [IX_BOOKING_FB_FULFILLMENT_STATUS]
        ON dbo.[BOOKING]([fbFulfillmentStatus]);
END;
GO

-- Verification output.
SELECT
    c.[name] AS [columnName],
    TYPE_NAME(c.[user_type_id]) AS [dataType],
    c.[max_length] AS [maxLength],
    c.[is_nullable] AS [isNullable]
FROM sys.columns AS c
WHERE c.[object_id] = OBJECT_ID(N'dbo.BOOKING')
  AND c.[name] IN (N'fbFulfillmentStatus', N'fbFulfilledAt')
ORDER BY c.[name];
GO

SELECT
    CASE
        WHEN COL_LENGTH(N'dbo.BOOKING', N'fbFulfillmentStatus') IS NOT NULL
         AND COL_LENGTH(N'dbo.BOOKING', N'fbFulfilledAt') IS NOT NULL
        THEN 'DB_HOTFIX_APPLIED=1'
        ELSE 'DB_HOTFIX_APPLIED=0'
    END AS [result];
GO
