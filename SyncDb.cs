using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=localhost;Database=CinemaBookingDB;User Id=sa;Password=12345;TrustServerCertificate=True;";
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MOVIE]') AND name = 'averageRating')
                    ALTER TABLE [MOVIE] ADD [averageRating] DECIMAL(3,2) NOT NULL DEFAULT 0.0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MOVIE]') AND name = 'totalReviews')
                    ALTER TABLE [MOVIE] ADD [totalReviews] INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MOVIE]') AND name = 'totalViews')
                    ALTER TABLE [MOVIE] ADD [totalViews] INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MOVIE]') AND name = 'dailyViews')
                    ALTER TABLE [MOVIE] ADD [dailyViews] INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MOVIE]') AND name = 'viewCount')
                    ALTER TABLE [MOVIE] ADD [viewCount] INT NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[REVIEW]') AND name = 'editCount')
                    ALTER TABLE [REVIEW] ADD [editCount] INT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[REVIEW]') AND name = 'moderatedBy')
                    ALTER TABLE [REVIEW] ADD [moderatedBy] NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[REVIEW]') AND name = 'rejectedReason')
                    ALTER TABLE [REVIEW] ADD [rejectedReason] NVARCHAR(500) NULL;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[CHAT_HISTORY]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [CHAT_HISTORY] (
                        [chatHistoryId] NVARCHAR(50) PRIMARY KEY,
                        [userId] NVARCHAR(50) NULL,
                        [userMessage] NVARCHAR(MAX) NOT NULL,
                        [aiReplyMessage] NVARCHAR(MAX) NOT NULL,
                        [createdAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MOVIE_DAILY_VIEW]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MOVIE_DAILY_VIEW] (
                        [movieId] NVARCHAR(50) NOT NULL,
                        [viewDate] DATE NOT NULL,
                        [viewCount] INT NOT NULL DEFAULT 0,
                        CONSTRAINT [PK_MOVIE_DAILY_VIEW] PRIMARY KEY ([movieId], [viewDate])
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MOVIE_VIEW_LOG]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MOVIE_VIEW_LOG] (
                        [movieViewLogId] NVARCHAR(50) PRIMARY KEY,
                        [movieId] NVARCHAR(50) NOT NULL,
                        [userId] NVARCHAR(50) NULL,
                        [viewedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        [ipAddress] NVARCHAR(100) NULL
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[REVIEW_EDIT_HISTORY]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [REVIEW_EDIT_HISTORY] (
                        [reviewEditHistoryId] NVARCHAR(50) PRIMARY KEY,
                        [reviewId] NVARCHAR(50) NOT NULL,
                        [oldRating] INT NOT NULL,
                        [newRating] INT NOT NULL,
                        [oldComment] NVARCHAR(1000) NULL,
                        [newComment] NVARCHAR(1000) NULL,
                        [editedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[REVIEW_MODERATION_HISTORY]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [REVIEW_MODERATION_HISTORY] (
                        [moderationHistoryId] NVARCHAR(50) PRIMARY KEY,
                        [reviewId] NVARCHAR(50) NOT NULL,
                        [oldStatus] NVARCHAR(30) NULL,
                        [newStatus] NVARCHAR(30) NOT NULL,
                        [moderatorId] NVARCHAR(50) NULL,
                        [rejectedReason] NVARCHAR(1000) NULL,
                        [moderatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END
            ";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        Console.WriteLine("Database schema synchronized.");
    }
}
