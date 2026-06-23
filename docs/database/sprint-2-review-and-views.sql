-- Add view tracking and highlight features
ALTER TABLE [MOVIE] ADD [viewCount] INT NOT NULL DEFAULT 0;

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

-- Highlight constraint (optional, but good for data integrity)
-- Highlight values: 'HOT', 'NEW', 'TRENDING'
ALTER TABLE [MOVIE] ADD CONSTRAINT [CK_MOVIE_HIGHLIGHT] 
    CHECK ([highlight] IS NULL OR [highlight] IN ('HOT', 'NEW', 'TRENDING'));
GO

-- Review constraints were mostly handled, but we explicitly make sure
-- We already have UX_REVIEW_BOOKING in cinema-booking-schema.sql
