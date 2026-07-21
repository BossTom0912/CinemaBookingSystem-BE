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
            IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_CUSTOMER')
                INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
                VALUES (N'ROLE_CUSTOMER', N'CUSTOMER', N'Customer account');

            IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_STAFF')
                INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
                VALUES (N'ROLE_STAFF', N'STAFF', N'Cinema staff account');

            IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_MANAGER')
                INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
                VALUES (N'ROLE_MANAGER', N'MANAGER', N'Cinema manager account');

            IF NOT EXISTS (SELECT 1 FROM dbo.[ROLE] WHERE [roleId] = N'ROLE_ADMIN')
                INSERT INTO dbo.[ROLE] ([roleId], [roleName], [description])
                VALUES (N'ROLE_ADMIN', N'ADMIN', N'System administrator account');

            IF OBJECT_ID(N'dbo.ROLE_PROVISIONING_POLICY', N'U') IS NULL
                CREATE TABLE dbo.[ROLE_PROVISIONING_POLICY]
                (
                    [roleId] NVARCHAR(50) NOT NULL PRIMARY KEY,
                    [profileKind] NVARCHAR(20) NOT NULL,
                    [requiresCinema] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_REQUIRES_CINEMA] DEFAULT 0,
                    [defaultStaffPosition] NVARCHAR(100) NULL,
                    [isActive] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_ACTIVE] DEFAULT 1,
                    [isPublicRegistrationAllowed] BIT NOT NULL CONSTRAINT [DF_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER] DEFAULT 0,
                    CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PROFILE]
                        CHECK ([profileKind] IN (N'CUSTOMER', N'STAFF', N'NONE')),
                    CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE]
                        CHECK
                        (
                            ([profileKind] = N'STAFF' AND [requiresCinema] = 1 AND [defaultStaffPosition] IS NOT NULL)
                            OR ([profileKind] = N'CUSTOMER' AND [requiresCinema] = 0 AND [defaultStaffPosition] IS NULL)
                            OR ([profileKind] = N'NONE' AND [requiresCinema] = 0 AND [defaultStaffPosition] IS NULL)
                        ),
                    CONSTRAINT [CK_ROLE_PROVISIONING_POLICY_PUBLIC_REGISTER]
                        CHECK ([isPublicRegistrationAllowed] = 0 OR [profileKind] = N'CUSTOMER'),
                    CONSTRAINT [FK_ROLE_PROVISIONING_POLICY_ROLE]
                        FOREIGN KEY ([roleId]) REFERENCES dbo.[ROLE]([roleId])
                );

            IF OBJECT_ID(N'dbo.ROLE_ASSIGNMENT_RULE', N'U') IS NULL
                CREATE TABLE dbo.[ROLE_ASSIGNMENT_RULE]
                (
                    [grantorRoleId] NVARCHAR(50) NOT NULL,
                    [granteeRoleId] NVARCHAR(50) NOT NULL,
                    [isActive] BIT NOT NULL CONSTRAINT [DF_ROLE_ASSIGNMENT_RULE_ACTIVE] DEFAULT 1,
                    CONSTRAINT [PK_ROLE_ASSIGNMENT_RULE] PRIMARY KEY ([grantorRoleId], [granteeRoleId]),
                    CONSTRAINT [CK_ROLE_ASSIGNMENT_RULE_DIFFERENT_ROLES]
                        CHECK ([grantorRoleId] <> [granteeRoleId]),
                    CONSTRAINT [FK_ROLE_ASSIGNMENT_RULE_GRANTOR]
                        FOREIGN KEY ([grantorRoleId]) REFERENCES dbo.[ROLE]([roleId]),
                    CONSTRAINT [FK_ROLE_ASSIGNMENT_RULE_GRANTEE]
                        FOREIGN KEY ([granteeRoleId]) REFERENCES dbo.[ROLE]([roleId])
                );

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.ROLE_PROVISIONING_POLICY')
                  AND name = N'IX_ROLE_PROVISIONING_POLICY_PUBLIC'
            )
                CREATE INDEX [IX_ROLE_PROVISIONING_POLICY_PUBLIC]
                    ON dbo.[ROLE_PROVISIONING_POLICY]([isActive], [isPublicRegistrationAllowed]);

            IF NOT EXISTS
            (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.ROLE_ASSIGNMENT_RULE')
                  AND name = N'IX_ROLE_ASSIGNMENT_RULE_GRANTEE'
            )
                CREATE INDEX [IX_ROLE_ASSIGNMENT_RULE_GRANTEE]
                    ON dbo.[ROLE_ASSIGNMENT_RULE]([granteeRoleId]);

            MERGE dbo.[ROLE_PROVISIONING_POLICY] AS target
            USING
            (
                VALUES
                    (N'ROLE_CUSTOMER', N'CUSTOMER', CONVERT(bit, 0), CAST(NULL AS NVARCHAR(100)), CONVERT(bit, 1), CONVERT(bit, 1)),
                    (N'ROLE_STAFF', N'STAFF', CONVERT(bit, 1), N'Staff', CONVERT(bit, 1), CONVERT(bit, 0)),
                    (N'ROLE_MANAGER', N'STAFF', CONVERT(bit, 1), N'Manager', CONVERT(bit, 1), CONVERT(bit, 0)),
                    (N'ROLE_ADMIN', N'NONE', CONVERT(bit, 0), CAST(NULL AS NVARCHAR(100)), CONVERT(bit, 1), CONVERT(bit, 0))
            ) AS source ([roleId], [profileKind], [requiresCinema], [defaultStaffPosition], [isActive], [isPublicRegistrationAllowed])
            ON target.[roleId] = source.[roleId]
            WHEN NOT MATCHED THEN INSERT
                ([roleId], [profileKind], [requiresCinema], [defaultStaffPosition], [isActive], [isPublicRegistrationAllowed])
            VALUES
                (source.[roleId], source.[profileKind], source.[requiresCinema], source.[defaultStaffPosition], source.[isActive], source.[isPublicRegistrationAllowed]);

            MERGE dbo.[ROLE_ASSIGNMENT_RULE] AS target
            USING
            (
                VALUES
                    (N'ROLE_ADMIN', N'ROLE_CUSTOMER', CONVERT(bit, 1)),
                    (N'ROLE_ADMIN', N'ROLE_STAFF', CONVERT(bit, 1)),
                    (N'ROLE_ADMIN', N'ROLE_MANAGER', CONVERT(bit, 1))
            ) AS source ([grantorRoleId], [granteeRoleId], [isActive])
            ON target.[grantorRoleId] = source.[grantorRoleId]
               AND target.[granteeRoleId] = source.[granteeRoleId]
            WHEN NOT MATCHED THEN INSERT ([grantorRoleId], [granteeRoleId], [isActive])
            VALUES (source.[grantorRoleId], source.[granteeRoleId], source.[isActive]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.ROLE_ASSIGNMENT_RULE', N'U') IS NOT NULL
                DROP TABLE dbo.[ROLE_ASSIGNMENT_RULE];

            IF OBJECT_ID(N'dbo.ROLE_PROVISIONING_POLICY', N'U') IS NOT NULL
                DROP TABLE dbo.[ROLE_PROVISIONING_POLICY];
            """);
    }
}
