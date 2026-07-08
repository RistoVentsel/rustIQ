using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerQueuePlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE servers
                ADD COLUMN IF NOT EXISTS "QueuePlayers" integer;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE servers
                DROP COLUMN IF EXISTS "QueuePlayers";
                """);
        }
    }
}
