using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LibraryBoundMcpDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                schema: "qp_operations",
                table: "connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_connections_LibraryId_OwnerId",
                schema: "qp_operations",
                table: "connections",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_connections_libraries_LibraryId_OwnerId",
                schema: "qp_operations",
                table: "connections",
                columns: new[] { "LibraryId", "OwnerId" },
                principalSchema: "qp_library",
                principalTable: "libraries",
                principalColumns: new[] { "Id", "OwnerId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
                ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
                  (("ProfileId"='ollama-qwen3-8b' AND "LibraryId" IS NULL)
                    OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL))
                  AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Library-scoped grants cannot be widened by rollback. Restore a verified backup with its matching application.");
        }
    }
}
