USE [CinemaBookingDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- =========================
-- 12. DEVELOPMENT SEED DATA
-- =========================

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

INSERT INTO [LANGUAGE] ([languageId], [name]) VALUES
('VN', N'Tiếng Việt'),
('EN_SUB_VN', N'Tiếng Anh phụ đề tiếng Việt'),
('EN_DUB_VN', N'Tiếng Anh lồng tiếng Việt'),
('KR_SUB_VN', N'Tiếng Hàn phụ đề tiếng Việt'),
('JP_SUB_VN', N'Tiếng Nhật phụ đề tiếng Việt'),
('TH_SUB_VN', N'Tiếng Thái phụ đề tiếng Việt'),
('CN_SUB_VN', N'Tiếng Trung phụ đề tiếng Việt');
GO

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
GO

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

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DOCTOR_STRANGE_3')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DOCTOR_STRANGE_3', N'Doctor Strange: Phù thủy tối thượng', 120, 'EN_SUB_VN', '2026-05-01', 'T16',
         N'Phần phim tiếp theo về Phù Thủy Tối Thượng.',
         'https://upload.wikimedia.org/wikipedia/vi/thumb/c/c7/Doctor_Strange_poster.jpg/250px-Doctor_Strange_poster.jpg',
         'https://www.youtube.com/embed/aWzlQ2N6qqg', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_LAT_MAT_8')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_LAT_MAT_8', N'Lật Mặt 8: Vòng Tay Nắng', 115, 'VN', '2026-04-28', 'P',
         N'Tác phẩm điện ảnh mới với câu chuyện gia đình và hành trình hóa giải đầy cảm xúc.',
         'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQJjuvDhVkPTzqS3QvUztEkUAGRdjPWAf3-MaBJWRh0gg&s=10',
         'https://www.youtube.com/embed/S_B7yD3D6i0', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_AVENGERS_SECRET_WARS')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_AVENGERS_SECRET_WARS', N'Avengers: Cuộc Chiến Bí Mật', 150, 'EN_SUB_VN', '2026-06-20', 'T13',
         N'Biệt đội siêu anh hùng đối mặt mối đe dọa đa vũ trụ cực lớn.',
         'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTMC_dgyLzhZZpJqGiTzS_wbodFx6jVhQvaGN1P3yqx_g&s=10',
         'https://www.youtube.com/embed/6ZfuNTqbHE8', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_DORAEMON_2026')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_DORAEMON_2026', N'Doraemon Movie 2026: Nobita và Lâu Đài Dưới Đáy Biển', 105, 'VN', '2026-06-01', 'P',
         N'Doraemon và nhóm bạn trong chuyến phiêu lưu mới.',
         'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS4mui2l19-YYsbIIpRKpvmTKLFvp0RlxnEzR11HaOXiw&s=10',
         'https://www.youtube.com/embed/7V2K61h-VnE', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_MESDAMES_THANH_SAC')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_MESDAMES_THANH_SAC', N'Mesdames: Thanh Sắc', 125, 'VN', '2026-06-19', 'T18', 
         N'Đại mỹ nhân Cầm Thanh và Madame Sắc – bà chủ vũ trường Kim Đô giàu có tại Sài Gòn những năm 1960.',
         'https://cinestar.com.vn/_next/image/?url=https%3A%2F%2Fapi-website.cinestar.com.vn%2Fmedia%2Fwysiwyg%2FPosters%2F06-2026%2Fmesdames-thanh-sac.jpg&w=1920&q=75',
         'https://www.youtube.com/embed/c0R3q1L_H1o', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_MOANA_2')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_MOANA_2', N'Moana 2', 100, 'EN_SUB_VN', '2026-07-01', 'P', 
         N'Hành trình mới đầy phiêu lưu của Moana và á thần Maui vượt qua đại dương bao la.',
         'https://cinestar.com.vn/_next/image/?url=https%3A%2F%2Fapi-website.cinestar.com.vn%2Fmedia%2Fwysiwyg%2FPosters%2F07-2026%2Fmoana_1.jpg&w=1920&q=75',
         'https://www.youtube.com/embed/hDZ7y8RP5HE', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_CONAN_27')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_CONAN_27', N'Thám Tử Lừng Danh Conan: Ngôi Biệt Thự Ngôi Sao Năm Cánh', 110, 'VN', '2026-06-15', 'T13', 
         N'Hành trình phá án mới của Conan tại ngôi biệt thự bí ẩn hình ngôi sao năm cánh.',
         'https://vimages.coccoc.com/vimage?ns=cinema&url=https%3A%2F%2Figuov8nhvyobj.vcdn.cloud%2Fmedia%2Fcatalog%2Fproduct%2Fcache%2F1%2Fimage%2Fc5f0a1eff4c394a251036189ccddaacd%2Fp%2Fo%2Fposter_conan_movie_29.jpg',
         'https://www.youtube.com/embed/J7TzUaN55_k', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_MONG_VUOT')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_MONG_VUOT', N'Móng Vuốt', 95, 'VN', '2026-06-07', 'T18', 
         N'Cuộc đấu tranh sinh tồn nghẹt thở của một nhóm bạn trẻ chống lại ác thú trong rừng sâu.',
         'https://vimages.coccoc.com/vimage?ns=cinema&url=https%3A%2F%2Figuov8nhvyobj.vcdn.cloud%2Fmedia%2Fcatalog%2Fproduct%2Fcache%2F1%2Fimage%2Fc5f0a1eff4c394a251036189ccddaacd%2Fm%2Fn%2Fmn3_henryposter_470x700.jpg',
         'https://www.youtube.com/embed/x7nS2vD0F_E', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_RUNNING_MAN_2026')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_RUNNING_MAN_2026', N'Running Man Vietnam 2026: Chúa Tể Thời Gian', 105, 'VN', '2026-07-10', 'P', 
         N'Cuộc đua giải trí đầy kịch tính xoay quanh quyền năng thời gian của các thành viên Running Man Việt Nam.',
         'https://iguov8nhvyobj.vcdn.cloud/media/catalog/product/cache/1/image/c5f0a1eff4c394a251036189ccddaacd/r/m/rm26_mainposter_470x700.jpg',
         'https://www.youtube.com/embed/c0R3q1L_H1o', 'COMING_SOON');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_YOUR_NAME')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_YOUR_NAME', N'Your Name: Tên Cậu Là Gì?', 106, 'EN_SUB_VN', '2026-05-20', 'P', 
         N'Câu chuyện hoán đổi thân xác kỳ diệu và tình yêu vượt qua không gian thời gian giữa Mitsuha và Taki.',
         'https://vimages.coccoc.com/vimage?ns=cinema&url=https%3A%2F%2Figuov8nhvyobj.vcdn.cloud%2Fmedia%2Fcatalog%2Fproduct%2Fcache%2F1%2Fimage%2Fc5f0a1eff4c394a251036189ccddaacd%2Fy%2Fo%2Fyour_name_localized_adaptation_social_470_x_700.jpg',
         'https://www.youtube.com/embed/s0wtdJS_Dss', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_SPIRITED_AWAY')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_SPIRITED_AWAY', N'Spirited Away: Vùng Đất Linh Hồn', 125, 'VN', '2001-07-20', 'P', 
         N'Cô bé Chihiro bị lạc vào thế giới linh hồn ma thuật đầy bí ẩn và phải tìm cách giải cứu cha mẹ mình khỏi lời nguyền.',
         'https://image.tmdb.org/t/p/w500/3949ugCz4ChJga5fsdY6t6nj96c.jpg',
         'https://www.youtube.com/embed/ByXuk9QqQkk', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_INCEPTION')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_INCEPTION', N'Inception: Kẻ Đánh Cắp Giấc Mơ', 148, 'EN_SUB_VN', '2010-07-16', 'T16', 
         N'Một kẻ trộm chuyên nghiệp có khả năng xâm nhập vào tiềm thức của người khác thông qua giấc mơ để đánh cắp các bí mật kinh doanh.',
         'https://image.tmdb.org/t/p/w500/edv5CZv00w9ZavOORFIYTEHIQg8.jpg',
         'https://www.youtube.com/embed/8hP9D6kZseM', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_HOWLS_CASTLE')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_HOWLS_CASTLE', N'Lâu Đài Bay Của Pháp Sư Howl', 119, 'VN', '2004-11-20', 'P', 
         N'Sophie, một thợ làm mũ trẻ tuổi bị phù thủy nguyền rủa thành một bà lão, tìm đến lâu đài bay di động của pháp sư trẻ Howl.',
         'https://image.tmdb.org/t/p/w500/hbr7385W4Wntq6U92zXzQe9W126.jpg',
         'https://www.youtube.com/embed/iwRgUMy15-c', 'NOW_SHOWING');

