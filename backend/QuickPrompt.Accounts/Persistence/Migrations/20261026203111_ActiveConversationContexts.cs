using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActiveConversationContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_sessions",
                schema: "qp_library",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_sessions", x => new { x.OwnerId, x.LibraryId, x.SessionId });
                    table.ForeignKey(
                        name: "FK_agent_sessions_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_LibraryId_OwnerId",
                schema: "qp_library",
                table: "agent_sessions",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_OwnerId_ExpiresAt",
                schema: "qp_library",
                table: "agent_sessions",
                columns: new[] { "OwnerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_OwnerId_UpdatedAt",
                schema: "qp_library",
                table: "agent_sessions",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.Sql("""
                ALTER TABLE qp_library.agent_sessions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.agent_sessions FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_library.agent_sessions FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT,INSERT,DELETE ON qp_library.agent_sessions TO quickprompt_app;
                GRANT UPDATE ("Revision","Payload","UpdatedAt","ExpiresAt") ON qp_library.agent_sessions TO quickprompt_app;
                CREATE POLICY active_session_owner ON qp_library.agent_sessions FOR ALL TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor())
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (
                    SELECT 1 FROM qp_library.libraries library WHERE library."Id"="LibraryId"
                      AND library."OwnerId"=qp_library.current_actor()));
                ALTER TABLE qp_library.agent_sessions ADD CONSTRAINT bounded_active_session CHECK ((
                  "Revision">0 AND jsonb_typeof("Payload")='object' AND octet_length("Payload"::text)<=1048576
                  AND "Payload" ?& ARRAY['id','approval','values']
                  AND "Payload"->>'id'="SessionId"::text AND "Payload"->>'approval'='NOT_GRANTED'
                  AND jsonb_typeof("Payload"->'values')='object'
                  AND "ExpiresAt">"UpdatedAt" AND "ExpiresAt"<="UpdatedAt"+interval '2 hours') IS TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Active conversations require a matching verified backup; destructive schema rollback is unsupported.");
    }
}
