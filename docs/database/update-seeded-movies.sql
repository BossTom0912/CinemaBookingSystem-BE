SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Clear existing genres for seeded movies
DELETE FROM [MOVIE_GENRE] WHERE [movieId] LIKE 'MOV_SEED_%';

-- 2. Update the 20 movies from the user's table (STT 21 to 40)
-- 21 (MOV_SEED_001)
UPDATE [MOVIE]
SET [title] = N'The Lord of the Rings: The Return of the King',
    [director] = N'Peter Jackson',
    [description] = N'Trận chiến cuối cùng quyết định vận mệnh của Trung Địa khi Frodo và Sam tiến gần đến Núi Diệt Vong để tiêu hủy Nhẫn Chúa.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/3/543d81b3cc3980df9a7970d2b9d248b3.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=r5X-hFf6Bwo',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_001';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_001', 23), ('MOV_SEED_001', 8);

-- 22 (MOV_SEED_002)
UPDATE [MOVIE]
SET [title] = N'Schindler''s List',
    [director] = N'Steven Spielberg',
    [description] = N'Câu chuyện có thật về Oskar Schindler, một nhà công nghiệp người Đức đã cứu sống hơn một ngàn người Do Thái trong thời kỳ Holocaust.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/2/815d9183416ca6fcda9278bdca5bfdcb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=gG22XNhtnoY',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_002';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_002', 20), ('MOV_SEED_002', 33);

-- 23 (MOV_SEED_003)
UPDATE [MOVIE]
SET [title] = N'Spirited Away',
    [director] = N'Hayao Miyazaki',
    [description] = N'Cô bé Chihiro lạc vào thế giới của những linh hồn và phải làm việc tại nhà tắm của phù thủy Yubaba để cứu cha mẹ mình.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/5/a90f11ca3b3b24fcbcfa28fdca5aebdb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=ByXuk9QqQkk',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_003';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_003', 6), ('MOV_SEED_003', 23);

-- 24 (MOV_SEED_004)
UPDATE [MOVIE]
SET [title] = N'Spider-Man: Into the Spider-Verse',
    [director] = N'Bob Persichetti',
    [description] = N'Cậu bé Miles Morales trở thành Người Nhện mới và hợp sức với các Người Nhện đến từ các chiều không gian khác để cứu vũ trụ.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/8/1de37a85dfca62febcea4efbca7fdecb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=tg52up16eq0',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_004';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_004', 6), ('MOV_SEED_004', 30);

-- 25 (MOV_SEED_005)
UPDATE [MOVIE]
SET [title] = N'Gladiator',
    [director] = N'Ridley Scott',
    [description] = N'Một vị tướng La Mã bị phản bội, gia đình bị sát hại và phải quay trở lại Rome dưới thân phận một võ sĩ giác đấu để trả thù.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/2/42f3bd5c95dfce7da67287dca1e9cbde.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=P5ieIbInFpg',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_005';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_005', 1), ('MOV_SEED_005', 20);

-- 26 (MOV_SEED_006)
UPDATE [MOVIE]
SET [title] = N'Parasite',
    [director] = N'Bong Joon Ho',
    [description] = N'Một gia đình nghèo khó dùng mưu mẹo để thâm nhập và ký sinh vào cuộc sống xa hoa của một gia đình thượng lưu tại Seoul.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/4/a7404a8bca2c6ce6fc72bece26e5fcde.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=5xH0HfJHsaY',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_006';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_006', 27), ('MOV_SEED_006', 25);

-- 27 (MOV_SEED_007)
UPDATE [MOVIE]
SET [title] = N'Django Unchained',
    [director] = N'Quentin Tarantino',
    [description] = N'Một nô lệ được trả tự do hợp tác với một thợ săn tiền thưởng người Đức để giải cứu vợ mình khỏi một chủ đồn điền tàn bạo.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/2/026d36e2f1e626e2e582862d64dfd482.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=0fUCuvNlOCg',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_007';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_007', 15), ('MOV_SEED_007', 1);

-- 28 (MOV_SEED_008)
UPDATE [MOVIE]
SET [title] = N'The Lion King',
    [director] = N'Roger Allers',
    [description] = N'Chú sư tử trẻ Simba vượt qua bi kịch mất cha và sự phản bội của người chú Scar để giành lại ngai vàng chính đáng của mình.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/1/817d23fcba3268579e276fdca21ea3cb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=lFzVJEksoDY',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_008';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_008', 6), ('MOV_SEED_008', 12);

