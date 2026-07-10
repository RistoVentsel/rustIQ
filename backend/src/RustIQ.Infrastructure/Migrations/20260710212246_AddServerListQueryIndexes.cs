using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerListQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_servers_Country",
                table: "servers",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_servers_LastWipeAtUtc",
                table: "servers",
                column: "LastWipeAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_servers_NextWipeAtUtc",
                table: "servers",
                column: "NextWipeAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_servers_Players",
                table: "servers",
                column: "Players");

            migrationBuilder.CreateIndex(
                name: "IX_servers_ServerType",
                table: "servers",
                column: "ServerType");

            migrationBuilder.CreateIndex(
                name: "IX_servers_Status",
                table: "servers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_servers_Country",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_LastWipeAtUtc",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_NextWipeAtUtc",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_Players",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_ServerType",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_Status",
                table: "servers");
        }
    }
}
