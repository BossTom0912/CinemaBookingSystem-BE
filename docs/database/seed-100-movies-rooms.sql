SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =========================================================================
-- SYSTEM RESET & SEED DATA SCRIPT: 100 MOVIES & 100 ROOMS
-- =========================================================================

-- 1. DELETE EXISTING TRANSACTIONAL SEED DATA (In correct dependency order)
PRINT 'Clearing existing booking, payment, ticket, refund, and showtime tables...';

DELETE FROM [TICKET];
DELETE FROM [BOOKING_SEAT];
DELETE FROM [BOOKING_FB_ITEM];
DELETE FROM [REFUND];
DELETE FROM [PAYMENT];
DELETE FROM [VOUCHER_USAGE];
DELETE FROM [BOOKING];
DELETE FROM [SHOWTIME_SEAT];
DELETE FROM [SHOWTIME_CANCELLATION];
DELETE FROM [SHOWTIME];

PRINT 'Transaction and showtime tables cleared successfully.';

-- 2. SEED 100 MOVIES
PRINT 'Seeding 100 movies...';

DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    DECLARE @movieId NVARCHAR(50) = 'MOV_SEED_' + RIGHT('000' + CAST(@i AS NVARCHAR(10)), 3);
    DECLARE @title NVARCHAR(255) = 
        CASE (@i % 10)
            WHEN 0 THEN N'Chiến Binh Ánh Sáng ' + CAST(@i AS NVARCHAR(10))
            WHEN 1 THEN N'Mật Mã Vô Cực ' + CAST(@i AS NVARCHAR(10))
            WHEN 2 THEN N'Kẻ Kiến Tạo Giấc Mơ ' + CAST(@i AS NVARCHAR(10))
            WHEN 3 THEN N'Chuyến Tàu Định Mệnh ' + CAST(@i AS NVARCHAR(10))
            WHEN 4 THEN N'Ảo Ảnh Không Gian ' + CAST(@i AS NVARCHAR(10))
            WHEN 5 THEN N'Bí Mật Dưới Lòng Đất ' + CAST(@i AS NVARCHAR(10))
            WHEN 6 THEN N'Thành Phố Sương Mù ' + CAST(@i AS NVARCHAR(10))
            WHEN 7 THEN N'Đấu Sĩ Cuối Cùng ' + CAST(@i AS NVARCHAR(10))
            WHEN 8 THEN N'Truy Tìm Kho Báu ' + CAST(@i AS NVARCHAR(10))
            ELSE N'Vương Quốc Bị Lãng Quên ' + CAST(@i AS NVARCHAR(10))
        END;
    DECLARE @duration INT = 90 + (@i % 45); -- Duration between 90 and 134 minutes
    DECLARE @languageId NVARCHAR(50) = 
        CASE (@i % 5)
            WHEN 0 THEN 'VN'
            WHEN 1 THEN 'EN_SUB_VN'
            WHEN 2 THEN 'EN_DUB_VN'
            WHEN 3 THEN 'KR_SUB_VN'
            ELSE 'JP_SUB_VN'
        END;
    DECLARE @ageRating NVARCHAR(30) = 
        CASE (@i % 4)
            WHEN 0 THEN 'P'
            WHEN 1 THEN 'T13'
            WHEN 2 THEN 'T16'
            ELSE 'T18'
        END;
    DECLARE @highlight NVARCHAR(30) = 
        CASE (@i % 10)
            WHEN 1 THEN 'HOT'
            WHEN 2 THEN 'NEW'
            WHEN 3 THEN 'TRENDING'
            ELSE NULL
        END;
    DECLARE @movieStatus NVARCHAR(30) = 
        CASE (@i % 20)
            WHEN 0 THEN 'COMING_SOON'
            ELSE 'NOW_SHOWING'
        END;

    DECLARE @posterUrl NVARCHAR(500) = 
        CASE (@i % 10)
            WHEN 0 THEN 'https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=500&q=80'
            WHEN 1 THEN 'https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&q=80'
            WHEN 2 THEN 'https://images.unsplash.com/photo-1542204172-e7052809a850?w=500&q=80'
            WHEN 3 THEN 'https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=500&q=80'
            WHEN 4 THEN 'https://images.unsplash.com/photo-1478720568477-152d9b164e26?w=500&q=80'
            WHEN 5 THEN 'https://images.unsplash.com/photo-1440404653325-ab127d49abc1?w=500&q=80'
            WHEN 6 THEN 'https://images.unsplash.com/photo-1513151233558-d860c5398176?w=500&q=80'
            WHEN 7 THEN 'https://images.unsplash.com/photo-1524985069026-dd2f40d9b752?w=500&q=80'
            WHEN 8 THEN 'https://images.unsplash.com/photo-1574267432553-4b4628081c31?w=500&q=80'
            ELSE 'https://images.unsplash.com/photo-1626814026160-2237a95fc5a0?w=500&q=80'
        END;
        
    DECLARE @trailerUrl NVARCHAR(500) = 
        CASE (@i % 10)
            WHEN 0 THEN 'https://www.youtube.com/watch?v=Ke1Y334tPn8'
            WHEN 1 THEN 'https://www.youtube.com/watch?v=gq2xKJXYZ80'
            WHEN 2 THEN 'https://www.youtube.com/watch?v=8Qn_spdM5Zg'
            WHEN 3 THEN 'https://www.youtube.com/watch?v=d9MyW72ELq0'
            WHEN 4 THEN 'https://www.youtube.com/watch?v=aWzlQ2N6ecg'
            WHEN 5 THEN 'https://www.youtube.com/watch?v=UaVTIH8ujWY'
            WHEN 6 THEN 'https://www.youtube.com/watch?v=TcMBFSGVi1c'
            WHEN 7 THEN 'https://www.youtube.com/watch?v=JfVOs4VSpmA'
            WHEN 8 THEN 'https://www.youtube.com/watch?v=Go8nDbRyMQP'
            ELSE 'https://www.youtube.com/watch?v=dQw4w9WgXcQ'
        END;

    IF NOT EXISTS (SELECT 1 FROM [MOVIE] WHERE [movieId] = @movieId)
    BEGIN
        INSERT INTO [MOVIE] (
            [movieId], [title], [durationMinutes], [languageId], [releaseDate],
            [ageRating], [description], [posterUrl], [trailerUrl], [director],
            [highlight], [movieStatus], [viewCount], [averageRating], [totalReviews],
            [totalViews], [dailyViews]
        )
        VALUES (
            @movieId,
            @title,
            @duration,
            @languageId,
            DATEADD(DAY, -@i, CAST(GETDATE() AS DATE)),
            @ageRating,
            N'Description for ' + @title + N'. This is a seeded test movie.',
            @posterUrl,
            @trailerUrl,
            N'Director ' + CAST(@i AS NVARCHAR(10)),
            @highlight,
            @movieStatus,
            0, 0.00, 0, 0, 0
        );
    END

    SET @i = @i + 1;