IF NOT EXISTS (SELECT 1 FROM dbo.[MOVIE] WHERE [movieId] = 'MOV_SPIDER_MAN_SPIDER_VERSE')
    INSERT INTO dbo.[MOVIE]
        ([movieId], [title], [durationMinutes], [languageId], [releaseDate],
         [ageRating], [description], [posterUrl], [trailerUrl], [movieStatus])
    VALUES
        ('MOV_SPIDER_MAN_SPIDER_VERSE', N'Spider-Man: Du Hành Vũ Trụ Nhện', 140, 'EN_SUB_VN', '2023-06-02', 'T13', 
         N'Miles Morales tái hợp với Gwen Stacy và du hành qua đa vũ trụ nhện, chạm trán với một biệt đội Người Nhện bảo vệ đa vũ trụ.',
         'https://image.tmdb.org/t/p/w500/8Gxv2wSbsr07LI496Oa3j2P8Jkm.jpg',
         'https://www.youtube.com/embed/cqGjhVJWtEg', 'NOW_SHOWING');
GO

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_DOCTOR_STRANGE_3', genreId FROM GENRE WHERE name IN (N'Hành động', N'Khoa học viễn tưởng');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_LAT_MAT_8', genreId FROM GENRE WHERE name IN (N'Hài hước', N'Gia đình', N'Tâm lý - Tình cảm');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_AVENGERS_SECRET_WARS', genreId FROM GENRE WHERE name IN (N'Hành động', N'Siêu anh hùng');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_DORAEMON_2026', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Phiêu lưu', N'Gia đình');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_MESDAMES_THANH_SAC', genreId FROM GENRE WHERE name IN (N'Tâm lý - Tình cảm', N'Kịch tính (Drama)');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_MOANA_2', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Phiêu lưu', N'Gia đình');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_CONAN_27', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Trinh thám', N'Anime');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_MONG_VUOT', genreId FROM GENRE WHERE name IN (N'Sinh tồn', N'Giật gân (Thriller)');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_RUNNING_MAN_2026', genreId FROM GENRE WHERE name IN (N'Hài hước', N'Phiêu lưu');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_YOUR_NAME', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Tâm lý - Tình cảm', N'Anime');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_SPIRITED_AWAY', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Kỳ ảo (Fantasy)', N'Anime');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_INCEPTION', genreId FROM GENRE WHERE name IN (N'Hành động', N'Khoa học viễn tưởng', N'Giật gân (Thriller)');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_HOWLS_CASTLE', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Kỳ ảo (Fantasy)', N'Anime');

