using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcceptFreeFormRefundBankNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "REFUND_CLAIM"
                    DROP CONSTRAINT IF EXISTS "FK_REFUND_CLAIM_BANK_DIRECTORY";
                DROP INDEX IF EXISTS "IX_REFUND_CLAIM_bankCode";
                ALTER TABLE "REFUND_CLAIM"
                    ALTER COLUMN "bankCode" TYPE character varying(100);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "bankCode",
                table: "REFUND_CLAIM",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CLAIM_bankCode",
                table: "REFUND_CLAIM",
                column: "bankCode");

            migrationBuilder.AddForeignKey(
                name: "FK_REFUND_CLAIM_BANK_DIRECTORY",
                table: "REFUND_CLAIM",
                column: "bankCode",
                principalTable: "BANK_DIRECTORY",
                principalColumn: "bankCode");
        }
    }
}