END;

PRINT '100 movies seeded successfully.';

-- 3. SEED 100 ROOMS
PRINT 'Seeding 100 rooms...';

DECLARE @j INT = 1;
WHILE @j <= 100
BEGIN
    DECLARE @roomId NVARCHAR(50) = 'RM_SEED_' + RIGHT('000' + CAST(@j AS NVARCHAR(10)), 3);
    DECLARE @cinemaId NVARCHAR(50) = 
        CASE WHEN (@j % 2 = 0) THEN 'CIN_ND_Q1' ELSE 'CIN_BH_DN' END;
    DECLARE @roomName NVARCHAR(100) = 
        CASE WHEN (@j % 2 = 0) 
            THEN N'Room ' + CAST(@j AS NVARCHAR(10)) + N' - Q1'
            ELSE N'Room ' + CAST(@j AS NVARCHAR(10)) + N' - BH'
        END;
    DECLARE @capacity INT = 30 + (@j % 30); -- Capacity between 30 and 59 seats
    DECLARE @roomStatus NVARCHAR(30) = 'ACTIVE';

    IF NOT EXISTS (SELECT 1 FROM [ROOM] WHERE [roomId] = @roomId)
    BEGIN
        INSERT INTO [ROOM] (
            [roomId], [cinemaId], [roomName], [capacity], [roomStatus]
        )
        VALUES (
            @roomId,
            @cinemaId,
            @roomName,
            @capacity,
            @roomStatus
        );
    END

    SET @j = @j + 1;
END;

PRINT '100 rooms seeded successfully.';
GO
