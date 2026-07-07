using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamDiscoveryAndPlayerCountSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscoverySource",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDiscoveredAtUtc",
                table: "servers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPlayerCountUpdatedAtUtc",
                table: "servers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerCountSource",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscoverySource",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "LastDiscoveredAtUtc",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "LastPlayerCountUpdatedAtUtc",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "PlayerCountSource",
                table: "servers");
        }
    }
}
