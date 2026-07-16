using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260711000000_AddBookingFbFulfilledByStaffProfile")]
public partial class AddBookingFbFulfilledByStaffProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The canonical SQL schema and its upgrade script may have created this
        // column before EF migration history was introduced. Guard every object
        // so MigrateAsync can reconcile either database shape safely.
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.BOOKING', N'fbFulfilledByStaffProfileId') IS NULL
                ALTER TABLE dbo.[BOOKING] ADD [fbFulfilledByStaffProfileId] NVARCHAR(50) NULL;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.BOOKING')
                  AND name = N'IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID'
            )
                CREATE INDEX [IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID]
                    ON dbo.[BOOKING]([fbFulfilledByStaffProfileId]);

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'dbo.BOOKING')
                  AND name = N'FK_BOOKING_FB_FULFILLED_BY_STAFF'
            )
                ALTER TABLE dbo.[BOOKING]
                    ADD CONSTRAINT [FK_BOOKING_FB_FULFILLED_BY_STAFF]
                    FOREIGN KEY ([fbFulfilledByStaffProfileId])
                    REFERENCES dbo.[STAFF_PROFILE]([staffProfileId]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BOOKING_FB_FULFILLED_BY_STAFF",
            table: "BOOKING");

        migrationBuilder.DropIndex(
            name: "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID",
            table: "BOOKING");

        migrationBuilder.DropColumn(
            name: "fbFulfilledByStaffProfileId",
            table: "BOOKING");
    }
}
