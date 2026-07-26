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
        migrationBuilder.Sql(
            """
            ALTER TABLE "BOOKING"
                ADD COLUMN IF NOT EXISTS "fbFulfilledByStaffProfileId" varchar(50);

            CREATE INDEX IF NOT EXISTS "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID"
                ON "BOOKING" ("fbFulfilledByStaffProfileId");

            DO $$
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_BOOKING_FB_FULFILLED_BY_STAFF'
                ) THEN
                    ALTER TABLE "BOOKING"
                        ADD CONSTRAINT "FK_BOOKING_FB_FULFILLED_BY_STAFF"
                        FOREIGN KEY ("fbFulfilledByStaffProfileId")
                        REFERENCES "STAFF_PROFILE" ("staffProfileId");
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "BOOKING"
                DROP CONSTRAINT IF EXISTS "FK_BOOKING_FB_FULFILLED_BY_STAFF";
            DROP INDEX IF EXISTS "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID";
            ALTER TABLE "BOOKING"
                DROP COLUMN IF EXISTS "fbFulfilledByStaffProfileId";
            """);
    }
}
