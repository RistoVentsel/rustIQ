using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleMetricsWipeAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastWipeAtUtc",
                table: "servers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "servers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "servers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextWipeAtUtc",
                table: "servers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWipeAtUtc",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "NextWipeAtUtc",
                table: "servers");
        }
    }
}
