using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260726213000_AddVoucherShowtimeIdAndRoomId")]
public partial class AddVoucherShowtimeIdAndRoomId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.VOUCHER', 'showtimeId') IS NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] ADD [showtimeId] NVARCHAR(50) NULL;
            END;

            IF COL_LENGTH('dbo.VOUCHER', 'roomId') IS NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] ADD [roomId] NVARCHAR(50) NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.VOUCHER', 'showtimeId') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] DROP COLUMN [showtimeId];
            END;

            IF COL_LENGTH('dbo.VOUCHER', 'roomId') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.[VOUCHER] DROP COLUMN [roomId];
            END;
            """);
    }
}
