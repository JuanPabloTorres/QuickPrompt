using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateAgentMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_contexts",
                schema: "qp_library",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_contexts", x => new { x.OwnerId, x.LibraryId, x.SessionId });
                    table.ForeignKey(
                        name: "FK_agent_contexts_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_preferences",
                schema: "qp_library",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_preferences", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_agent_preferences_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "qp_identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_contexts_ExpiresAt",
                schema: "qp_library",
                table: "agent_contexts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_agent_contexts_LibraryId_OwnerId",
                schema: "qp_library",
                table: "agent_contexts",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_contexts_OwnerId_UpdatedAt",
                schema: "qp_library",
                table: "agent_contexts",
                columns: new[] { "OwnerId", "UpdatedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.agent_contexts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.agent_contexts FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.agent_preferences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.agent_preferences FORCE ROW LEVEL SECURITY;
                CREATE POLICY owned_agent_context ON qp_library.agent_contexts
                  USING ("OwnerId" = qp_library.current_actor() AND EXISTS (
                    SELECT 1 FROM qp_library.libraries l WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()))
                  WITH CHECK ("OwnerId" = qp_library.current_actor() AND EXISTS (
                    SELECT 1 FROM qp_library.libraries l WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()));
                CREATE POLICY owned_agent_preferences ON qp_library.agent_preferences
                  USING ("OwnerId" = qp_library.current_actor()) WITH CHECK ("OwnerId" = qp_library.current_actor());
                REVOKE UPDATE ON qp_library.agent_contexts, qp_library.agent_preferences FROM quickprompt_app;
                GRANT UPDATE ("Payload", "Title", "UpdatedAt", "ExpiresAt") ON qp_library.agent_contexts TO quickprompt_app;
                GRANT UPDATE ("Language", "Format", "ProviderId") ON qp_library.agent_preferences TO quickprompt_app;
                ALTER TABLE qp_library.agent_contexts ADD CONSTRAINT bounded_agent_context
                  CHECK (octet_length("Payload"::text) <= 1048576 AND "ExpiresAt" > "UpdatedAt"
                    AND "ExpiresAt" <= "UpdatedAt" + interval '7 days');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Remembered private context cannot be dropped by rollback. Restore the verified backup and matching application instead.");
        }
    }
}
