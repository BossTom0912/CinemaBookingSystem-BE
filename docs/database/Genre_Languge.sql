    -- 1. Tạo bảng Danh mục Thể loại (GENRE)
    CREATE TABLE GENRE (
        genreId INT IDENTITY(1,1) PRIMARY KEY,
        name NVARCHAR(100) NOT NULL
    );

    -- 2. Tạo bảng trung gian (MOVIE_GENRE)
    CREATE TABLE MOVIE_GENRE (
        movieId NVARCHAR(50) NOT NULL,
        genreId INT NOT NULL,
        PRIMARY KEY (movieId, genreId),
        CONSTRAINT FK_MOVIE_GENRE_MOVIE FOREIGN KEY (movieId) REFERENCES MOVIE(movieId) ON DELETE CASCADE,
        CONSTRAINT FK_MOVIE_GENRE_GENRE FOREIGN KEY (genreId) REFERENCES GENRE(genreId) ON DELETE CASCADE
    );

    -- 3. Xóa bỏ cột Genre (dạng text cũ) trong bảng MOVIE
    ALTER TABLE MOVIE DROP COLUMN genre;

    -- 4. Thêm sẵn một số thể loại cơ bản (Seed Data)
    INSERT INTO GENRE (name) VALUES
    (N'Hành động'),
    (N'Hài hước'),
    (N'Kinh dị'),
    (N'Khoa học viễn tưởng'),
    (N'Tâm lý - Tình cảm'),
    (N'Hoạt hình'),
    (N'Tài liệu'),
    (N'Phiêu lưu'),
(N'Võ thuật'),
(N'Cổ trang'),
(N'Kiếm hiệp'),
(N'Gia đình'),
(N'Hình sự'),
(N'Trinh thám'),
(N'Viễn tây (Western)'),
(N'Nhạc kịch (Musical)'),
(N'Thể thao'),
(N'Sinh tồn'),
(N'Hậu tận thế'),
(N'Lịch sử'),
(N'Tiểu sử'),
(N'Thần thoại'),
(N'Kỳ ảo (Fantasy)'),
(N'Trào phúng (Satire)'),
(N'Hài đen (Black Comedy)'),
(N'Lãng mạn hài (Rom-com)'),
(N'Giật gân (Thriller)'),
(N'Tâm lý tội phạm'),
(N'Bí ẩn (Mystery)'),
(N'Siêu anh hùng'),
(N'Xác sống (Zombie)'),
(N'Ma cà rồng'),
(N'Kịch tính (Drama)'),
(N'Thanh xuân'),
(N'Ngôn tình'),
(N'Đam mỹ'),
(N'Bách hợp'),
(N'Cung đấu'),
(N'Gia đấu'),
(N'Xuyên không'),
(N'Trọng sinh'),
(N'Tiên hiệp'),
(N'Huyền huyễn'),
(N'Dị giới'),
(N'Mạt thế'),
(N'Đua xe'),
(N'Thảm họa'),
(N'Quái vật'),
(N'Không gian'),
(N'Du hành thời gian'),
(N'Tôn giáo'),
(N'Chính trị'),
(N'Chiến tranh'),
(N'Phim độc lập (Indie)'),
(N'Thể nghiệm (Experimental)'),
(N'Kịch câm'),
(N'Mafia - Xã hội đen'),
(N'Anime'),
(N'Live-action'),
(N'Chuyển thể từ Game'),
(N'Chuyển thể từ Tiểu thuyết'),
(N'Ẩm thực'),
(N'Pháp lý - Tòa án'),
(N'Y khoa'),
(N'Tình báo - Điệp viên'),
(N'Nghệ thuật (Art House)'),
(N'Khoa giáo'),
(N'Phim tương tác'),
(N'Tài liệu giả tưởng (Mockumentary)'),
(N'Đâm chém (Slasher)'),
(N'Film Noir'),
(N'Neo-noir'),
(N'Học đường'),
(N'Tuổi mới lớn (Coming-of-age)'),
(N'Bí ẩn giết người (Whodunit)'),
(N'Giật gân tâm lý'),
(N'Võ thuật hài'),
(N'Phép thuật'),
(N'Cyberpunk'),
(N'Steampunk'),
(N'Bi kịch'),
(N'Võng du (Game thực tế ảo)'),
(N'Đô thị tình duyên'),
(N'Hào môn thế gia'),
(N'Cưới trước yêu sau'),
(N'Oan gia ngõ hẹp'),
(N'Thanh mai trúc mã'),
(N'Tình yêu công sở'),
(N'Tình tay ba'),
(N'Phản anh hùng (Anti-hero)'),
(N'Khảo cổ học'),
(N'Viễn tưởng kỳ ảo (Science Fantasy)'),
(N'Nhạc kịch lãng mạn'),
(N'Quái thú khổng lồ (Kaiju)'),
(N'Săn tiền thưởng'),
(N'Truy tìm kho báu'),
(N'Thoát hiểm (Escape)'),
(N'Hài kịch tình huống (Sitcom)'),
(N'Phiêu lưu không gian'),
(N'Lãng mạn bi kịch'),
(N'Ám ảnh ma quỷ'),
(N'Trừ tà'),
(N'Dân gian truyền thuyết'),
(N'Siêu nhiên'),
(N'Huyền bí (Occult)'),
(N'Mật mã - Giải đố'),
(N'Nữ quyền'),
(N'Tự truyện');

    -- 1. Tạo bảng Danh mục Ngôn ngữ (LANGUAGE)
    CREATE TABLE LANGUAGE (
        languageId NVARCHAR(50) PRIMARY KEY,
        name NVARCHAR(100) NOT NULL
    );

    -- 2. Đổi tên cột `language` thành `languageId` trong bảng MOVIE
    EXEC sp_rename 'MOVIE.language', 'languageId', 'COLUMN';

    ALTER TABLE MOVIE ALTER COLUMN languageId NVARCHAR(50) NULL;

    -- 2. Gắn lại khóa ngoại từ MOVIE(languageId) sang LANGUAGE(languageId)
    ALTER TABLE MOVIE ADD CONSTRAINT FK_MOVIE_LANGUAGE FOREIGN KEY (languageId) REFERENCES LANGUAGE(languageId);

    -- 4. Thêm sẵn các ngôn ngữ cơ bản (Seed Data) vào bảng LANGUAGE
    INSERT INTO LANGUAGE (languageId, name) VALUES
    ('VN', N'Tiếng Việt'),
    ('EN_SUB_VN', N'Tiếng Anh phụ đề tiếng Việt'),
    ('EN_DUB_VN', N'Tiếng Anh lồng tiếng Việt'),
    ('KR_SUB_VN', N'Tiếng Hàn phụ đề tiếng Việt'),
    ('JP_SUB_VN', N'Tiếng Nhật phụ đề tiếng Việt'),
    ('TH_SUB_VN', N'Tiếng Thái phụ đề tiếng Việt'),
    ('CN_SUB_VN', N'Tiếng Trung phụ đề tiếng Việt');



	 ALTER TABLE MOVIE
    ADD Director NVARCHAR(200) NULL;