-- ==========================================
-- FILE: cinema-booking-schema.sql (DATA SEED ONLY)
-- ==========================================

USE [CinemaBookingDB];
GO

-- ==========================================
-- 1. SEED DICTIONARY & BASE DATA
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_CUSTOMER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_CUSTOMER', 'CUSTOMER', N'Customer account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_STAFF')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_STAFF', 'STAFF', N'Cinema staff account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_MANAGER')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_MANAGER', 'MANAGER', N'Cinema manager account');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = 'ROLE_ADMIN')
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES ('ROLE_ADMIN', 'ADMIN', N'System administrator account');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_NORMAL')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_NORMAL', 'NORMAL', 0.00);

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_VIP')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_VIP', 'VIP', 30000.00);

IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT_TYPE] WHERE [seatTypeId] = 'SEAT_TYPE_SWEETBOX')
    INSERT INTO dbo.[SEAT_TYPE] ([seatTypeId], [typeName], [extraFee])
    VALUES ('SEAT_TYPE_SWEETBOX', 'SWEETBOX', 50000.00);
GO

IF NOT EXISTS (SELECT 1 FROM [LANGUAGE])
BEGIN
    INSERT INTO [LANGUAGE] ([languageId], [name]) VALUES
    ('VN', N'Tiếng Việt'),
    ('EN_SUB_VN', N'Tiếng Anh phụ đề tiếng Việt'),
    ('EN_DUB_VN', N'Tiếng Anh lồng tiếng Việt'),
    ('KR_SUB_VN', N'Tiếng Hàn phụ đề tiếng Việt'),
    ('JP_SUB_VN', N'Tiếng Nhật phụ đề tiếng Việt'),
    ('TH_SUB_VN', N'Tiếng Thái phụ đề tiếng Việt'),
    ('CN_SUB_VN', N'Tiếng Trung phụ đề tiếng Việt');
END
GO

IF NOT EXISTS (SELECT 1 FROM [GENRE])
BEGIN
    INSERT INTO [GENRE] ([name]) VALUES
    (N'Hành động'), (N'Hài hước'), (N'Kinh dị'), (N'Khoa học viễn tưởng'), (N'Tâm lý - Tình cảm'), (N'Hoạt hình'),
    (N'Tài liệu'), (N'Phiêu lưu'), (N'Võ thuật'), (N'Cổ trang'), (N'Kiếm hiệp'), (N'Gia đình'), (N'Hình sự'),
    (N'Trinh thám'), (N'Viễn tây (Western)'), (N'Nhạc kịch (Musical)'), (N'Thể thao'), (N'Sinh tồn'),
    (N'Hậu tận thế'), (N'Lịch sử'), (N'Tiểu sử'), (N'Thần thoại'), (N'Kỳ ảo (Fantasy)'), (N'Trào phúng (Satire)'),
    (N'Hài đen (Black Comedy)'), (N'Lãng mạn hài (Rom-com)'), (N'Giật gân (Thriller)'), (N'Tâm lý tội phạm'),
    (N'Bí ẩn (Mystery)'), (N'Siêu anh hùng'), (N'Xác sống (Zombie)'), (N'Ma cà rồng'), (N'Kịch tính (Drama)'),
    (N'Thanh xuân'), (N'Ngôn tình'), (N'Đam mỹ'), (N'Bách hợp'), (N'Cung đấu'), (N'Gia đấu'), (N'Xuyên không'),
    (N'Trọng sinh'), (N'Tiên hiệp'), (N'Huyền huyễn'), (N'Dị giới'), (N'Mạt thế'), (N'Đua xe'), (N'Thảm họa'),
    (N'Quái vật'), (N'Không gian'), (N'Du hành thời gian'), (N'Tôn giáo'), (N'Chính trị'), (N'Chiến tranh'),
    (N'Phim độc lập (Indie)'), (N'Thể nghiệm (Experimental)'), (N'Kịch câm'), (N'Mafia - Xã hội đen'),
    (N'Anime'), (N'Live-action'), (N'Chuyển thể từ Game'), (N'Chuyển thể từ Tiểu thuyết'), (N'Ẩm thực'),
    (N'Pháp lý - Tòa án'), (N'Y khoa'), (N'Tình báo - Điệp viên'), (N'Nghệ thuật (Art House)'), (N'Khoa giáo'),
    (N'Phim tương tác'), (N'Tài liệu giả tưởng (Mockumentary)'), (N'Đâm chém (Slasher)'), (N'Film Noir'),
    (N'Neo-noir'), (N'Học trường'), (N'Tuổi mới lớn (Coming-of-age)'), (N'Bí ẩn giết người (Whodunit)'),
    (N'Giật gân tâm lý'), (N'Võ thuật hài'), (N'Phép thuật'), (N'Cyberpunk'), (N'Steampunk'), (N'Bi kịch'),
    (N'Võng du (Game thực tế ảo)'), (N'Đô thị tình duyên'), (N'Hào môn thế gia'), (N'Cưới trước yêu sau'),
    (N'Oan gia ngõ hẹp'), (N'Thanh mai trúc mã'), (N'Tình yêu công sở'), (N'Tình tay ba'),
    (N'Phản anh hùng (Anti-hero)'), (N'Khảo cổ học'), (N'Viễn tưởng kỳ ảo (Science Fantasy)'),
    (N'Nhạc kịch lãng mạn'), (N'Quái thú khổng lồ (Kaiju)'), (N'Săn tiền thưởng'), (N'Truy tìm kho báu'),
    (N'Thoát hiểm (Escape)'), (N'Hài kịch tình huống (Sitcom)'), (N'Phiêu lưu không gian'),
    (N'Lãng mạn bi kịch'), (N'Ám ảnh ma quỷ'), (N'Trừ tà'), (N'Dân gian truyền thuyết'), (N'Siêu nhiên'),
    (N'Huyền bí (Occult)'), (N'Mật mã - Giải đố'), (N'Nữ quyền'), (N'Tự truyện');
