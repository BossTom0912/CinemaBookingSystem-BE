using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherShowtimeIdAndRoomIdPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "VOUCHER"
                    ADD COLUMN IF NOT EXISTS "roomId" character varying(50) NULL;

                ALTER TABLE "VOUCHER"
                    ADD COLUMN IF NOT EXISTS "showtimeId" character varying(50) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: an adopted database may have owned these columns before EF history.
        }
    }
}