INSERT INTO MOVIE_GENRE (movieId, genreId)
SELECT 'MOV_SPIDER_MAN_SPIDER_VERSE', genreId FROM GENRE WHERE name IN (N'Hoạt hình', N'Hành động', N'Siêu anh hùng');
GO

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

IF NOT EXISTS (SELECT 1 FROM dbo.[PAYMENT_PROVIDER] WHERE [paymentProviderId] = 'PP_SEPAY')
    INSERT INTO dbo.[PAYMENT_PROVIDER] ([paymentProviderId], [providerName], [apiEndpoint], [providerStatus])
    VALUES ('PP_SEPAY', 'SEPAY', 'https://my.sepay.vn', 'ACTIVE');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'VCB')
    INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName])
    VALUES ('VCB', '970436', N'Vietcombank', N'Joint Stock Commercial Bank for Foreign Trade of Vietnam');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'MB')
    INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName])
    VALUES ('MB', '970422', N'MB Bank', N'Military Commercial Joint Stock Bank');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'TCB')
    INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName])
    VALUES ('TCB', '970407', N'Techcombank', N'Vietnam Technological and Commercial Joint Stock Bank');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'BIDV')
    INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName])
    VALUES ('BIDV', '970418', N'BIDV', N'Joint Stock Commercial Bank for Investment and Development of Vietnam');

IF NOT EXISTS (SELECT 1 FROM dbo.[BANK_DIRECTORY] WHERE [bankCode] = 'CTG')
    INSERT dbo.[BANK_DIRECTORY] ([bankCode], [bankBin], [shortName], [fullName])
    VALUES ('CTG', '970415', N'VietinBank', N'Vietnam Joint Stock Commercial Bank for Industry and Trade');
GO

-- CUSTOMER SEED
DECLARE @CustomerRoleId NVARCHAR(50);
SELECT @CustomerRoleId = [roleId] FROM dbo.[ROLE] WHERE [roleName] = 'CUSTOMER';

IF @CustomerRoleId IS NULL
BEGIN
    SET @CustomerRoleId = 'R01';
    INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
    VALUES (@CustomerRoleId, 'CUSTOMER', N'Khach hang mua ve online');
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

-- STAFF, MANAGER, ADMIN SEED
IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = 'staff@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified])
    VALUES ('USR_STAFF_01', 'ROLE_STAFF', 'staff@gmail.com', 'PBKDF2-SHA256.100000.rM95luc6yevV5JAh5Yveng==.MiL/DaQ9S+ZI0krUgdm0I1tNcVOx3cG2Hz5jOBEci/o=', N'Cinema Staff', 'ACTIVE', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[STAFF_PROFILE] WHERE [userId] = 'USR_STAFF_01')
