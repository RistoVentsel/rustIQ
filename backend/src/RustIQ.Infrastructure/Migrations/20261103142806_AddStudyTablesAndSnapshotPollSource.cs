using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyTablesAndSnapshotPollSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PollSource",
                table: "server_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuerySucceeded",
                table: "server_snapshots",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "study_battlemetrics_wipe_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BattleMetricsServerId = table.Column<long>(type: "bigint", nullable: true),
                    NextWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_battlemetrics_wipe_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_probes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    Players = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: true),
                    QueuePlayers = table.Column<int>(type: "integer", nullable: true),
                    LatencyMilliseconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_probes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_sample_servers",
                columns: table => new
                {
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    QueryPort = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Stratum = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeakPlayersAtSelection = table.Column<int>(type: "integer", nullable: false),
                    BattleMetricsServerId = table.Column<long>(type: "bigint", nullable: true),
                    SelectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_sample_servers", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "study_wipe_signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdvertisedNextWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Keywords = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_wipe_signals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_battlemetrics_wipe_snapshots_ServerId_CapturedAtUtc",
                table: "study_battlemetrics_wipe_snapshots",
                columns: new[] { "ServerId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_probes_ServerId_ProbedAtUtc",
                table: "study_probes",
                columns: new[] { "ServerId", "ProbedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_wipe_signals_ServerId_ObservedAtUtc",
                table: "study_wipe_signals",
                columns: new[] { "ServerId", "ObservedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_battlemetrics_wipe_snapshots");

            migrationBuilder.DropTable(
                name: "study_probes");

            migrationBuilder.DropTable(
                name: "study_sample_servers");

            migrationBuilder.DropTable(
                name: "study_wipe_signals");

            migrationBuilder.DropColumn(
                name: "PollSource",
                table: "server_snapshots");

            migrationBuilder.DropColumn(
                name: "QuerySucceeded",
                table: "server_snapshots");
        }
    }
}
