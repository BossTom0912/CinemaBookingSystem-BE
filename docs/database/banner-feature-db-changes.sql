-- ============================================================================
-- SCRIPT THÊM BẢNG BANNER (QUẢNG CÁO, BẮP NƯỚC, SỰ KIỆN RẠP)
-- ============================================================================

USE CinemaBookingDB;
GO

IF OBJECT_ID('dbo.BANNER', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.[BANNER] (
        [bannerId] VARCHAR(50) PRIMARY KEY,
        [title] NVARCHAR(200) NOT NULL,
        [imageUrl] NVARCHAR(1000) NOT NULL,
        [linkUrl] NVARCHAR(1000) NULL,
        [bannerType] VARCHAR(50) NOT NULL, -- MOVIE, PROMOTION, FOOD_BEVERAGE, SYSTEM
        [displayOrder] INT NOT NULL DEFAULT 0,
        [isActive] BIT NOT NULL DEFAULT 1,
        [createdAt] DATETIME NOT NULL DEFAULT GETDATE()
    );
    
    PRINT 'Da tao bang BANNER thanh cong.';
END
ELSE
BEGIN
    PRINT 'Bang BANNER da ton tai trong CSDL.';
END;
GO