END
GO

-- ==========================================
-- 2. SEED CINEMAS & CINEMA ROOMS
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[CINEMA] WHERE [cinemaId] = 'CIN_ND_Q1')
    INSERT INTO dbo.[CINEMA] ([cinemaId], [cinemaName], [address], [city], [phoneNumber], [cinemaStatus])
    VALUES ('CIN_ND_Q1', N'Rap Nguyen Du - Quan 1', N'116 Nguyen Du, Phuong Ben Thanh, Quan 1', N'Ho Chi Minh', '02838273111', 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[CINEMA] WHERE [cinemaId] = 'CIN_BH_DN')
    INSERT INTO dbo.[CINEMA] ([cinemaId], [cinemaName], [address], [city], [phoneNumber], [cinemaStatus])
    VALUES ('CIN_BH_DN', N'Rap Bien Hoa - Dong Nai', N'Khu pho 2, Phuong Tan Tien, TP Bien Hoa', N'Dong Nai', '02513822111', 'ACTIVE');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM01')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM01', 'CIN_ND_Q1', N'Phong 1 - 2D Dolby', 40, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM02')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM02', 'CIN_ND_Q1', N'Phong 2 - 3D IMAX', 30, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM03')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM03', 'CIN_BH_DN', N'Phong VIP', 20, 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.[ROOM] WHERE [roomId] = 'RM04')
    INSERT INTO dbo.[ROOM] ([roomId], [cinemaId], [roomName], [capacity], [roomStatus])
    VALUES ('RM04', 'CIN_BH_DN', N'Phong Sweetbox', 12, 'ACTIVE');
GO

