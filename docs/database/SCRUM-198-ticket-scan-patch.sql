SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.CHECKIN_LOG', 'scannedByUserId') IS NULL
    BEGIN
        ALTER TABLE dbo.CHECKIN_LOG
            ADD scannedByUserId NVARCHAR(50) NULL;
    END;

    UPDATE checkInLog
    SET scannedByUserId = staffProfile.userId
    FROM dbo.CHECKIN_LOG AS checkInLog
    INNER JOIN dbo.STAFF_PROFILE AS staffProfile
        ON staffProfile.staffProfileId = checkInLog.staffProfileId
    WHERE checkInLog.scannedByUserId IS NULL;

    IF EXISTS (
        SELECT 1
        FROM dbo.CHECKIN_LOG
        WHERE scannedByUserId IS NULL
    )
    BEGIN
        THROW 50001,
            'CHECKIN_LOG contains rows that cannot be mapped to a scanning user.',
            1;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.CHECKIN_LOG')
          AND name = 'scannedByUserId'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.CHECKIN_LOG
            ALTER COLUMN scannedByUserId NVARCHAR(50) NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.CHECKIN_LOG')
          AND name = 'staffProfileId'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dbo.CHECKIN_LOG
            ALTER COLUMN staffProfileId NVARCHAR(50) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('dbo.CHECKIN_LOG')
          AND name = 'FK_CHECKIN_LOG_SCANNED_BY_USER'
    )
    BEGIN
        ALTER TABLE dbo.CHECKIN_LOG
            ADD CONSTRAINT FK_CHECKIN_LOG_SCANNED_BY_USER
            FOREIGN KEY (scannedByUserId)
            REFERENCES dbo.[USER](userId);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.CHECKIN_LOG')
          AND name = 'IX_CHECKIN_LOG_SCANNED_BY_USER_TIME'
    )
    BEGIN
        CREATE INDEX IX_CHECKIN_LOG_SCANNED_BY_USER_TIME
            ON dbo.CHECKIN_LOG(scannedByUserId, scanTime);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
