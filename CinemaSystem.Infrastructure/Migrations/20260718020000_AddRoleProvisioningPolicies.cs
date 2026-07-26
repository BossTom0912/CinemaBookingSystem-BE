using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260718020000_AddRoleProvisioningPolicies")]
public partial class AddRoleProvisioningPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO "ROLE" ("roleId", "roleName", "description")
            VALUES
                ('ROLE_CUSTOMER', 'CUSTOMER', 'Customer account'),
                ('ROLE_STAFF', 'STAFF', 'Cinema staff account'),
                ('ROLE_MANAGER', 'MANAGER', 'Cinema manager account'),
                ('ROLE_ADMIN', 'ADMIN', 'System administrator account')
            ON CONFLICT ("roleId") DO NOTHING;

            CREATE TABLE IF NOT EXISTS "ROLE_PROVISIONING_POLICY"
            (
                "roleId" varchar(50) PRIMARY KEY,
                "profileKind" varchar(20) NOT NULL,
                "requiresCinema" boolean NOT NULL DEFAULT false,
                "defaultStaffPosition" varchar(100),
                "isActive" boolean NOT NULL DEFAULT true,
                "isPublicRegistrationAllowed" boolean NOT NULL DEFAULT false,
                CONSTRAINT "CK_ROLE_PROVISIONING_POLICY_PROFILE"
                    CHECK ("profileKind" IN ('CUSTOMER', 'STAFF', 'NONE')),
                CONSTRAINT "CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE"
                    CHECK
                    (
                        ("profileKind" = 'STAFF' AND "requiresCinema" = true AND "defaultStaffPosition" IS NOT NULL)
                        OR ("profileKind" = 'CUSTOMER' AND "requiresCinema" = false AND "defaultStaffPosition" IS NULL)
                        OR ("profileKind" = 'NONE' AND "requiresCinema" = false AND "defaultStaffPosition" IS NULL)
                    ),
                CONSTRAINT "CK_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER"
                    CHECK ("isPublicRegistrationAllowed" = false OR "profileKind" = 'CUSTOMER'),
                CONSTRAINT "FK_ROLE_PROVISIONING_POLICY_ROLE"
                    FOREIGN KEY ("roleId") REFERENCES "ROLE" ("roleId")
            );

            CREATE TABLE IF NOT EXISTS "ROLE_ASSIGNMENT_RULE"
            (
                "grantorRoleId" varchar(50) NOT NULL,
                "granteeRoleId" varchar(50) NOT NULL,
                "isActive" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_ROLE_ASSIGNMENT_RULE" PRIMARY KEY ("grantorRoleId", "granteeRoleId"),
                CONSTRAINT "CK_ROLE_ASSIGNMENT_RULE_DIFFERENT_ROLES"
                    CHECK ("grantorRoleId" <> "granteeRoleId"),
                CONSTRAINT "FK_ROLE_ASSIGNMENT_RULE_GRANTOR"
                    FOREIGN KEY ("grantorRoleId") REFERENCES "ROLE" ("roleId"),
                CONSTRAINT "FK_ROLE_ASSIGNMENT_RULE_GRANTEE"
                    FOREIGN KEY ("granteeRoleId") REFERENCES "ROLE" ("roleId")
            );

            CREATE INDEX IF NOT EXISTS "IX_ROLE_PROVISIONING_POLICY_PUBLIC"
                ON "ROLE_PROVISIONING_POLICY" ("isActive", "isPublicRegistrationAllowed");
            CREATE INDEX IF NOT EXISTS "IX_ROLE_ASSIGNMENT_RULE_GRANTEE"
                ON "ROLE_ASSIGNMENT_RULE" ("granteeRoleId");

            INSERT INTO "ROLE_PROVISIONING_POLICY"
                ("roleId", "profileKind", "requiresCinema", "defaultStaffPosition", "isActive", "isPublicRegistrationAllowed")
            VALUES
                ('ROLE_CUSTOMER', 'CUSTOMER', false, NULL, true, true),
                ('ROLE_STAFF', 'STAFF', true, 'Staff', true, false),
                ('ROLE_MANAGER', 'STAFF', true, 'Manager', true, false),
                ('ROLE_ADMIN', 'NONE', false, NULL, true, false)
            ON CONFLICT ("roleId") DO UPDATE SET
                "profileKind" = EXCLUDED."profileKind",
                "requiresCinema" = EXCLUDED."requiresCinema",
                "defaultStaffPosition" = EXCLUDED."defaultStaffPosition",
                "isActive" = EXCLUDED."isActive",
                "isPublicRegistrationAllowed" = EXCLUDED."isPublicRegistrationAllowed";

            INSERT INTO "ROLE_ASSIGNMENT_RULE" ("grantorRoleId", "granteeRoleId", "isActive")
            VALUES
                ('ROLE_ADMIN', 'ROLE_CUSTOMER', true),
                ('ROLE_ADMIN', 'ROLE_STAFF', true),
                ('ROLE_ADMIN', 'ROLE_MANAGER', true)
            ON CONFLICT ("grantorRoleId", "granteeRoleId") DO UPDATE SET
                "isActive" = EXCLUDED."isActive";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS "ROLE_ASSIGNMENT_RULE";
            DROP TABLE IF EXISTS "ROLE_PROVISIONING_POLICY";
            """);
    }
}
