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
            ALTER TABLE "BOOKING"
                ADD COLUMN IF NOT EXISTS "compensationDiscountAmount" numeric(18,2) NOT NULL DEFAULT 0;

            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT') THEN
                    ALTER TABLE "BOOKING"
                        ADD CONSTRAINT "CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT"
                        CHECK ("compensationDiscountAmount" >= 0);
                END IF;
            END $$;

            CREATE OR REPLACE FUNCTION cinema_set_row_version()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                NEW."rowVersion" := decode(lpad(to_hex(txid_current()), 16, '0'), 'hex');
                RETURN NEW;
            END;
            $$;

            CREATE TABLE IF NOT EXISTS "CANCELLATION_COMPENSATION"
            (
                "cancellationCompensationId" varchar(50) PRIMARY KEY,
                "sourceBookingId" varchar(50) NOT NULL,
                "showtimeCancellationId" varchar(50) NOT NULL,
                "customerProfileId" varchar(50),
                "status" varchar(30) NOT NULL DEFAULT 'ISSUED',
                "policyVersion" varchar(50) NOT NULL,
                "issuedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "expiresAt" timestamp with time zone NOT NULL,
                CONSTRAINT "UQ_CANCELLATION_COMPENSATION_BOOKING" UNIQUE ("sourceBookingId"),
                CONSTRAINT "CK_CANCELLATION_COMPENSATION_STATUS"
                    CHECK ("status" IN ('ISSUED', 'PARTIALLY_USED', 'USED', 'EXPIRED', 'VOIDED')),
                CONSTRAINT "CK_CANCELLATION_COMPENSATION_EXPIRY" CHECK ("expiresAt" > "issuedAt"),
                CONSTRAINT "FK_CANCELLATION_COMPENSATION_BOOKING"
                    FOREIGN KEY ("sourceBookingId") REFERENCES "BOOKING" ("bookingId"),
                CONSTRAINT "FK_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION"
                    FOREIGN KEY ("showtimeCancellationId") REFERENCES "SHOWTIME_CANCELLATION" ("showtimeCancellationId"),
                CONSTRAINT "FK_CANCELLATION_COMPENSATION_CUSTOMER_PROFILE"
                    FOREIGN KEY ("customerProfileId") REFERENCES "CUSTOMER_PROFILE" ("customerProfileId")
            );

            CREATE TABLE IF NOT EXISTS "COMPENSATION_TICKET"
            (
                "compensationTicketId" varchar(50) PRIMARY KEY,
                "cancellationCompensationId" varchar(50) NOT NULL,
                "voucherCode" varchar(100) NOT NULL,
                "status" varchar(30) NOT NULL DEFAULT 'ISSUED',
                "reservedBookingId" varchar(50),
                "reservedBookingSeatId" varchar(50),
                "reservedAt" timestamp with time zone,
                "redeemedAt" timestamp with time zone,
                "rowVersion" bytea NOT NULL DEFAULT decode(lpad(to_hex(txid_current()), 16, '0'), 'hex'),
                CONSTRAINT "UQ_COMPENSATION_TICKET_CODE" UNIQUE ("voucherCode"),
                CONSTRAINT "CK_COMPENSATION_TICKET_STATUS"
                    CHECK ("status" IN ('ISSUED', 'RESERVED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
                CONSTRAINT "FK_COMPENSATION_TICKET_COMPENSATION"
                    FOREIGN KEY ("cancellationCompensationId")
                    REFERENCES "CANCELLATION_COMPENSATION" ("cancellationCompensationId") ON DELETE CASCADE,
                CONSTRAINT "FK_COMPENSATION_TICKET_RESERVED_BOOKING"
                    FOREIGN KEY ("reservedBookingId") REFERENCES "BOOKING" ("bookingId"),
                CONSTRAINT "FK_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT"
                    FOREIGN KEY ("reservedBookingSeatId") REFERENCES "BOOKING_SEAT" ("bookingSeatId")
            );

            CREATE TABLE IF NOT EXISTS "COMPENSATION_COMBO"
            (
                "compensationComboId" varchar(50) PRIMARY KEY,
                "cancellationCompensationId" varchar(50) NOT NULL,
                "voucherCode" varchar(100) NOT NULL,
                "displayName" varchar(255) NOT NULL,
                "status" varchar(30) NOT NULL DEFAULT 'ISSUED',
                "redeemedAt" timestamp with time zone,
                "redeemedAtCinemaId" varchar(50),
                "redeemedByStaffProfileId" varchar(50),
                "rowVersion" bytea NOT NULL DEFAULT decode(lpad(to_hex(txid_current()), 16, '0'), 'hex'),
                CONSTRAINT "UQ_COMPENSATION_COMBO_COMPENSATION" UNIQUE ("cancellationCompensationId"),
                CONSTRAINT "UQ_COMPENSATION_COMBO_CODE" UNIQUE ("voucherCode"),
                CONSTRAINT "CK_COMPENSATION_COMBO_STATUS"
                    CHECK ("status" IN ('ISSUED', 'REDEEMED', 'EXPIRED', 'VOIDED')),
                CONSTRAINT "FK_COMPENSATION_COMBO_COMPENSATION"
                    FOREIGN KEY ("cancellationCompensationId")
                    REFERENCES "CANCELLATION_COMPENSATION" ("cancellationCompensationId") ON DELETE CASCADE,
                CONSTRAINT "FK_COMPENSATION_COMBO_CINEMA"
                    FOREIGN KEY ("redeemedAtCinemaId") REFERENCES "CINEMA" ("cinemaId"),
                CONSTRAINT "FK_COMPENSATION_COMBO_STAFF_PROFILE"
                    FOREIGN KEY ("redeemedByStaffProfileId") REFERENCES "STAFF_PROFILE" ("staffProfileId")
            );

            ALTER TABLE "COMPENSATION_TICKET"
                ADD COLUMN IF NOT EXISTS "rowVersion" bytea;
            ALTER TABLE "COMPENSATION_COMBO"
                ADD COLUMN IF NOT EXISTS "rowVersion" bytea;
            UPDATE "COMPENSATION_TICKET"
                SET "rowVersion" = decode(lpad(to_hex(txid_current()), 16, '0'), 'hex')
                WHERE "rowVersion" IS NULL;
            UPDATE "COMPENSATION_COMBO"
                SET "rowVersion" = decode(lpad(to_hex(txid_current()), 16, '0'), 'hex')
                WHERE "rowVersion" IS NULL;
            ALTER TABLE "COMPENSATION_TICKET"
                ALTER COLUMN "rowVersion" SET DEFAULT decode(lpad(to_hex(txid_current()), 16, '0'), 'hex'),
                ALTER COLUMN "rowVersion" SET NOT NULL;
            ALTER TABLE "COMPENSATION_COMBO"
                ALTER COLUMN "rowVersion" SET DEFAULT decode(lpad(to_hex(txid_current()), 16, '0'), 'hex'),
                ALTER COLUMN "rowVersion" SET NOT NULL;

            DROP TRIGGER IF EXISTS "TR_COMPENSATION_TICKET_ROW_VERSION" ON "COMPENSATION_TICKET";
            CREATE TRIGGER "TR_COMPENSATION_TICKET_ROW_VERSION"
                BEFORE UPDATE ON "COMPENSATION_TICKET"
                FOR EACH ROW EXECUTE FUNCTION cinema_set_row_version();
            DROP TRIGGER IF EXISTS "TR_COMPENSATION_COMBO_ROW_VERSION" ON "COMPENSATION_COMBO";
            CREATE TRIGGER "TR_COMPENSATION_COMBO_ROW_VERSION"
                BEFORE UPDATE ON "COMPENSATION_COMBO"
                FOR EACH ROW EXECUTE FUNCTION cinema_set_row_version();

            CREATE INDEX IF NOT EXISTS "IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION"
                ON "CANCELLATION_COMPENSATION" ("showtimeCancellationId");
            CREATE INDEX IF NOT EXISTS "IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS"
                ON "CANCELLATION_COMPENSATION" ("customerProfileId", "status");
            CREATE INDEX IF NOT EXISTS "IX_COMPENSATION_TICKET_COMPENSATION"
                ON "COMPENSATION_TICKET" ("cancellationCompensationId");
            CREATE INDEX IF NOT EXISTS "IX_COMPENSATION_TICKET_RESERVED_BOOKING"
                ON "COMPENSATION_TICKET" ("reservedBookingId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT"
                ON "COMPENSATION_TICKET" ("reservedBookingSeatId")
                WHERE "reservedBookingSeatId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS "COMPENSATION_COMBO";
            DROP TABLE IF EXISTS "COMPENSATION_TICKET";
            DROP TABLE IF EXISTS "CANCELLATION_COMPENSATION";
            ALTER TABLE "BOOKING" DROP CONSTRAINT IF EXISTS "CK_BOOKING_COMPENSATION_DISCOUNT_AMOUNT";
            ALTER TABLE "BOOKING" DROP COLUMN IF EXISTS "compensationDiscountAmount";
            """);
    }
}
