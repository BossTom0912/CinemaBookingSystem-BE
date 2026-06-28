/*
============================================================
Admin schema alignment patch
============================================================
Purpose:
- Normalize MOVIE genre/language data without losing legacy values.
- Add the Admin movie, review, chatbot, and view-tracking schema expected by
  the current EF Core model.
- Repair earlier Sprint 2 column-name mismatches in an idempotent transaction.

This patch is safe to rerun against CinemaBookingDB.
============================================================
*/

USE [CinemaBookingDB];

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.GENRE', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[GENRE] (
            [genreId] INT IDENTITY(1,1) NOT NULL
                CONSTRAINT [PK_GENRE] PRIMARY KEY,
            [name] NVARCHAR(100) NOT NULL
        );
    END;

    IF OBJECT_ID(N'dbo.LANGUAGE', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[LANGUAGE] (
            [languageId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_LANGUAGE] PRIMARY KEY,
            [name] NVARCHAR(100) NOT NULL
        );
    END;

    INSERT INTO dbo.[LANGUAGE] ([languageId], [name])
    SELECT seed.[languageId], seed.[name]
    FROM (VALUES
        (N'VN', N'Tiếng Việt'),
        (N'EN_SUB_VN', N'Tiếng Anh phụ đề tiếng Việt'),
        (N'EN_DUB_VN', N'Tiếng Anh lồng tiếng Việt'),
        (N'KR_SUB_VN', N'Tiếng Hàn phụ đề tiếng Việt'),
        (N'JP_SUB_VN', N'Tiếng Nhật phụ đề tiếng Việt'),
        (N'TH_SUB_VN', N'Tiếng Thái phụ đề tiếng Việt'),
        (N'CN_SUB_VN', N'Tiếng Trung phụ đề tiếng Việt')
    ) AS seed([languageId], [name])
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.[LANGUAGE] AS existing
        WHERE existing.[languageId] = seed.[languageId]
    );

    INSERT INTO dbo.[GENRE] ([name])
    SELECT seed.[name]
    FROM (VALUES
        (N'Hành động'), (N'Hài hước'), (N'Kinh dị'),
        (N'Khoa học viễn tưởng'), (N'Tâm lý - Tình cảm'),
        (N'Hoạt hình'), (N'Tài liệu'), (N'Phiêu lưu'),
        (N'Võ thuật'), (N'Cổ trang'), (N'Kiếm hiệp'),
        (N'Gia đình'), (N'Hình sự'), (N'Trinh thám'),
        (N'Viễn tây (Western)'), (N'Nhạc kịch (Musical)'),
        (N'Thể thao'), (N'Sinh tồn'), (N'Hậu tận thế'),
        (N'Lịch sử'), (N'Tiểu sử'), (N'Thần thoại'),
        (N'Kỳ ảo (Fantasy)'), (N'Trào phúng (Satire)'),
        (N'Hài đen (Black Comedy)'), (N'Lãng mạn hài (Rom-com)'),
        (N'Giật gân (Thriller)'), (N'Tâm lý tội phạm'),
        (N'Bí ẩn (Mystery)'), (N'Siêu anh hùng'),
        (N'Xác sống (Zombie)'), (N'Ma cà rồng'),
        (N'Kịch tính (Drama)'), (N'Thanh xuân'), (N'Ngôn tình'),
        (N'Đam mỹ'), (N'Bách hợp'), (N'Cung đấu'), (N'Gia đấu'),
        (N'Xuyên không'), (N'Trọng sinh'), (N'Tiên hiệp'),
        (N'Huyền huyễn'), (N'Dị giới'), (N'Mạt thế'),
        (N'Đua xe'), (N'Thảm họa'), (N'Quái vật'), (N'Không gian'),
        (N'Du hành thời gian'), (N'Tôn giáo'), (N'Chính trị'),
        (N'Chiến tranh'), (N'Phim độc lập (Indie)'),
        (N'Thể nghiệm (Experimental)'), (N'Kịch câm'),
        (N'Mafia - Xã hội đen'), (N'Anime'), (N'Live-action'),
        (N'Chuyển thể từ Game'), (N'Chuyển thể từ Tiểu thuyết'),
        (N'Ẩm thực'), (N'Pháp lý - Tòa án'), (N'Y khoa'),
        (N'Tình báo - Điệp viên'), (N'Nghệ thuật (Art House)'),
        (N'Khoa giáo'), (N'Phim tương tác'),
        (N'Tài liệu giả tưởng (Mockumentary)'),
        (N'Đâm chém (Slasher)'), (N'Film Noir'), (N'Neo-noir'),
        (N'Học đường'), (N'Tuổi mới lớn (Coming-of-age)'),
        (N'Bí ẩn giết người (Whodunit)'), (N'Giật gân tâm lý'),
        (N'Võ thuật hài'), (N'Phép thuật'), (N'Cyberpunk'),
        (N'Steampunk'), (N'Bi kịch'), (N'Võng du (Game thực tế ảo)'),
        (N'Đô thị tình duyên'), (N'Hào môn thế gia'),
        (N'Cưới trước yêu sau'), (N'Oan gia ngõ hẹp'),
        (N'Thanh mai trúc mã'), (N'Tình yêu công sở'),
        (N'Tình tay ba'), (N'Phản anh hùng (Anti-hero)'),
        (N'Khảo cổ học'), (N'Viễn tưởng kỳ ảo (Science Fantasy)'),
        (N'Nhạc kịch lãng mạn'), (N'Quái thú khổng lồ (Kaiju)'),
        (N'Săn tiền thưởng'), (N'Truy tìm kho báu'),
        (N'Thoát hiểm (Escape)'), (N'Hài kịch tình huống (Sitcom)'),
        (N'Phiêu lưu không gian'), (N'Lãng mạn bi kịch'),
        (N'Ám ảnh ma quỷ'), (N'Trừ tà'),
        (N'Dân gian truyền thuyết'), (N'Siêu nhiên'),
        (N'Huyền bí (Occult)'), (N'Mật mã - Giải đố'),
        (N'Nữ quyền'), (N'Tự truyện')
    ) AS seed([name])
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.[GENRE] AS existing
        WHERE existing.[name] = seed.[name]
    );

    IF COL_LENGTH(N'dbo.MOVIE', N'languageId') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [languageId] NVARCHAR(50) NULL;

    IF COL_LENGTH(N'dbo.MOVIE', N'director') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [director] NVARCHAR(200) NULL;

    IF OBJECT_ID(N'dbo.MOVIE_GENRE', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[MOVIE_GENRE] (
            [movieId] NVARCHAR(50) NOT NULL,
            [genreId] INT NOT NULL,
            CONSTRAINT [PK_MOVIE_GENRE] PRIMARY KEY ([movieId], [genreId]),
            CONSTRAINT [FK_MOVIE_GENRE_MOVIE]
                FOREIGN KEY ([movieId]) REFERENCES dbo.[MOVIE]([movieId])
                ON DELETE CASCADE,
            CONSTRAINT [FK_MOVIE_GENRE_GENRE]
                FOREIGN KEY ([genreId]) REFERENCES dbo.[GENRE]([genreId])
                ON DELETE CASCADE
        );
    END;

    IF COL_LENGTH(N'dbo.MOVIE', N'genre') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.[GENRE] ([name])
            SELECT DISTINCT token.[name]
            FROM dbo.[MOVIE] AS movie
            CROSS APPLY (
                SELECT LTRIM(RTRIM([value])) AS [name]
                FROM STRING_SPLIT(REPLACE(movie.[genre], N'';'', N'',''), N'','')
            ) AS token
            WHERE NULLIF(token.[name], N'''') IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.[GENRE] AS existing
                  WHERE existing.[name] = token.[name]
              );

            INSERT INTO dbo.[MOVIE_GENRE] ([movieId], [genreId])
            SELECT DISTINCT movie.[movieId], genre.[genreId]
            FROM dbo.[MOVIE] AS movie
            CROSS APPLY (
                SELECT LTRIM(RTRIM([value])) AS [name]
                FROM STRING_SPLIT(REPLACE(movie.[genre], N'';'', N'',''), N'','')
            ) AS token
            INNER JOIN dbo.[GENRE] AS genre ON genre.[name] = token.[name]
            WHERE NULLIF(token.[name], N'''') IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.[MOVIE_GENRE] AS existing
                  WHERE existing.[movieId] = movie.[movieId]
                    AND existing.[genreId] = genre.[genreId]
              );';
    END;

    IF COL_LENGTH(N'dbo.MOVIE', N'language') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE movie
            SET [languageId] =
                CASE
                    WHEN movie.[language] LIKE N''%Anh%phụ đề%''
                      OR movie.[language] LIKE N''%Anh%Phu de%'' THEN N''EN_SUB_VN''
                    WHEN movie.[language] LIKE N''%Anh%lồng tiếng%''
                      OR movie.[language] LIKE N''%Anh%Long tieng%'' THEN N''EN_DUB_VN''
                    WHEN movie.[language] LIKE N''%Hàn%'' THEN N''KR_SUB_VN''
                    WHEN movie.[language] LIKE N''%Nhật%'' THEN N''JP_SUB_VN''
                    WHEN movie.[language] LIKE N''%Thái%'' THEN N''TH_SUB_VN''
                    WHEN movie.[language] LIKE N''%Trung%'' THEN N''CN_SUB_VN''
                    WHEN movie.[language] LIKE N''%Việt%''
                      OR movie.[language] LIKE N''%Viet%'' THEN N''VN''
                    ELSE movie.[languageId]
                END
            FROM dbo.[MOVIE] AS movie
            WHERE movie.[language] IS NOT NULL;';
    END;

    EXEC sys.sp_executesql N'
        UPDATE movie
        SET [languageId] =
            CASE
                WHEN movie.[languageId] IN
                    (N''VN'', N''EN_SUB_VN'', N''EN_DUB_VN'', N''KR_SUB_VN'',
                     N''JP_SUB_VN'', N''TH_SUB_VN'', N''CN_SUB_VN'')
                    THEN movie.[languageId]
                WHEN movie.[languageId] LIKE N''%Anh%phụ đề%''
                  OR movie.[languageId] LIKE N''%Anh%Phu de%'' THEN N''EN_SUB_VN''
                WHEN movie.[languageId] LIKE N''%Anh%lồng tiếng%''
                  OR movie.[languageId] LIKE N''%Anh%Long tieng%'' THEN N''EN_DUB_VN''
                WHEN movie.[languageId] LIKE N''%Hàn%'' THEN N''KR_SUB_VN''
                WHEN movie.[languageId] LIKE N''%Nhật%'' THEN N''JP_SUB_VN''
                WHEN movie.[languageId] LIKE N''%Thái%'' THEN N''TH_SUB_VN''
                WHEN movie.[languageId] LIKE N''%Trung%'' THEN N''CN_SUB_VN''
                WHEN movie.[languageId] LIKE N''%Việt%''
                  OR movie.[languageId] LIKE N''%Viet%'' THEN N''VN''
                ELSE NULL
            END
        FROM dbo.[MOVIE] AS movie
        WHERE movie.[languageId] IS NOT NULL;';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] = N'FK_MOVIE_LANGUAGE'
          AND [parent_object_id] = OBJECT_ID(N'dbo.MOVIE')
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.[MOVIE] WITH CHECK
            ADD CONSTRAINT [FK_MOVIE_LANGUAGE]
                FOREIGN KEY ([languageId])
                REFERENCES dbo.[LANGUAGE]([languageId]);';
    END;

    IF COL_LENGTH(N'dbo.MOVIE', N'genre') IS NOT NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.[MOVIE] DROP COLUMN [genre];';

    IF COL_LENGTH(N'dbo.MOVIE', N'language') IS NOT NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.[MOVIE] DROP COLUMN [language];';

    IF COL_LENGTH(N'dbo.MOVIE', N'highlight') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [highlight] NVARCHAR(30) NULL;
    IF COL_LENGTH(N'dbo.MOVIE', N'viewCount') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [viewCount] INT NOT NULL
            CONSTRAINT [DF_MOVIE_VIEW_COUNT] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MOVIE', N'averageRating') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [averageRating] DECIMAL(3,2) NOT NULL
            CONSTRAINT [DF_MOVIE_AVERAGE_RATING] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MOVIE', N'totalReviews') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [totalReviews] INT NOT NULL
            CONSTRAINT [DF_MOVIE_TOTAL_REVIEWS] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MOVIE', N'totalViews') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [totalViews] INT NOT NULL
            CONSTRAINT [DF_MOVIE_TOTAL_VIEWS] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MOVIE', N'dailyViews') IS NULL
        ALTER TABLE dbo.[MOVIE] ADD [dailyViews] INT NOT NULL
            CONSTRAINT [DF_MOVIE_DAILY_VIEWS] DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.[USER]', N'spamViolationCount') IS NULL
        ALTER TABLE dbo.[USER] ADD [spamViolationCount] INT NOT NULL
            CONSTRAINT [DF_USER_SPAM_VIOLATION_COUNT] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.[USER]', N'isBlocked') IS NULL
        ALTER TABLE dbo.[USER] ADD [isBlocked] BIT NOT NULL
            CONSTRAINT [DF_USER_IS_BLOCKED] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.[USER]', N'blockedUntil') IS NULL
        ALTER TABLE dbo.[USER] ADD [blockedUntil] DATETIME2 NULL;

    IF COL_LENGTH(N'dbo.REVIEW', N'status') IS NULL
        ALTER TABLE dbo.[REVIEW] ADD [status] NVARCHAR(20) NOT NULL
            CONSTRAINT [DF_REVIEW_STATUS] DEFAULT (N'PENDING') WITH VALUES;
    IF COL_LENGTH(N'dbo.REVIEW', N'editCount') IS NULL
        ALTER TABLE dbo.[REVIEW] ADD [editCount] INT NOT NULL
            CONSTRAINT [DF_REVIEW_EDIT_COUNT] DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.REVIEW', N'rejectedReason') IS NULL
        ALTER TABLE dbo.[REVIEW] ADD [rejectedReason] NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.REVIEW', N'moderatedBy') IS NULL
        ALTER TABLE dbo.[REVIEW] ADD [moderatedBy] NVARCHAR(50) NULL;

    IF OBJECT_ID(N'dbo.MOVIE_VIEW_LOG', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[MOVIE_VIEW_LOG] (
            [movieViewLogId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_MOVIE_VIEW_LOG] PRIMARY KEY,
            [movieId] NVARCHAR(50) NOT NULL,
            [userId] NVARCHAR(50) NULL,
            [viewedAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_MOVIE_VIEW_LOG_VIEWED_AT] DEFAULT SYSUTCDATETIME(),
            [ipAddress] NVARCHAR(100) NULL,
            CONSTRAINT [FK_MOVIE_VIEW_LOG_MOVIE]
                FOREIGN KEY ([movieId]) REFERENCES dbo.[MOVIE]([movieId]),
            CONSTRAINT [FK_MOVIE_VIEW_LOG_USER]
                FOREIGN KEY ([userId]) REFERENCES dbo.[USER]([userId])
        );
    END;

    IF COL_LENGTH(N'dbo.MOVIE_VIEW_LOG', N'movieViewLogId') IS NULL
       AND COL_LENGTH(N'dbo.MOVIE_VIEW_LOG', N'viewLogId') IS NOT NULL
        EXEC sys.sp_rename N'dbo.MOVIE_VIEW_LOG.viewLogId', N'movieViewLogId', N'COLUMN';

    IF COL_LENGTH(N'dbo.MOVIE_VIEW_LOG', N'viewedAt') IS NULL
       AND COL_LENGTH(N'dbo.MOVIE_VIEW_LOG', N'viewTime') IS NOT NULL
        EXEC sys.sp_rename N'dbo.MOVIE_VIEW_LOG.viewTime', N'viewedAt', N'COLUMN';

    IF OBJECT_ID(N'dbo.MOVIE_DAILY_VIEW', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[MOVIE_DAILY_VIEW] (
            [dailyViewId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_MOVIE_DAILY_VIEW] PRIMARY KEY,
            [movieId] NVARCHAR(50) NOT NULL,
            [viewDate] DATE NOT NULL,
            [viewCount] INT NOT NULL
                CONSTRAINT [DF_MOVIE_DAILY_VIEW_COUNT] DEFAULT (0),
            CONSTRAINT [UQ_MOVIE_DAILY_VIEW] UNIQUE ([movieId], [viewDate]),
            CONSTRAINT [FK_MOVIE_DAILY_VIEW_MOVIE]
                FOREIGN KEY ([movieId]) REFERENCES dbo.[MOVIE]([movieId])
        );
    END;

    IF OBJECT_ID(N'dbo.CHAT_HISTORY', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[CHAT_HISTORY] (
            [chatHistoryId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_CHAT_HISTORY] PRIMARY KEY,
            [userId] NVARCHAR(50) NULL,
            [userMessage] NVARCHAR(MAX) NOT NULL,
            [aiReplyMessage] NVARCHAR(MAX) NOT NULL,
            [createdAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_CHAT_HISTORY_CREATED_AT] DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [FK_CHAT_HISTORY_USER]
                FOREIGN KEY ([userId]) REFERENCES dbo.[USER]([userId])
        );
    END;

    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'userMessage') IS NULL
        ALTER TABLE dbo.[CHAT_HISTORY] ADD [userMessage] NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'aiReplyMessage') IS NULL
        ALTER TABLE dbo.[CHAT_HISTORY] ADD [aiReplyMessage] NVARCHAR(MAX) NULL;

    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'message') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE dbo.[CHAT_HISTORY]
            SET [userMessage] =
                    CASE WHEN [isUserMessage] = 1 THEN [message] ELSE N'''' END,
                [aiReplyMessage] =
                    CASE WHEN [isUserMessage] = 0 THEN [message] ELSE N'''' END
            WHERE [userMessage] IS NULL OR [aiReplyMessage] IS NULL;';
    END;

    EXEC sys.sp_executesql N'
        UPDATE dbo.[CHAT_HISTORY]
        SET [userMessage] = COALESCE([userMessage], N''''),
            [aiReplyMessage] = COALESCE([aiReplyMessage], N'''')
        WHERE [userMessage] IS NULL OR [aiReplyMessage] IS NULL;

        ALTER TABLE dbo.[CHAT_HISTORY]
            ALTER COLUMN [userMessage] NVARCHAR(MAX) NOT NULL;
        ALTER TABLE dbo.[CHAT_HISTORY]
            ALTER COLUMN [aiReplyMessage] NVARCHAR(MAX) NOT NULL;';

    DECLARE @dropChatDefaults NVARCHAR(MAX) = N'';
    SELECT @dropChatDefaults +=
        N'ALTER TABLE dbo.[CHAT_HISTORY] DROP CONSTRAINT '
        + QUOTENAME(defaultConstraint.[name]) + N';'
    FROM sys.default_constraints AS defaultConstraint
    INNER JOIN sys.columns AS columnDefinition
        ON columnDefinition.[object_id] = defaultConstraint.[parent_object_id]
       AND columnDefinition.[column_id] = defaultConstraint.[parent_column_id]
    WHERE defaultConstraint.[parent_object_id] = OBJECT_ID(N'dbo.CHAT_HISTORY')
      AND columnDefinition.[name] IN (N'sessionId', N'message', N'isUserMessage');
    IF @dropChatDefaults <> N''
        EXEC sys.sp_executesql @dropChatDefaults;

    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'sessionId') IS NOT NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.[CHAT_HISTORY] DROP COLUMN [sessionId];';
    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'message') IS NOT NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.[CHAT_HISTORY] DROP COLUMN [message];';
    IF COL_LENGTH(N'dbo.CHAT_HISTORY', N'isUserMessage') IS NOT NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.[CHAT_HISTORY] DROP COLUMN [isUserMessage];';

    IF OBJECT_ID(N'dbo.REVIEW_EDIT_HISTORY', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[REVIEW_EDIT_HISTORY] (
            [reviewEditHistoryId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_REVIEW_EDIT_HISTORY] PRIMARY KEY,
            [reviewId] NVARCHAR(50) NOT NULL,
            [oldRating] INT NOT NULL,
            [newRating] INT NOT NULL,
            [oldComment] NVARCHAR(1000) NULL,
            [newComment] NVARCHAR(1000) NULL,
            [editedAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_REVIEW_EDIT_HISTORY_EDITED_AT] DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [FK_REVIEW_EDIT_HISTORY_REVIEW]
                FOREIGN KEY ([reviewId]) REFERENCES dbo.[REVIEW]([reviewId])
        );
    END;

    IF COL_LENGTH(N'dbo.REVIEW_EDIT_HISTORY', N'reviewEditHistoryId') IS NULL
       AND COL_LENGTH(N'dbo.REVIEW_EDIT_HISTORY', N'editHistoryId') IS NOT NULL
        EXEC sys.sp_rename
            N'dbo.REVIEW_EDIT_HISTORY.editHistoryId',
            N'reviewEditHistoryId',
            N'COLUMN';

    IF OBJECT_ID(N'dbo.REVIEW_MODERATION_HISTORY', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.[REVIEW_MODERATION_HISTORY] (
            [moderationHistoryId] NVARCHAR(50) NOT NULL
                CONSTRAINT [PK_REVIEW_MODERATION_HISTORY] PRIMARY KEY,
            [reviewId] NVARCHAR(50) NOT NULL,
            [oldStatus] NVARCHAR(30) NULL,
            [newStatus] NVARCHAR(30) NOT NULL,
            [moderatorId] NVARCHAR(50) NULL,
            [rejectedReason] NVARCHAR(1000) NULL,
            [moderatedAt] DATETIME2 NOT NULL
                CONSTRAINT [DF_REVIEW_MODERATION_HISTORY_MODERATED_AT] DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [FK_REVIEW_MODERATION_HISTORY_REVIEW]
                FOREIGN KEY ([reviewId]) REFERENCES dbo.[REVIEW]([reviewId]),
            CONSTRAINT [FK_REVIEW_MODERATION_HISTORY_USER]
                FOREIGN KEY ([moderatorId]) REFERENCES dbo.[USER]([userId])
        );
    END;

    IF COL_LENGTH(N'dbo.REVIEW_MODERATION_HISTORY', N'moderatorId') IS NULL
       AND COL_LENGTH(N'dbo.REVIEW_MODERATION_HISTORY', N'moderatedBy') IS NOT NULL
        EXEC sys.sp_rename
            N'dbo.REVIEW_MODERATION_HISTORY.moderatedBy',
            N'moderatorId',
            N'COLUMN';

    IF COL_LENGTH(N'dbo.REVIEW_MODERATION_HISTORY', N'rejectedReason') IS NULL
       AND COL_LENGTH(N'dbo.REVIEW_MODERATION_HISTORY', N'reason') IS NOT NULL
        EXEC sys.sp_rename
            N'dbo.REVIEW_MODERATION_HISTORY.reason',
            N'rejectedReason',
            N'COLUMN';

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'UX_REVIEW_BOOKING'
          AND [object_id] = OBJECT_ID(N'dbo.REVIEW')
    )
        CREATE UNIQUE INDEX [UX_REVIEW_BOOKING]
            ON dbo.[REVIEW]([bookingId])
            WHERE [bookingId] IS NOT NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_REVIEW_EDIT_HISTORY_REVIEW_ID'
          AND [object_id] = OBJECT_ID(N'dbo.REVIEW_EDIT_HISTORY')
    )
        CREATE INDEX [IX_REVIEW_EDIT_HISTORY_REVIEW_ID]
            ON dbo.[REVIEW_EDIT_HISTORY]([reviewId]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_REVIEW_MODERATION_HISTORY_REVIEW_ID'
          AND [object_id] = OBJECT_ID(N'dbo.REVIEW_MODERATION_HISTORY')
    )
        CREATE INDEX [IX_REVIEW_MODERATION_HISTORY_REVIEW_ID]
            ON dbo.[REVIEW_MODERATION_HISTORY]([reviewId]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_MOVIE_DAILY_VIEW_DATE'
          AND [object_id] = OBJECT_ID(N'dbo.MOVIE_DAILY_VIEW')
    )
        CREATE INDEX [IX_MOVIE_DAILY_VIEW_DATE]
            ON dbo.[MOVIE_DAILY_VIEW]([viewDate]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_MOVIE_HIGHLIGHT_VIEWS'
          AND [object_id] = OBJECT_ID(N'dbo.MOVIE')
    )
        CREATE INDEX [IX_MOVIE_HIGHLIGHT_VIEWS]
            ON dbo.[MOVIE]([highlight], [totalViews] DESC);

    COMMIT TRANSACTION;
    PRINT 'Admin schema alignment patch applied successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
