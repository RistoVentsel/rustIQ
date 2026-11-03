using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyServerDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_server_descriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Answered = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_server_descriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_server_descriptions_ServerId_CapturedAtUtc",
                table: "study_server_descriptions",
                columns: new[] { "ServerId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_server_descriptions");
        }
    }
}
