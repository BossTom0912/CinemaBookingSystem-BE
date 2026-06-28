using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Data;

public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseMaintenanceService(CinemaDbContext dbContext, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureSchemaCompatibilityAsync(cancellationToken);
    }

    public Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        return DbInitializer.SeedAsync(_serviceProvider, isDevelopment);
    }

    private Task EnsureSchemaCompatibilityAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[MOVIE]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.MOVIE', N'averageRating') IS NULL
                    ALTER TABLE [dbo].[MOVIE] ADD [averageRating] DECIMAL(3,2) NOT NULL CONSTRAINT [DF_MOVIE_averageRating] DEFAULT 0.0;

                IF COL_LENGTH(N'dbo.MOVIE', N'totalReviews') IS NULL
                    ALTER TABLE [dbo].[MOVIE] ADD [totalReviews] INT NOT NULL CONSTRAINT [DF_MOVIE_totalReviews] DEFAULT 0;

                IF COL_LENGTH(N'dbo.MOVIE', N'totalViews') IS NULL
                    ALTER TABLE [dbo].[MOVIE] ADD [totalViews] INT NOT NULL CONSTRAINT [DF_MOVIE_totalViews] DEFAULT 0;

                IF COL_LENGTH(N'dbo.MOVIE', N'dailyViews') IS NULL
                    ALTER TABLE [dbo].[MOVIE] ADD [dailyViews] INT NOT NULL CONSTRAINT [DF_MOVIE_dailyViews] DEFAULT 0;

                IF COL_LENGTH(N'dbo.MOVIE', N'viewCount') IS NULL
                    ALTER TABLE [dbo].[MOVIE] ADD [viewCount] INT NOT NULL CONSTRAINT [DF_MOVIE_viewCount] DEFAULT 0;
            END;

            IF OBJECT_ID(N'[dbo].[REVIEW]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.REVIEW', N'status') IS NULL
                    ALTER TABLE [dbo].[REVIEW] ADD [status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_REVIEW_status] DEFAULT N'PENDING';

                IF COL_LENGTH(N'dbo.REVIEW', N'editCount') IS NULL
                    ALTER TABLE [dbo].[REVIEW] ADD [editCount] INT NOT NULL CONSTRAINT [DF_REVIEW_editCount] DEFAULT 0;

                IF COL_LENGTH(N'dbo.REVIEW', N'moderatedBy') IS NULL
                    ALTER TABLE [dbo].[REVIEW] ADD [moderatedBy] NVARCHAR(50) NULL;

                IF COL_LENGTH(N'dbo.REVIEW', N'rejectedReason') IS NULL
                    ALTER TABLE [dbo].[REVIEW] ADD [rejectedReason] NVARCHAR(500) NULL;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_REVIEW_STATUS'
                        AND parent_object_id = OBJECT_ID(N'[dbo].[REVIEW]')
                )
                    EXEC(N'ALTER TABLE [dbo].[REVIEW] WITH NOCHECK
                    ADD CONSTRAINT [CK_REVIEW_STATUS]
                    CHECK ([status] IN (N''Pending'', N''Approved'', N''Rejected'', N''Flagged'', N''PENDING'', N''APPROVED'', N''REJECTED'', N''FLAGGED''))');
            END;
            """;

        return _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
