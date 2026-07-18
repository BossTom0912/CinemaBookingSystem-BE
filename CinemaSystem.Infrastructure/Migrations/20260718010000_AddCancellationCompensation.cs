using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260718010000_AddCancellationCompensation")]
public partial class AddCancellationCompensation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.BOOKING', N'compensationDiscountAmount') IS NULL
                ALTER TABLE dbo.[BOOKING]
                    ADD [compensationDiscountAmount] DECIMAL(18,2) NOT NULL
                        CONSTRAINT [DF_BOOKING_COMPENSATION_DISCOUNT_AMOUNT] DEFAULT 0;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
                  AND name = N'CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT'
            )
                ALTER TABLE dbo.[BOOKING] WITH CHECK
                    ADD CONSTRAINT [CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT]
                    CHECK ([compensationDiscountAmount] >= 0);

            IF OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION', N'U') IS NULL
                CREATE TABLE dbo.[CANCELLATION_COMPENSATION]
                (
                    [cancellationCompensationId] NVARCHAR(50) NOT NULL PRIMARY KEY,
                    [sourceBookingId] NVARCHAR(50) NOT NULL,
                    [showtimeCancellationId] NVARCHAR(50) NOT NULL,
                    [customerProfileId] NVARCHAR(50) NULL,
                    [status] NVARCHAR(30) NOT NULL
                        CONSTRAINT [DF_CANCELLATION_COMPENSATION_STATUS] DEFAULT 'ISSUED',
                    [policyVersion] NVARCHAR(50) NOT NULL,
                    [issuedAt] DATETIME2 NOT NULL
                        CONSTRAINT [DF_CANCELLATION_COMPENSATION_ISSUED_AT] DEFAULT SYSUTCDATETIME(),
                    [expiresAt] DATETIME2 NOT NULL,
                    CONSTRAINT [UQ_CANCELLATION_COMPENSATION_BOOKING] UNIQUE ([sourceBookingId]),
                    CONSTRAINT [CK_CANCELLATION_COMPENSATION_STATUS]
                        CHECK ([status] IN ('ISSUED', 'PARTIALLY_USED', 'USED', 'EXPIRED', 'VOIDED')),
                    CONSTRAINT [CK_CANCELLATION_COMPENSATION_EXPIRY]
                        CHECK ([expiresAt] > [issuedAt]),
                    CONSTRAINT [FK_CANCELLATION_COMPENSATION_BOOKING]
                        FOREIGN KEY ([sourceBookingId]) REFERENCES dbo.[BOOKING]([bookingId]),
                    CONSTRAINT [FK_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION]
                        FOREIGN KEY ([showtimeCancellationId]) REFERENCES dbo.[SHOWTIME_CANCELLATION]([showtimeCancellationId]),
                    CONSTRAINT [FK_CANCELLATION_COMPENSATION_CUSTOMER_PROFILE]
                        FOREIGN KEY ([customerProfileId]) REFERENCES dbo.[CUSTOMER_PROFILE]([customerProfileId])
                );

            IF OBJECT_ID(N'dbo.COMPENSATION_TICKET', N'U') IS NULL
                CREATE TABLE dbo.[COMPENSATION_TICKET]
                (
                    [compensationTicketId] NVARCHAR(50) NOT NULL PRIMARY KEY,
                    [cancellationCompensationId] NVARCHAR(50) NOT NULL,
                    [voucherCode] NVARCHAR(100) NOT NULL,
                    [status] NVARCHAR(30) NOT NULL
                        CONSTRAINT [DF_COMPENSATION_TICKET_STATUS] DEFAULT 'ISSUED',
                    [reservedBookingId] NVARCHAR(50) NULL,
                    [reservedBookingSeatId] NVARCHAR(50) NULL,
                    [reservedAt] DATETIME2 NULL,
                    [redeemedAt] DATETIME2 NULL,
                    [rowVersion] ROWVERSION NOT NULL,
                    CONSTRAINT [UQ_COMPENSATION_TICKET_CODE] UNIQUE ([voucherCode]),
                    CONSTRAINT [CK_COMPENSATION_TICKET_STATUS]
                        CHECK ([status] IN ('ISSUED', 'RESERVED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
                    CONSTRAINT [FK_COMPENSATION_TICKET_COMPENSATION]
                        FOREIGN KEY ([cancellationCompensationId])
                        REFERENCES dbo.[CANCELLATION_COMPENSATION]([cancellationCompensationId])
                        ON DELETE CASCADE,
                    CONSTRAINT [FK_COMPENSATION_TICKET_RESERVED_BOOKING]
                        FOREIGN KEY ([reservedBookingId]) REFERENCES dbo.[BOOKING]([bookingId]),
                    CONSTRAINT [FK_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT]
                        FOREIGN KEY ([reservedBookingSeatId]) REFERENCES dbo.[BOOKING_SEAT]([bookingSeatId])
                );

            IF OBJECT_ID(N'dbo.COMPENSATION_COMBO', N'U') IS NULL
                CREATE TABLE dbo.[COMPENSATION_COMBO]
                (
                    [compensationComboId] NVARCHAR(50) NOT NULL PRIMARY KEY,
                    [cancellationCompensationId] NVARCHAR(50) NOT NULL,
                    [voucherCode] NVARCHAR(100) NOT NULL,
                    [displayName] NVARCHAR(255) NOT NULL,
                    [status] NVARCHAR(30) NOT NULL
                        CONSTRAINT [DF_COMPENSATION_COMBO_STATUS] DEFAULT 'ISSUED',
                    [redeemedAt] DATETIME2 NULL,
                    [redeemedAtCinemaId] NVARCHAR(50) NULL,
                    [redeemedByStaffProfileId] NVARCHAR(50) NULL,
                    [rowVersion] ROWVERSION NOT NULL,
                    CONSTRAINT [UQ_COMPENSATION_COMBO_COMPENSATION] UNIQUE ([cancellationCompensationId]),
                    CONSTRAINT [UQ_COMPENSATION_COMBO_CODE] UNIQUE ([voucherCode]),
                    CONSTRAINT [CK_COMPENSATION_COMBO_STATUS]
                        CHECK ([status] IN ('ISSUED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
                    CONSTRAINT [FK_COMPENSATION_COMBO_COMPENSATION]
                        FOREIGN KEY ([cancellationCompensationId])
                        REFERENCES dbo.[CANCELLATION_COMPENSATION]([cancellationCompensationId])
                        ON DELETE CASCADE,
                    CONSTRAINT [FK_COMPENSATION_COMBO_CINEMA]
                        FOREIGN KEY ([redeemedAtCinemaId]) REFERENCES dbo.[CINEMA]([cinemaId]),
                    CONSTRAINT [FK_COMPENSATION_COMBO_STAFF_PROFILE]
                        FOREIGN KEY ([redeemedByStaffProfileId]) REFERENCES dbo.[STAFF_PROFILE]([staffProfileId])
                );

            IF COL_LENGTH(N'dbo.COMPENSATION_TICKET', N'rowVersion') IS NULL
                ALTER TABLE dbo.[COMPENSATION_TICKET]
                    ADD [rowVersion] ROWVERSION NOT NULL;

            IF COL_LENGTH(N'dbo.COMPENSATION_COMBO', N'rowVersion') IS NULL
                ALTER TABLE dbo.[COMPENSATION_COMBO]
                    ADD [rowVersion] ROWVERSION NOT NULL;

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION')
                  AND name = N'IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION'
            )
                CREATE INDEX [IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION]
                    ON dbo.[CANCELLATION_COMPENSATION]([showtimeCancellationId]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION')
                  AND name = N'IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS'
            )
                CREATE INDEX [IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS]
                    ON dbo.[CANCELLATION_COMPENSATION]([customerProfileId], [status]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
                  AND name = N'IX_COMPENSATION_TICKET_COMPENSATION'
            )
                CREATE INDEX [IX_COMPENSATION_TICKET_COMPENSATION]
                    ON dbo.[COMPENSATION_TICKET]([cancellationCompensationId]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
                  AND name = N'IX_COMPENSATION_TICKET_RESERVED_BOOKING'
            )
                CREATE INDEX [IX_COMPENSATION_TICKET_RESERVED_BOOKING]
                    ON dbo.[COMPENSATION_TICKET]([reservedBookingId]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.COMPENSATION_TICKET')
                  AND name = N'UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT'
            )
                CREATE UNIQUE INDEX [UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT]
                    ON dbo.[COMPENSATION_TICKET]([reservedBookingSeatId])
                    WHERE [reservedBookingSeatId] IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.COMPENSATION_COMBO', N'U') IS NOT NULL
                DROP TABLE dbo.[COMPENSATION_COMBO];

            IF OBJECT_ID(N'dbo.COMPENSATION_TICKET', N'U') IS NOT NULL
                DROP TABLE dbo.[COMPENSATION_TICKET];

            IF OBJECT_ID(N'dbo.CANCELLATION_COMPENSATION', N'U') IS NOT NULL
                DROP TABLE dbo.[CANCELLATION_COMPENSATION];

            IF EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
                  AND name = N'CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT'
            )
                ALTER TABLE dbo.[BOOKING]
                    DROP CONSTRAINT [CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT];

            IF COL_LENGTH(N'dbo.BOOKING', N'compensationDiscountAmount') IS NOT NULL
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
                      AND name = N'DF_BOOKING_COMPENSATION_DISCOUNT_AMOUNT'
                )
                    ALTER TABLE dbo.[BOOKING]
                        DROP CONSTRAINT [DF_BOOKING_COMPENSATION_DISCOUNT_AMOUNT];

                ALTER TABLE dbo.[BOOKING]
                    DROP COLUMN [compensationDiscountAmount];
            END;
            """);
    }
}
