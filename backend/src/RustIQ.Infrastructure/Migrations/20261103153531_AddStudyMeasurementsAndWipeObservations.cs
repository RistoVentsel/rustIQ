using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyMeasurementsAndWipeObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_population_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalServers = table.Column<int>(type: "integer", nullable: false),
                    OnlineServers = table.Column<int>(type: "integer", nullable: false),
                    OnlineServersWithPlayers = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_population_samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_resource_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SampledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessCpuMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    WorkingSetBytes = table.Column<long>(type: "bigint", nullable: false),
                    NetworkReceivedBytes = table.Column<long>(type: "bigint", nullable: true),
                    NetworkSentBytes = table.Column<long>(type: "bigint", nullable: true),
                    A2sPacketsSent = table.Column<long>(type: "bigint", nullable: false),
                    BattleMetricsRequests = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_resource_samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_sample_rankings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_sample_rankings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_server_wipe_observations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousLastWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PreviousNextWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFirstObservationSinceStart = table.Column<bool>(type: "boolean", nullable: false),
                    Keywords = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_server_wipe_observations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_table_write_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TableName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Inserts = table.Column<long>(type: "bigint", nullable: false),
                    Updates = table.Column<long>(type: "bigint", nullable: false),
                    Deletes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_table_write_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_population_samples_SampledAtUtc",
                table: "study_population_samples",
                column: "SampledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_study_resource_samples_Role_SampledAtUtc",
                table: "study_resource_samples",
                columns: new[] { "Role", "SampledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_sample_rankings_ServerId_SampledAtUtc",
                table: "study_sample_rankings",
                columns: new[] { "ServerId", "SampledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_server_wipe_observations_ServerId_ObservedAtUtc",
                table: "study_server_wipe_observations",
                columns: new[] { "ServerId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_table_write_samples_TableName_SampledAtUtc",
                table: "study_table_write_samples",
                columns: new[] { "TableName", "SampledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_population_samples");

            migrationBuilder.DropTable(
                name: "study_resource_samples");

            migrationBuilder.DropTable(
                name: "study_sample_rankings");

            migrationBuilder.DropTable(
                name: "study_server_wipe_observations");

            migrationBuilder.DropTable(
                name: "study_table_write_samples");
        }
    }
}
