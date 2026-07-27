using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurePostgresRowVersionConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE SEQUENCE IF NOT EXISTS cinema_row_version_seq AS bigint;

                CREATE OR REPLACE FUNCTION cinema_set_row_version()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    NEW."rowVersion" := decode(
                        lpad(to_hex(nextval('cinema_row_version_seq')), 16, '0'),
                        'hex');
                    RETURN NEW;
                END;
                $function$;

                DO $$
                DECLARE
                    relation_name text;
                    trigger_name text;
                BEGIN
                    FOREACH relation_name IN ARRAY ARRAY[
                        'SHOWTIME_SEAT',
                        'REFUND_CLAIM',
                        'MANUAL_REFUND_PROCESS',
                        'COMPENSATION_TICKET',
                        'COMPENSATION_COMBO']
                    LOOP
                        IF to_regclass(format('%I', relation_name)) IS NOT NULL
                           AND EXISTS (
                               SELECT 1
                               FROM information_schema.columns AS c
                               WHERE c.table_schema = current_schema()
                                 AND c.table_name = relation_name
                                 AND c.column_name = 'rowVersion')
                        THEN
                            EXECUTE format(
                                'UPDATE %I SET "rowVersion" = decode(lpad(to_hex(nextval(''cinema_row_version_seq'')), 16, ''0''), ''hex'') WHERE "rowVersion" IS NULL',
                                relation_name);
                            EXECUTE format(
                                'ALTER TABLE %I ALTER COLUMN "rowVersion" SET DEFAULT decode(lpad(to_hex(nextval(''cinema_row_version_seq'')), 16, ''0''), ''hex'')',
                                relation_name);

                            trigger_name := format('TR_%s_ROW_VERSION', relation_name);
                            EXECUTE format('DROP TRIGGER IF EXISTS %I ON %I', trigger_name, relation_name);
                            EXECUTE format(
                                'CREATE TRIGGER %I BEFORE INSERT OR UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION cinema_set_row_version()',
                                trigger_name,
                                relation_name);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    relation_name text;
                    trigger_name text;
                BEGIN
                    FOREACH relation_name IN ARRAY ARRAY[
                        'SHOWTIME_SEAT',
                        'REFUND_CLAIM',
                        'MANUAL_REFUND_PROCESS',
                        'COMPENSATION_TICKET',
                        'COMPENSATION_COMBO']
                    LOOP
                        IF to_regclass(format('%I', relation_name)) IS NOT NULL THEN
                            trigger_name := format('TR_%s_ROW_VERSION', relation_name);
                            EXECUTE format('DROP TRIGGER IF EXISTS %I ON %I', trigger_name, relation_name);

                            IF EXISTS (
                                SELECT 1
                                FROM information_schema.columns AS c
                                WHERE c.table_schema = current_schema()
                                  AND c.table_name = relation_name
                                  AND c.column_name = 'rowVersion')
                            THEN
                                EXECUTE format(
                                    'ALTER TABLE %I ALTER COLUMN "rowVersion" DROP DEFAULT',
                                    relation_name);
                            END IF;
                        END IF;
                    END LOOP;
                END $$;

                DROP FUNCTION IF EXISTS cinema_set_row_version();
                DROP SEQUENCE IF EXISTS cinema_row_version_seq;
                """);
        }
    }
}
