using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class McpCollectionCapability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add an optional bit; no stored connection, authorization or token is upgraded.
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
                ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
                  (("ProfileId"='ollama-qwen3-8b' AND "LibraryId" IS NULL AND "McpCapabilities"=0)
                    OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=1)
                    OR ("ProfileId"='mcp-library-import' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=3)
                    OR ("ProfileId"='mcp-library' AND "LibraryId" IS NOT NULL AND "McpCapabilities" IN (1,3,5,7,9,11,13,15)))
                  AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Approved collection grants cannot be narrowed by schema rollback. Revoke affected connections and restore a verified matching application/backup.");
        }
    }
}