BEGIN
    INSERT INTO dbo.[STAFF_PROFILE] ([staffProfileId], [userId], [cinemaId], [position], [employmentStatus])
    VALUES ('STF_STAFF_01', 'USR_STAFF_01', 'CIN_ND_Q1', N'Staff', 'ACTIVE');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = 'manager@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified])
    VALUES ('USR_MANAGER_01', 'ROLE_MANAGER', 'manager@gmail.com', 'PBKDF2-SHA256.100000.7RvWmkbRQePL7fvz3yx7bw==.r335C694AyWYhudChqRtnKaHezM3gSAvTPa2wGw6jlQ=', N'Cinema Manager', 'ACTIVE', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[STAFF_PROFILE] WHERE [userId] = 'USR_MANAGER_01')
BEGIN
    INSERT INTO dbo.[STAFF_PROFILE] ([staffProfileId], [userId], [cinemaId], [position], [employmentStatus])
    VALUES ('STF_MANAGER_01', 'USR_MANAGER_01', 'CIN_ND_Q1', N'Manager', 'ACTIVE');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[USER] WHERE [email] = 'admin@gmail.com')
BEGIN
    INSERT INTO dbo.[USER] ([userId], [roleId], [email], [passwordHash], [fullName], [status], [emailVerified])
    VALUES ('USR_ADMIN_01', 'ROLE_ADMIN', 'admin@gmail.com', 'PBKDF2-SHA256.100000.oFqe8oKlqqxZJvDiKLoKbw==.+k5Jt26QxcvQUrJE+xLPo/PnX5uuvD4JaE+QYzgdmrg=', N'System Admin', 'ACTIVE', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[STAFF_PROFILE] WHERE [userId] = 'USR_ADMIN_01')
BEGIN
    INSERT INTO dbo.[STAFF_PROFILE] ([staffProfileId], [userId], [cinemaId], [position], [employmentStatus])
    VALUES ('STF_ADMIN_01', 'USR_ADMIN_01', 'CIN_ND_Q1', N'Administrator', 'ACTIVE');
END;
GO

-- =========================
-- 13. QUICK VERIFICATION
-- =========================

SELECT
    DB_NAME() AS [databaseName],
    COUNT(*) AS [mergedTableCount]
FROM sys.tables
WHERE [name] IN (
    'BANK_DIRECTORY',
    'REFUND_CLAIM',
    'REFUND_CLAIM_TOKEN',
    'CUSTOMER_REFUND_REQUEST',
    'REFUND_PAYOUT_ATTEMPT',
    'MANUAL_REFUND_PROCESS',
    'EMAIL_OUTBOX',
    'CHAT_HISTORY',
    'MOVIE_VIEW_LOG',
    'MOVIE_DAILY_VIEW',
    'REVIEW_EDIT_HISTORY',
    'REVIEW_MODERATION_HISTORY'
);

SELECT
    t.[name] AS [tableName],
    c.[name] AS [columnName]
FROM sys.tables t
INNER JOIN sys.columns c ON c.[object_id] = t.[object_id]
WHERE
    (t.[name] = 'CHECKIN_LOG' AND c.[name] IN ('staffProfileId', 'scannedByUserId', 'rawQrCode'))
    OR (t.[name] = 'BOOKING' AND c.[name] IN ('fbFulfillmentStatus', 'fbFulfilledAt'))
    OR (t.[name] = 'MOVIE' AND c.[name] IN ('highlight', 'viewCount', 'averageRating', 'totalReviews', 'totalViews', 'dailyViews'))
    OR (t.[name] = 'USER' AND c.[name] IN ('spamViolationCount', 'isBlocked', 'blockedUntil'))
    OR (t.[name] = 'REVIEW' AND c.[name] IN ('bookingId', 'moderatedBy', 'editCount'))
ORDER BY t.[name], c.[name];

SELECT
    i.[name] AS [indexName],
    OBJECT_NAME(i.[object_id]) AS [tableName]
FROM sys.indexes i
WHERE i.[name] IN (
    'IX_CHECKIN_LOG_SCANNED_BY_USER_TIME',
    'IX_BOOKING_FB_FULFILLMENT_STATUS',
    'IX_REFUND_PAYOUT_ATTEMPT_REFUND_STATUS',
    'IX_EMAIL_OUTBOX_STATUS_NEXT_ATTEMPT',
    'IX_MOVIE_HIGHLIGHT_VIEWS',
    'IX_MOVIE_DAILY_VIEW_DATE',
    'UX_REVIEW_BOOKING'
)
ORDER BY OBJECT_NAME(i.[object_id]), i.[name];
GO