-- 29 (MOV_SEED_009)
UPDATE [MOVIE]
SET [title] = N'Whiplash',
    [director] = N'Damien Chazelle',
    [description] = N'Cuộc đối đầu căng thẳng và cực đoan giữa một tay trống trẻ đầy tham vọng và một người thầy dạy nhạc vô cùng bạo lực, hà khắc.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/4/b817fa97fdceca2f8da79fbca8fe1fcb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=7d_jQycdQGo',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_009';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_009', 33), ('MOV_SEED_009', 16);

-- 30 (MOV_SEED_010)
UPDATE [MOVIE]
SET [title] = N'The Prestige',
    [director] = N'Christopher Nolan',
    [description] = N'Cuộc đối đầu thế kỷ mang tính thù hận giữa hai ảo thuật gia ở London nhằm tạo ra ảo ảnh tối thượng, bất chấp sự hy sinh.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/6/4a58ffbc3d49af4fbcd11f1de43fdcbc.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=o4gHCmTQDk4',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_010';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_010', 29), ('MOV_SEED_010', 27);

-- 31 (MOV_SEED_011)
UPDATE [MOVIE]
SET [title] = N'Leon: The Professional',
    [director] = N'Luc Besson',
    [description] = N'Một sát thủ chuyên nghiệp bất đắc dĩ phải bảo bọc một cô bé 12 tuổi sau khi gia đình cô bị một gã cảnh sát biến chất sát hại.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/0/815b3c58284fae24d36e2467fdcae5df.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=aNQonjuK9Xg',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_011';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_011', 1), ('MOV_SEED_011', 13);

-- 32 (MOV_SEED_012)
UPDATE [MOVIE]
SET [title] = N'The Departed',
    [director] = N'Martin Scorsese',
    [description] = N'Cuộc đấu trí căng thẳng giữa một cảnh sát chìm thâm nhập vào băng đảng mafia và một tên tội phạm được cài cắm vào sở cảnh sát.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/3/5ea8e7b93fd9a2bfbceca67fded3ebde.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=iojhqm0JTW4',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_012';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_012', 13), ('MOV_SEED_012', 27);

-- 33 (MOV_SEED_013)
UPDATE [MOVIE]
SET [title] = N'The Usual Suspects',
    [director] = N'Bryan Singer',
    [description] = N'Một người sống sót duy nhất sau vụ nổ súng tại bến cảng kể lại câu chuyện ly kỳ về một ông trùm tội phạm bí ẩn tên Keyser Söze.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/7/127fa6fcde9827cf9ebca25eec5dfecb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=oi5X2RiL7pA',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_013';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_013', 29), ('MOV_SEED_013', 13);

-- 34 (MOV_SEED_014)
UPDATE [MOVIE]
SET [title] = N'Your Name',
    [director] = N'Makoto Shinkai',
    [description] = N'Hai thiếu niên ở hai vùng địa lý khác nhau của Nhật Bản bất ngờ bị tráo đổi cơ thể cho nhau trong những giấc mơ.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/1/1a9db3cd3f39ef478da79fb6fcfa168a.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=3KR8_M-GWhY',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_014';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_014', 6), ('MOV_SEED_014', 5);

-- 35 (MOV_SEED_015)
UPDATE [MOVIE]
SET [title] = N'Joker',
    [director] = N'Todd Phillips',
    [description] = N'Câu chuyện độc lập đen tối lột tả quá trình tha hóa của Arthur Fleck, từ một gã hề tổn thương thành tội phạm Joker.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/9/a64edcfbcda357fbcea32ebfca6eacde.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=zAGVQLHvwOY',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_015';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_015', 28);

-- 36 (MOV_SEED_016)
UPDATE [MOVIE]
SET [title] = N'Coco',
    [director] = N'Lee Unkrich',
    [description] = N'Cậu bé Miguel đam mê âm nhạc vô tình bị đưa đến Vùng đất của người chết và khám phá ra bí mật chấn động của gia tộc.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/1/d61fa8ecba357efbcda4efbdca6ebfdf.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=xLNzRkTp5KI',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_016';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_016', 6), ('MOV_SEED_016', 12);

-- 37 (MOV_SEED_017)
UPDATE [MOVIE]
SET [title] = N'WALL-E',
    [director] = N'Andrew Stanton',
    [description] = N'Chú rô-bốt thu dọn rác cô đơn trên Trái Đất bị bỏ hoang vô tình bước vào một cuộc hành trình vũ trụ thay đổi số phận nhân loại.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/5/43777f98d7f90e5fc577170f2f3d6402.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=CZ1CATHerX4',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_017';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_017', 6), ('MOV_SEED_017', 4);

