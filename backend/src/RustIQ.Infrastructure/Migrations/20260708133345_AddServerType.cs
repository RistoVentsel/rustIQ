using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServerType",
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
                name: "ServerType",
                table: "servers");
        }
    }
}
