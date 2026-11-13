using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyPredictionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_prediction_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayedNextWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastWipeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_prediction_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_prediction_snapshots_ServerId_CapturedAtUtc",
                table: "study_prediction_snapshots",
                columns: new[] { "ServerId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_prediction_snapshots");
        }
    }
}