-- 38 (MOV_SEED_018)
UPDATE [MOVIE]
SET [title] = N'Mad Max: Fury Road',
    [director] = N'George Miller',
    [description] = N'Trên sa mạc hậu tận thế cằn cỗi, Max hợp sức với Furiosa để trốn chạy khỏi gã bạo chúa khét tiếng Immortan Joe.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/7/e54fa8ecfbda62febcea27fdea1ea7cb.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=hEJnMQG9ev8',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_018';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_018', 1), ('MOV_SEED_018', 19);

-- 39 (MOV_SEED_019)
UPDATE [MOVIE]
SET [title] = N'Toy Story 3',
    [director] = N'Lee Unkrich',
    [description] = N'Khi cậu chủ Andy chuẩn bị vào đại học, nhóm đồ chơi của Woody và Buzz Lightyear vô tình bị gửi nhầm đến một nhà trẻ đầy rẫy hiểm họa.',
    [posterUrl] = 'https://images.metacritic.com/products/movies/4/f31edcb90e3d2bfbaae029ddbca5e3df.jpg',
    [trailerUrl] = 'https://www.youtube.com/watch?v=JCPXckfT-6g',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_019';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_019', 6), ('MOV_SEED_019', 8);

-- 40 (MOV_SEED_020)
UPDATE [MOVIE]
SET [title] = N'Inception',
    [director] = N'Christopher Nolan',
    [description] = N'Một kẻ trộm chuyên nghiệp có khả năng xâm nhập vào giấc mơ của người khác để đánh cắp bí mật quốc gia.',
    [posterUrl] = 'https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=500&q=80',
    [trailerUrl] = 'https://www.youtube.com/watch?v=Ke1Y334tPn8',
    [movieStatus] = 'NOW_SHOWING'
WHERE [movieId] = 'MOV_SEED_020';

INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES ('MOV_SEED_020', 4), ('MOV_SEED_020', 1);


-- 3. Loop for seeded movies 21 to 100 to fix encoding/font errors
DECLARE @k INT = 21;
WHILE @k <= 100
BEGIN
    DECLARE @seedMovieId NVARCHAR(50) = 'MOV_SEED_' + RIGHT('000' + CAST(@k AS NVARCHAR(10)), 3);
    DECLARE @seedTitle NVARCHAR(255) = 
        CASE (@k % 10)
            WHEN 0 THEN N'Chiến Binh Ánh Sáng ' + CAST(@k AS NVARCHAR(10))
            WHEN 1 THEN N'Mật Mã Vô Cực ' + CAST(@k AS NVARCHAR(10))
            WHEN 2 THEN N'Kẻ Kiến Tạo Giấc Mơ ' + CAST(@k AS NVARCHAR(10))
            WHEN 3 THEN N'Chuyến Tàu Định Mệnh ' + CAST(@k AS NVARCHAR(10))
            WHEN 4 THEN N'Ảo Ảnh Không Gian ' + CAST(@k AS NVARCHAR(10))
            WHEN 5 THEN N'Bí Mật Dưới Lòng Đất ' + CAST(@k AS NVARCHAR(10))
            WHEN 6 THEN N'Thành Phố Sương Mù ' + CAST(@k AS NVARCHAR(10))
            WHEN 7 THEN N'Đấu Sĩ Cuối Cùng ' + CAST(@k AS NVARCHAR(10))
            WHEN 8 THEN N'Truy Tìm Kho Báu ' + CAST(@k AS NVARCHAR(10))
            ELSE N'Vương Quốc Bị Lãng Quên ' + CAST(@k AS NVARCHAR(10))
        END;
        
    UPDATE [MOVIE]
    SET [title] = @seedTitle,
        [description] = N'Mô tả cho bộ phim ' + @seedTitle + N'. Đây là phim thử nghiệm được seed tự động.',
        [director] = N'Đạo Diễn ' + CAST(@k AS NVARCHAR(10))
    WHERE [movieId] = @seedMovieId;
    
    -- Insert generic genres for the rest of movies so they don't have empty genres []
    DECLARE @gId1 INT = 1 + (@k % 5);
    DECLARE @gId2 INT = 6 + (@k % 5);
    INSERT INTO [MOVIE_GENRE] ([movieId], [genreId]) VALUES (@seedMovieId, @gId1), (@seedMovieId, @gId2);
    
    SET @k = @k + 1;
END;
PRINT 'Updated 100 seeded movies successfully with clean fonts and realistic details!';
GO
