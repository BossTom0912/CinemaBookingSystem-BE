using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerfFix_AddIndexes_NotifAndBookingFbItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NOTIFICATION_USER_READ",
                table: "NOTIFICATION");

            migrationBuilder.RenameIndex(
                name: "IX_BOOKING_FB_ITEM_fbItemId",
                table: "BOOKING_FB_ITEM",
                newName: "IX_BOOKING_FB_ITEM_FB_ITEM_ID");

            migrationBuilder.RenameIndex(
                name: "IX_BOOKING_FB_ITEM_bookingId",
                table: "BOOKING_FB_ITEM",
                newName: "IX_BOOKING_FB_ITEM_BOOKING_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_USER_READ_CREATED",
                table: "NOTIFICATION",
                columns: new[] { "userId", "isRead", "createdAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NOTIFICATION_USER_READ_CREATED",
                table: "NOTIFICATION");

            migrationBuilder.RenameIndex(
                name: "IX_BOOKING_FB_ITEM_FB_ITEM_ID",
                table: "BOOKING_FB_ITEM",
                newName: "IX_BOOKING_FB_ITEM_fbItemId");

            migrationBuilder.RenameIndex(
                name: "IX_BOOKING_FB_ITEM_BOOKING_ID",
                table: "BOOKING_FB_ITEM",
                newName: "IX_BOOKING_FB_ITEM_bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_USER_READ",
                table: "NOTIFICATION",
                columns: new[] { "userId", "isRead" });
        }
    }
}
