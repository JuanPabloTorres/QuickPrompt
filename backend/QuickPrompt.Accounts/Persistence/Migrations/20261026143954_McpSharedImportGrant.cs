using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class McpSharedImportGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
                ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
                  (("ProfileId"='ollama-qwen3-8b' AND "LibraryId" IS NULL)
                    OR ("ProfileId" IN ('mcp-library-read','mcp-library-import') AND "LibraryId" IS NOT NULL))
                  AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Import grants cannot be reinterpreted by rollback. Restore a verified backup with its matching application.");
        }
    }
}
