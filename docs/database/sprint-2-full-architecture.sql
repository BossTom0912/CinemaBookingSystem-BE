-- ==========================================
-- SPRINT 2 FULL ARCHITECTURE: REVIEWS, VIEWS & HIGHLIGHTS
-- ==========================================

-- 1. Modify MOVIE table for Rating Aggregation and View Tracking
ALTER TABLE [MOVIE] ADD [averageRating] DECIMAL(3,2) NOT NULL DEFAULT 0.0;
ALTER TABLE [MOVIE] ADD [totalReviews] INT NOT NULL DEFAULT 0;
ALTER TABLE [MOVIE] ADD [totalViews] INT NOT NULL DEFAULT 0;
ALTER TABLE [MOVIE] ADD [dailyViews] INT NOT NULL DEFAULT 0;

-- 2. Modify REVIEW table constraints (already has status, but ensuring proper constraints)
ALTER TABLE [REVIEW] ADD CONSTRAINT [CK_REVIEW_STATUS] 
    CHECK ([status] IN ('PENDING', 'APPROVED', 'REJECTED', 'FLAGGED'));

-- 3. Review Edit History Table
CREATE TABLE [REVIEW_EDIT_HISTORY] (
    [reviewEditHistoryId] NVARCHAR(50) PRIMARY KEY,
    [reviewId] NVARCHAR(50) NOT NULL,
    [oldRating] INT NOT NULL,
    [newRating] INT NOT NULL,
    [oldComment] NVARCHAR(1000) NULL,
    [newComment] NVARCHAR(1000) NULL,
    [editedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT [FK_REVIEW_EDIT_HISTORY_REVIEW] FOREIGN KEY ([reviewId]) REFERENCES [REVIEW]([reviewId])
);
GO

-- 4. Review Moderation History Table
CREATE TABLE [REVIEW_MODERATION_HISTORY] (
    [moderationHistoryId] NVARCHAR(50) PRIMARY KEY,
    [reviewId] NVARCHAR(50) NOT NULL,
    [oldStatus] NVARCHAR(30) NULL,
    [newStatus] NVARCHAR(30) NOT NULL,
    [moderatorId] NVARCHAR(50) NULL, -- 'SYSTEM_AI' or Staff UserId
    [rejectedReason] NVARCHAR(1000) NULL,
    [moderatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [FK_REVIEW_MODERATION_HISTORY_REVIEW] FOREIGN KEY ([reviewId]) REFERENCES [REVIEW]([reviewId])
);
GO

-- 5. Historical View Analytics Table
CREATE TABLE [MOVIE_DAILY_VIEW] (
    [movieId] NVARCHAR(50) NOT NULL,
    [viewDate] DATE NOT NULL,
    [viewCount] INT NOT NULL DEFAULT 0,

    CONSTRAINT [PK_MOVIE_DAILY_VIEW] PRIMARY KEY ([movieId], [viewDate]),
    CONSTRAINT [FK_MOVIE_DAILY_VIEW_MOVIE] FOREIGN KEY ([movieId]) REFERENCES [MOVIE]([movieId])
);
GO

-- 6. Indexes for Performance & Scalability
CREATE INDEX [IX_REVIEW_EDIT_HISTORY_REVIEW_ID] ON [REVIEW_EDIT_HISTORY]([reviewId]);
CREATE INDEX [IX_REVIEW_MODERATION_HISTORY_REVIEW_ID] ON [REVIEW_MODERATION_HISTORY]([reviewId]);
CREATE INDEX [IX_MOVIE_DAILY_VIEW_DATE] ON [MOVIE_DAILY_VIEW]([viewDate]);
CREATE INDEX [IX_MOVIE_HIGHLIGHT_VIEWS] ON [MOVIE]([highlight], [totalViews] DESC);
GO
