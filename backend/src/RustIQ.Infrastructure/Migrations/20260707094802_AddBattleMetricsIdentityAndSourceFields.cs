using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleMetricsIdentityAndSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "servers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BattleMetricsServerId",
                table: "servers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "servers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataQuality",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "GamePort",
                table: "servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastValidationReason",
                table: "servers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerListSource",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "SummarySource",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateIndex(
                name: "IX_servers_BattleMetricsServerId",
                table: "servers",
                column: "BattleMetricsServerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_servers_BattleMetricsServerId",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "BattleMetricsServerId",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "DataQuality",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "GamePort",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "LastValidationReason",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "PlayerListSource",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "SummarySource",
                table: "servers");
        }
    }
}