-- ==========================================
-- 3. SEED MOVIES & MOVIE GENRES
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DOCTOR_STRANGE_3')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DOCTOR_STRANGE_3', N'Doctor Strange 3', 120, 'EN_SUB_VN', '2026-05-01', 'T16',
         N'Phan phim tiep theo ve Phu Thuy Toi Thuong.',
         'https://image.example.com/doctor-strange-3.jpg',
         'https://youtube.com/watch?v=doctor-strange-3', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_LAT_MAT_8')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_LAT_MAT_8', N'Lat Mat 8', 115, 'VN', '2026-04-28', 'P',
         N'Tac pham dien anh moi voi cau chuyen gia dinh va hanh trinh hoa giai.',
         'https://image.example.com/lat-mat-8.jpg',
         'https://youtube.com/watch?v=lat-mat-8', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_AVENGERS_SECRET_WARS')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_AVENGERS_SECRET_WARS', N'Avengers: Secret Wars', 150, 'EN_SUB_VN', '2026-06-20', 'T13',
         N'Biet doi sieu anh hung doi mat moi de doa da vu tru.',
         'https://image.example.com/avengers-secret-wars.jpg',
         'https://youtube.com/watch?v=avengers-secret-wars', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DORAEMON_2026')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DORAEMON_2026', N'Doraemon Movie 2026', 105, 'VN', '2026-06-01', 'P',
         N'Doraemon va nhom ban trong chuyen phieu luu moi.',
         'https://image.example.com/doraemon-2026.jpg',
         'https://youtube.com/watch?v=doraemon-2026', 'NOW_SHOWING');
GO

IF NOT EXISTS (SELECT 1 FROM MOVIE_GENRE)
BEGIN
    INSERT INTO MOVIE_GENRE (movieId, genreId)
    SELECT 'MOV_DOCTOR_STRANGE_3', genreId FROM GENRE WHERE name IN (N'Hành động', N'Khoa học viễn tưởng');

    INSERT INTO MOVIE_GENRE (movieId, genreId)
    SELECT 'MOV_LAT_MAT_8', genreId FROM GENRE WHERE name IN (N'Hài hước', N'Gia đình', N'Tâm lý - Tình cảm');

    INSERT INTO MOVIE_GENRE (movieId, genreId)
    SELECT 'MOV_AVENGERS_SECRET_WARS', genreId FROM GENRE WHERE name IN (N'Hành động', N'Siêu anh hùng');

    INSERT INTO MOVIE_GENRE (movieId, genreId)
    SELECT 'MOV_DORAEMON_2026', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Phiêu lưu', N'Gia đình');
END
GO

-- ==========================================
-- 4. DYNAMIC SEAT GENERATION
-- ==========================================

DECLARE @RoomId NVARCHAR(50);
DECLARE @Rows TABLE ([rowLabel] NVARCHAR(10), [seatCount] INT);
DECLARE @RowLabel NVARCHAR(10);
DECLARE @SeatCount INT;
DECLARE @SeatNumber INT;
DECLARE @SeatTypeId NVARCHAR(50);
DECLARE @SeatId NVARCHAR(50);
DECLARE @SeatCode NVARCHAR(20);

DECLARE room_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT [roomId] FROM (VALUES ('RM01'), ('RM02'), ('RM03'), ('RM04')) AS seededRooms([roomId]);

OPEN room_cursor;
FETCH NEXT FROM room_cursor INTO @RoomId;

WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE FROM @Rows;

    IF @RoomId = 'RM01'
        INSERT INTO @Rows VALUES ('A', 10), ('B', 10), ('C', 10), ('D', 10);
    ELSE IF @RoomId = 'RM02'
        INSERT INTO @Rows VALUES ('A', 10), ('B', 10), ('C', 10);
    ELSE IF @RoomId = 'RM03'
        INSERT INTO @Rows VALUES ('A', 8), ('B', 8), ('C', 4);
    ELSE
        INSERT INTO @Rows VALUES ('S', 12);

    DECLARE row_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [rowLabel], [seatCount] FROM @Rows;

    OPEN row_cursor;
    FETCH NEXT FROM row_cursor INTO @RowLabel, @SeatCount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SeatNumber = 1;

        WHILE @SeatNumber <= @SeatCount
        BEGIN
            SET @SeatCode = CONCAT(@RowLabel, @SeatNumber);
            SET @SeatId = CONCAT('SEAT_', @RoomId, '_', @RowLabel, RIGHT(CONCAT('0', @SeatNumber), 2));
            SET @SeatTypeId =
                CASE
                    WHEN @RoomId = 'RM04' THEN 'SEAT_TYPE_SWEETBOX'
                    WHEN @RowLabel IN ('C', 'D', 'S') THEN 'SEAT_TYPE_VIP'
                    ELSE 'SEAT_TYPE_NORMAL'
                END;

            IF NOT EXISTS (SELECT 1 FROM dbo.[SEAT] WHERE [seatId] = @SeatId)
                INSERT INTO dbo.[SEAT]
                    ([seatId], [roomId], [seatTypeId], [seatCode], [rowLabel], [seatNumber], [isActive])
                VALUES
                    (@SeatId, @RoomId, @SeatTypeId, @SeatCode, @RowLabel, @SeatNumber, 1);

            SET @SeatNumber += 1;
        END;

        FETCH NEXT FROM row_cursor INTO @RowLabel, @SeatCount;
    END;

    CLOSE row_cursor;
    DEALLOCATE row_cursor;

    FETCH NEXT FROM room_cursor INTO @RoomId;
END;

CLOSE room_cursor;
DEALLOCATE room_cursor;
GO

-- ==========================================
-- 5. SEED SHOWTIMES & SHOWTIME SEATS
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW001')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW001', 'MOV_DOCTOR_STRANGE_3', 'RM01', '2026-07-01T10:00:00', '2026-07-01T12:15:00', 80000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW002')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW002', 'MOV_LAT_MAT_8', 'RM01', '2026-07-01T13:00:00', '2026-07-01T15:10:00', 85000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW003')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW003', 'MOV_AVENGERS_SECRET_WARS', 'RM02', '2026-07-01T14:30:00', '2026-07-01T17:15:00', 120000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW004')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW004', 'MOV_DORAEMON_2026', 'RM03', '2026-07-01T19:00:00', '2026-07-01T21:00:00', 90000.00, 'OPEN');

IF NOT EXISTS (SELECT 1 FROM dbo.[SHOWTIME] WHERE [showtimeId] = 'SHW005')
    INSERT INTO dbo.[SHOWTIME] ([showtimeId], [movieId], [roomId], [startTime], [endTime], [basePrice], [status])
    VALUES ('SHW005', 'MOV_DOCTOR_STRANGE_3', 'RM04', '2026-07-02T20:00:00', '2026-07-02T22:15:00', 150000.00, 'OPEN');
GO

INSERT INTO dbo.[SHOWTIME_SEAT] ([showtimeSeatId], [showtimeId], [seatId], [seatStatus], [lockedUntil], [lockedByUserId])
SELECT
    CONCAT('STS_', showtime.[showtimeId], '_', seat.[seatId]),
    showtime.[showtimeId],
    seat.[seatId],
    CASE
        WHEN showtime.[showtimeId] = 'SHW001' AND seat.[seatCode] IN ('A1', 'A2') THEN 'BOOKED'
        WHEN showtime.[showtimeId] = 'SHW001' AND seat.[seatCode] IN ('B1', 'B2') THEN 'UNAVAILABLE'
        WHEN showtime.[showtimeId] = 'SHW003' AND seat.[seatCode] IN ('C1', 'C2') THEN 'LOCKED'
        ELSE 'AVAILABLE'
    END,
    CASE
        WHEN showtime.[showtimeId] = 'SHW003' AND seat.[seatCode] IN ('C1', 'C2')
            THEN DATEADD(MINUTE, 10, SYSUTCDATETIME())
        ELSE NULL
    END,
    NULL
FROM dbo.[SHOWTIME] AS showtime
INNER JOIN dbo.[SEAT] AS seat
    ON seat.[roomId] = showtime.[roomId]
    AND seat.[isActive] = 1
