using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedBankDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "BANK_DIRECTORY" (
                    "bankCode",
                    "bankBin",
                    "shortName",
                    "fullName",
                    "isActive",
                    "supportsAccountInquiry",
                    "supportsPayout",
                    "createdAt")
                VALUES
                    ('VCB', '970436', 'Vietcombank', 'Joint Stock Commercial Bank for Foreign Trade of Vietnam', TRUE, FALSE, FALSE, CURRENT_TIMESTAMP),
                    ('MB', '970422', 'MB Bank', 'Military Commercial Joint Stock Bank', TRUE, FALSE, FALSE, CURRENT_TIMESTAMP),
                    ('TCB', '970407', 'Techcombank', 'Vietnam Technological and Commercial Joint Stock Bank', TRUE, FALSE, FALSE, CURRENT_TIMESTAMP),
                    ('BIDV', '970418', 'BIDV', 'Joint Stock Commercial Bank for Investment and Development of Vietnam', TRUE, FALSE, FALSE, CURRENT_TIMESTAMP),
                    ('CTG', '970415', 'VietinBank', 'Vietnam Joint Stock Commercial Bank for Industry and Trade', TRUE, FALSE, FALSE, CURRENT_TIMESTAMP)
                ON CONFLICT ("bankCode") DO UPDATE
                SET
                    "bankBin" = EXCLUDED."bankBin",
                    "shortName" = EXCLUDED."shortName",
                    "fullName" = EXCLUDED."fullName",
                    "isActive" = TRUE,
                    "updatedAt" = CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only reference data: deleting these rows could break existing refund claims.
        }
    }
}
