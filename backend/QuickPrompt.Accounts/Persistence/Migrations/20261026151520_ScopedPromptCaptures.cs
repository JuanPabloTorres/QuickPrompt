using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopedPromptCaptures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "McpCapabilities",
                schema: "qp_operations",
                table: "connections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "captures",
                schema: "qp_library",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_captures", x => new { x.OwnerId, x.OperationId });
                    table.CheckConstraint("capture_initial_version", "\"Revision\"=1");
                    table.ForeignKey(
                        name: "FK_captures_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_captures_prompt_versions_LibraryId_PromptId_Revision",
                        columns: x => new { x.LibraryId, x.PromptId, x.Revision },
                        principalSchema: "qp_library",
                        principalTable: "prompt_versions",
                        principalColumns: new[] { "LibraryId", "PromptId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_captures_LibraryId_OwnerId",
                schema: "qp_library",
                table: "captures",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_captures_LibraryId_PromptId_Revision",
                schema: "qp_library",
                table: "captures",
                columns: new[] { "LibraryId", "PromptId", "Revision" });
            migrationBuilder.Sql("""
                -- The schema owner needs a transaction-local upgrade policy on a forced-RLS table.
                -- Application policies and privileges remain unchanged; this policy is removed before commit.
                CREATE POLICY connection_capability_upgrade ON qp_operations.connections FOR ALL TO quickprompt_migrator USING (true) WITH CHECK (true);
                UPDATE qp_operations.connections SET "McpCapabilities"=CASE "ProfileId" WHEN 'mcp-library-read' THEN 1 WHEN 'mcp-library-import' THEN 3 ELSE 0 END;
                DROP POLICY connection_capability_upgrade ON qp_operations.connections;
                ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
                ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
                  (("ProfileId"='ollama-qwen3-8b' AND "LibraryId" IS NULL AND "McpCapabilities"=0)
                    OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=1)
                    OR ("ProfileId"='mcp-library-import' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=3)
                    OR ("ProfileId"='mcp-library' AND "LibraryId" IS NOT NULL AND "McpCapabilities" IN (1,3,5,7)))
                  AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
                ALTER TABLE qp_library.captures ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.captures FORCE ROW LEVEL SECURITY;
                REVOKE UPDATE, DELETE ON qp_library.captures FROM quickprompt_app;
                CREATE POLICY capture_owner_read ON qp_library.captures FOR SELECT TO quickprompt_app
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY capture_owner_insert ON qp_library.captures FOR INSERT TO quickprompt_app
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                ALTER TABLE qp_library.captures ADD CONSTRAINT capture_hash_valid CHECK ("ContentHash" ~ '^[A-F0-9]{64}$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Private creation receipts and capability history cannot be dropped. Restore a verified backup with its matching application.");
        }
    }
}
