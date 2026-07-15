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
        migrationBuilder.AddColumn<string>(
            name: "fbFulfilledByStaffProfileId",
            table: "BOOKING",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID",
            table: "BOOKING",
            column: "fbFulfilledByStaffProfileId");

        migrationBuilder.AddForeignKey(
            name: "FK_BOOKING_FB_FULFILLED_BY_STAFF",
            table: "BOOKING",
            column: "fbFulfilledByStaffProfileId",
            principalTable: "STAFF_PROFILE",
            principalColumn: "staffProfileId");
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
