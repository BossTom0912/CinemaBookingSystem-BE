using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations;

[DbContext(typeof(CinemaDbContext))]
[Migration("20260728110000_AddSeatTypeCatalogMetadata")]
public partial class AddSeatTypeCatalogMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "isActive",
            table: "SEAT_TYPE",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<int>(
            name: "seatSpan",
            table: "SEAT_TYPE",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "sortOrder",
            table: "SEAT_TYPE",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "isActive", table: "SEAT_TYPE");
        migrationBuilder.DropColumn(name: "seatSpan", table: "SEAT_TYPE");
        migrationBuilder.DropColumn(name: "sortOrder", table: "SEAT_TYPE");
    }
}