WHERE showtime.[showtimeId] IN ('SHW001', 'SHW002', 'SHW003', 'SHW004', 'SHW005')
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.[SHOWTIME_SEAT] AS existing
        WHERE existing.[showtimeId] = showtime.[showtimeId]
            AND existing.[seatId] = seat.[seatId]
    );
GO

-- ==========================================
-- 6. SEED F&B MODULE & INVENTORY
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_POPCORN_PEPSI_L')
    INSERT INTO dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
    VALUES ('FB_POPCORN_PEPSI_L', N'Combo bap ngot va Pepsi lon', 75000.00, 'AVAILABLE');

IF NOT EXISTS (SELECT 1 FROM dbo.[FB_ITEM] WHERE [fbItemId] = 'FB_CHEESE_POPCORN_M')
    INSERT INTO dbo.[FB_ITEM] ([fbItemId], [itemName], [price], [itemStatus])
    VALUES ('FB_CHEESE_POPCORN_M', N'Bap pho mai co vua', 55000.00, 'AVAILABLE');

INSERT INTO dbo.[CINEMA_FB_INVENTORY] ([cinemaInventoryId], [cinemaId], [fbItemId], [quantity])
SELECT
    CONCAT('CFI_', cinema.[cinemaId], '_', item.[fbItemId]),
    cinema.[cinemaId],
    item.[fbItemId],
    500
FROM dbo.[CINEMA] AS cinema
CROSS JOIN dbo.[FB_ITEM] AS item
WHERE cinema.[cinemaStatus] = 'ACTIVE'
    AND item.[fbItemId] IN ('FB_POPCORN_PEPSI_L', 'FB_CHEESE_POPCORN_M')
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.[CINEMA_FB_INVENTORY] AS existing
        WHERE existing.[cinemaId] = cinema.[cinemaId]
            AND existing.[fbItemId] = item.[fbItemId]
    );

UPDATE inventory
SET [quantity] = 500
FROM dbo.[CINEMA_FB_INVENTORY] AS inventory
WHERE inventory.[fbItemId] IN ('FB_POPCORN_PEPSI_L', 'FB_CHEESE_POPCORN_M')
    AND inventory.[quantity] < 500;
GO

-- ==========================================
-- 7. SEED PAYMENT PROVIDERS & USERS
-- ==========================================

IF NOT EXISTS (SELECT 1 FROM dbo.[PAYMENT_PROVIDER] WHERE [paymentProviderId] = 'PP_SEPAY')
    INSERT INTO dbo.[PAYMENT_PROVIDER] ([paymentProviderId], [providerName], [apiEndpoint], [providerStatus])
    VALUES ('PP_SEPAY', 'SEPAY', 'https://my.sepay.vn', 'ACTIVE');
GO

DECLARE @CustomerRoleId NVARCHAR(50);
SELECT @CustomerRoleId = [roleId] FROM dbo.[ROLE] WHERE [roleName] = 'CUSTOMER';

IF @CustomerRoleId IS NULL
BEGIN
    SET @CustomerRoleId = 'ROLE_CUSTOMER';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = 'customer@gmail.com')
BEGIN
    INSERT INTO dbo.[USER]
        ([userId], [roleId], [email], [passwordHash], [fullName],
         [phoneNumber], [status], [emailVerified])
    VALUES
        ('U_CUST_01', @CustomerRoleId, 'customer@gmail.com',
         'AQAAAAEAACcQAAAAE...', N'Nguyen Tan Dung',
         '0901234567', 'ACTIVE', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[CUSTOMER_PROFILE] WHERE [userId] = 'U_CUST_01')
BEGIN
    INSERT INTO dbo.[CUSTOMER_PROFILE]
        ([customerProfileId], [userId], [memberLevel], [rewardPoints], [dateOfBirth])
    VALUES
        ('CP01', 'U_CUST_01', 'GOLD', 500, '2005-12-09');
END;
GO