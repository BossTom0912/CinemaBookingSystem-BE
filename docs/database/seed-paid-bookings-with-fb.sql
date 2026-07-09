USE [CinemaBookingDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    Seed ~15 paid customer bookings with tickets and F&B revenue for dashboard testing.

    What this script creates:
    - 15 ONLINE bookings for the two provided customer users.
    - 1 booked seat + 1 generated unused ticket per booking.
    - 1 successful payment per booking.
    - 1 F&B line item per booking.
    - SHOWTIME_SEAT rows are marked BOOKED.

    It uses existing MOVIE / SHOWTIME / ROOM / SEAT / SHOWTIME_SEAT data.
    If there are fewer than 15 available showtime seats, the script throws and rolls back.
*/

DECLARE @TicketCount int = 15;
DECLARE @PaymentProviderId nvarchar(50);
DECLARE @FbItemCount int;

BEGIN TRANSACTION;

BEGIN TRY
    -------------------------------------------------------------------------
    -- 1. Ensure the two provided customer users have customer profiles.
    -------------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM [dbo].[USER] WHERE [userId] = N'U_CUST_01')
       AND NOT EXISTS (SELECT 1 FROM [dbo].[CUSTOMER_PROFILE] WHERE [userId] = N'U_CUST_01')
    BEGIN
        INSERT INTO [dbo].[CUSTOMER_PROFILE]
            ([customerProfileId], [userId], [memberLevel], [rewardPoints], [dateOfBirth], [gender], [identityCard], [address], [avatarUrl])
        VALUES
            (N'CUS_SEED_U_CUST_01', N'U_CUST_01', N'STANDARD', 0, NULL, NULL, NULL, NULL, NULL);
    END;

    IF EXISTS (SELECT 1 FROM [dbo].[USER] WHERE [userId] = N'USR_f8f87b4cd65a4fcf8667468a89e29e37')
       AND NOT EXISTS (SELECT 1 FROM [dbo].[CUSTOMER_PROFILE] WHERE [userId] = N'USR_f8f87b4cd65a4fcf8667468a89e29e37')
    BEGIN
        INSERT INTO [dbo].[CUSTOMER_PROFILE]
            ([customerProfileId], [userId], [memberLevel], [rewardPoints], [dateOfBirth], [gender], [identityCard], [address], [avatarUrl])
        VALUES
            (N'CUS_SEED_USR_f8f87b4cd65a4fcf8667468a89e29e37', N'USR_f8f87b4cd65a4fcf8667468a89e29e37', N'STANDARD', 0, NULL, NULL, NULL, NULL, NULL);
    END;

    DECLARE @Customers table
    (
        [rowNo] int IDENTITY(1, 1) PRIMARY KEY,
        [customerProfileId] nvarchar(50) NOT NULL
    );

    INSERT INTO @Customers ([customerProfileId])
    SELECT [customerProfileId]
    FROM [dbo].[CUSTOMER_PROFILE]
    WHERE [userId] IN
    (
        N'U_CUST_01',
        N'USR_f8f87b4cd65a4fcf8667468a89e29e37'
    )
    ORDER BY [userId];

    IF NOT EXISTS (SELECT 1 FROM @Customers)
    BEGIN
        THROW 51001, 'No CUSTOMER_PROFILE found for the provided customer users.', 1;
    END;

    -------------------------------------------------------------------------
    -- 2. Ensure there is an active payment provider.
    -------------------------------------------------------------------------
    SELECT TOP (1)
        @PaymentProviderId = [paymentProviderId]
    FROM [dbo].[PAYMENT_PROVIDER]
    WHERE [providerStatus] = N'ACTIVE'
    ORDER BY
        CASE WHEN [providerName] = N'SEPAY' THEN 0 ELSE 1 END,
        [providerName];

    IF @PaymentProviderId IS NULL
    BEGIN
        SELECT TOP (1)
            @PaymentProviderId = [paymentProviderId]
        FROM [dbo].[PAYMENT_PROVIDER]
        WHERE [providerName] IN (N'SEPAY', N'SEPAY_SEED')
        ORDER BY CASE WHEN [providerName] = N'SEPAY' THEN 0 ELSE 1 END;

        IF @PaymentProviderId IS NULL
        BEGIN
            SET @PaymentProviderId = N'PAY_SEPAY_SEED';

            IF EXISTS (SELECT 1 FROM [dbo].[PAYMENT_PROVIDER] WHERE [paymentProviderId] = @PaymentProviderId)
            BEGIN
                UPDATE [dbo].[PAYMENT_PROVIDER]
                SET [providerStatus] = N'ACTIVE'
                WHERE [paymentProviderId] = @PaymentProviderId;
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[PAYMENT_PROVIDER]
                    ([paymentProviderId], [providerName], [apiEndpoint], [providerStatus])
                VALUES
                    (@PaymentProviderId, N'SEPAY_SEED', NULL, N'ACTIVE');
            END;
        END
        ELSE
        BEGIN
            UPDATE [dbo].[PAYMENT_PROVIDER]
            SET [providerStatus] = N'ACTIVE'
            WHERE [paymentProviderId] = @PaymentProviderId;
        END;
    END;

    -------------------------------------------------------------------------
    -- 3. Ensure at least a few available F&B items exist.
    -------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [dbo].[FB_ITEM] WHERE [itemStatus] = N'AVAILABLE')
    BEGIN
        IF EXISTS (SELECT 1 FROM [dbo].[FB_ITEM] WHERE [fbItemId] = N'FB_SEED_COMBO_1')
        BEGIN
            UPDATE [dbo].[FB_ITEM]
            SET [price] = 75000.00,
                [itemStatus] = N'AVAILABLE'
            WHERE [fbItemId] = N'FB_SEED_COMBO_1';
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[FB_ITEM]
                ([fbItemId], [itemName], [price], [itemStatus])
            VALUES
                (N'FB_SEED_COMBO_1', N'Seed Combo Popcorn Pepsi', 75000.00, N'AVAILABLE');
        END;

        IF EXISTS (SELECT 1 FROM [dbo].[FB_ITEM] WHERE [fbItemId] = N'FB_SEED_POPCORN_L')
        BEGIN
            UPDATE [dbo].[FB_ITEM]
            SET [price] = 55000.00,
                [itemStatus] = N'AVAILABLE'
            WHERE [fbItemId] = N'FB_SEED_POPCORN_L';
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[FB_ITEM]
                ([fbItemId], [itemName], [price], [itemStatus])
            VALUES
                (N'FB_SEED_POPCORN_L', N'Seed Popcorn Large', 55000.00, N'AVAILABLE');
        END;

        IF EXISTS (SELECT 1 FROM [dbo].[FB_ITEM] WHERE [fbItemId] = N'FB_SEED_PEPSI_L')
        BEGIN
            UPDATE [dbo].[FB_ITEM]
            SET [price] = 30000.00,
                [itemStatus] = N'AVAILABLE'
            WHERE [fbItemId] = N'FB_SEED_PEPSI_L';
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[FB_ITEM]
                ([fbItemId], [itemName], [price], [itemStatus])
            VALUES
                (N'FB_SEED_PEPSI_L', N'Seed Pepsi Large', 30000.00, N'AVAILABLE');
        END;
    END;

    DECLARE @FbItems table
    (
        [rowNo] int IDENTITY(1, 1) PRIMARY KEY,
        [fbItemId] nvarchar(50) NOT NULL,
        [unitPrice] decimal(18, 2) NOT NULL
    );

    INSERT INTO @FbItems ([fbItemId], [unitPrice])
    SELECT TOP (4)
        [fbItemId],
        [price]
    FROM [dbo].[FB_ITEM]
    WHERE [itemStatus] = N'AVAILABLE'
    ORDER BY [price] DESC, [fbItemId];

    SELECT @FbItemCount = COUNT(1) FROM @FbItems;

    IF @FbItemCount = 0
    BEGIN
        THROW 51002, 'No available F&B item found.', 1;
    END;

    -------------------------------------------------------------------------
    -- 4. Pick 15 available showtime seats, distributed across movies first.
    -------------------------------------------------------------------------
    DECLARE @SeedRows table
    (
        [seedNo] int NOT NULL PRIMARY KEY,
        [customerProfileId] nvarchar(50) NULL,
        [bookingId] nvarchar(50) NULL,
        [bookingSeatId] nvarchar(50) NULL,
        [ticketId] nvarchar(50) NULL,
        [paymentId] nvarchar(50) NULL,
        [bookingFbItemId] nvarchar(50) NULL,
        [showtimeId] nvarchar(50) NOT NULL,
        [showtimeSeatId] nvarchar(50) NOT NULL,
        [movieId] nvarchar(50) NOT NULL,
        [movieTitle] nvarchar(255) NOT NULL,
        [seatPrice] decimal(18, 2) NOT NULL,
        [fbItemId] nvarchar(50) NULL,
        [fbQuantity] int NULL,
        [fbUnitPrice] decimal(18, 2) NULL,
        [fbSubtotal] decimal(18, 2) NULL,
        [totalAmount] decimal(18, 2) NULL,
        [createdAt] datetime2(7) NULL
    );

    ;WITH CandidateSeats AS
    (
        SELECT
            [ss].[showtimeSeatId],
            [s].[showtimeId],
            [s].[movieId],
            [m].[title] AS [movieTitle],
            CAST([s].[basePrice] + [st].[extraFee] AS decimal(18, 2)) AS [seatPrice],
            ROW_NUMBER() OVER
            (
                PARTITION BY [s].[movieId]
                ORDER BY [s].[startTime], [ss].[showtimeSeatId]
            ) AS [movieSeatNo]
        FROM [dbo].[SHOWTIME_SEAT] AS [ss]
        INNER JOIN [dbo].[SHOWTIME] AS [s]
            ON [s].[showtimeId] = [ss].[showtimeId]
        INNER JOIN [dbo].[MOVIE] AS [m]
            ON [m].[movieId] = [s].[movieId]
        INNER JOIN [dbo].[SEAT] AS [seat]
            ON [seat].[seatId] = [ss].[seatId]
        INNER JOIN [dbo].[SEAT_TYPE] AS [st]
            ON [st].[seatTypeId] = [seat].[seatTypeId]
        WHERE [ss].[seatStatus] = N'AVAILABLE'
          AND [s].[status] IN (N'OPEN', N'COMPLETED', N'CLOSED')
          AND NOT EXISTS
          (
              SELECT 1
              FROM [dbo].[BOOKING_SEAT] AS [bs]
              WHERE [bs].[showtimeSeatId] = [ss].[showtimeSeatId]
          )
    ),
    RankedSeats AS
    (
        SELECT TOP (@TicketCount)
            ROW_NUMBER() OVER
            (
                ORDER BY [movieSeatNo], [movieTitle], [showtimeId], [showtimeSeatId]
            ) AS [seedNo],
            [showtimeId],
            [showtimeSeatId],
            [movieId],
            [movieTitle],
            [seatPrice]
        FROM CandidateSeats
        ORDER BY [movieSeatNo], [movieTitle], [showtimeId], [showtimeSeatId]
    )
    INSERT INTO @SeedRows
        ([seedNo], [showtimeId], [showtimeSeatId], [movieId], [movieTitle], [seatPrice])
    SELECT
        [seedNo],
        [showtimeId],
        [showtimeSeatId],
        [movieId],
        [movieTitle],
        [seatPrice]
    FROM RankedSeats;

    IF (SELECT COUNT(1) FROM @SeedRows) < @TicketCount
    BEGIN
        THROW 51003, 'Not enough AVAILABLE SHOWTIME_SEAT rows to seed 15 paid tickets.', 1;
    END;

    UPDATE [sr]
    SET
        [customerProfileId] = [c].[customerProfileId],
        [bookingId] = CONCAT(N'BOK_SEED_', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        [bookingSeatId] = CONCAT(N'BKS_SEED_', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        [ticketId] = CONCAT(N'TCK_SEED_', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        [paymentId] = CONCAT(N'PAY_SEED_', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        [bookingFbItemId] = CONCAT(N'BFI_SEED_', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        [fbItemId] = [fi].[fbItemId],
        [fbQuantity] = CASE WHEN [sr].[seedNo] % 3 = 0 THEN 2 ELSE 1 END,
        [fbUnitPrice] = [fi].[unitPrice],
        [fbSubtotal] = [fi].[unitPrice] * CASE WHEN [sr].[seedNo] % 3 = 0 THEN 2 ELSE 1 END,
        [totalAmount] = [sr].[seatPrice] + ([fi].[unitPrice] * CASE WHEN [sr].[seedNo] % 3 = 0 THEN 2 ELSE 1 END),
        [createdAt] = DATEADD(HOUR, -([sr].[seedNo] * 5), SYSUTCDATETIME())
    FROM @SeedRows AS [sr]
    INNER JOIN @Customers AS [c]
        ON [c].[rowNo] = (([sr].[seedNo] - 1) % (SELECT COUNT(1) FROM @Customers)) + 1
    INNER JOIN @FbItems AS [fi]
        ON [fi].[rowNo] = (([sr].[seedNo] - 1) % @FbItemCount) + 1;

    -------------------------------------------------------------------------
    -- 5. Insert paid bookings, seats, tickets, payments, and F&B rows.
    -------------------------------------------------------------------------
    INSERT INTO [dbo].[BOOKING]
        ([bookingId], [customerProfileId], [showtimeId], [createdByStaffProfileId],
         [bookingChannel], [guestName], [guestPhone], [guestEmail],
         [bookingStatus], [fbFulfillmentStatus], [fbFulfilledAt],
         [totalAmount], [createdAt], [expiredAt])
    SELECT
        [bookingId],
        [customerProfileId],
        [showtimeId],
        NULL,
        N'ONLINE',
        NULL,
        NULL,
        NULL,
        N'PAID',
        N'FULFILLED',
        DATEADD(MINUTE, 2, [createdAt]),
        [totalAmount],
        [createdAt],
        DATEADD(MINUTE, 10, [createdAt])
    FROM @SeedRows;

    INSERT INTO [dbo].[BOOKING_SEAT]
        ([bookingSeatId], [bookingId], [showtimeSeatId], [seatPrice])
    SELECT
        [bookingSeatId],
        [bookingId],
        [showtimeSeatId],
        [seatPrice]
    FROM @SeedRows;

    INSERT INTO [dbo].[TICKET]
        ([ticketId], [bookingSeatId], [qrCode], [ticketStatus], [generatedAt])
    SELECT
        [ticketId],
        [bookingSeatId],
        CONCAT(N'G2C|', [bookingId], N'|', [bookingSeatId], N'|', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 32)),
        N'UNUSED',
        DATEADD(MINUTE, 3, [createdAt])
    FROM @SeedRows;

    INSERT INTO [dbo].[PAYMENT]
        ([paymentId], [bookingId], [paymentProviderId], [amount],
         [paymentMethod], [transactionCode], [providerTransactionCode],
         [paymentStatus], [failureReason], [rawCallbackPayload],
         [createdAt], [updatedAt], [paidAt])
    SELECT
        [paymentId],
        [bookingId],
        @PaymentProviderId,
        [totalAmount],
        N'SEPAY',
        CONCAT(N'T', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 10)),
        CONCAT(N'SEED-', RIGHT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 12)),
        N'SUCCESS',
        NULL,
        N'{"seed":"paid-bookings-with-fb"}',
        [createdAt],
        DATEADD(MINUTE, 2, [createdAt]),
        DATEADD(MINUTE, 2, [createdAt])
    FROM @SeedRows;

    INSERT INTO [dbo].[BOOKING_FB_ITEM]
        ([bookingFBItemId], [bookingId], [fbItemId], [quantity], [unitPrice], [subtotal])
    SELECT
        [bookingFbItemId],
        [bookingId],
        [fbItemId],
        [fbQuantity],
        [fbUnitPrice],
        [fbSubtotal]
    FROM @SeedRows;

    UPDATE [ss]
    SET
        [seatStatus] = N'BOOKED',
        [lockedUntil] = NULL,
        [lockedByUserId] = NULL
    FROM [dbo].[SHOWTIME_SEAT] AS [ss]
    INNER JOIN @SeedRows AS [sr]
        ON [sr].[showtimeSeatId] = [ss].[showtimeSeatId];

    COMMIT TRANSACTION;

    -------------------------------------------------------------------------
    -- 6. Quick check result for dashboard verification.
    -------------------------------------------------------------------------
    SELECT
        COUNT(1) AS [SeededBookings],
        COUNT(DISTINCT [movieId]) AS [DistinctMovies],
        SUM([seatPrice]) AS [TicketRevenue],
        SUM([fbSubtotal]) AS [FbRevenue],
        SUM([totalAmount]) AS [GrossRevenue]
    FROM @SeedRows;

    SELECT
        [movieTitle],
        COUNT(1) AS [Tickets],
        SUM([seatPrice]) AS [TicketRevenue],
        SUM([fbSubtotal]) AS [FbRevenue],
        SUM([totalAmount]) AS [GrossRevenue]
    FROM @SeedRows
    GROUP BY [movieTitle]
    ORDER BY [Tickets] DESC, [GrossRevenue] DESC, [movieTitle];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO
