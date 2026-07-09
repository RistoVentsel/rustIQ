using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerRankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "server_rankings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    AveragePlayers = table.Column<double>(type: "double precision", nullable: false),
                    PeakPlayers = table.Column<int>(type: "integer", nullable: false),
                    SnapshotCount = table.Column<int>(type: "integer", nullable: false),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_rankings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_server_rankings_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_server_rankings_Rank",
                table: "server_rankings",
                column: "Rank",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_rankings_ServerId",
                table: "server_rankings",
                column: "ServerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "server_rankings");
        }
    }
}
