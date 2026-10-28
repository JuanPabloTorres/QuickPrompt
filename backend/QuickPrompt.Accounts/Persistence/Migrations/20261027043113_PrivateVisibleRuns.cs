using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateVisibleRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_runs",
                schema: "qp_operations",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostToken = table.Column<Guid>(type: "uuid", nullable: false),
                    Record = table.Column<string>(type: "jsonb", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Terminal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DraftHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CommittedPromptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommittedRevision = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => new { x.OwnerId, x.RunId });
                    table.ForeignKey(
                        name: "FK_agent_runs_connections_ConnectionId_OwnerId",
                        columns: x => new { x.ConnectionId, x.OwnerId },
                        principalSchema: "qp_operations",
                        principalTable: "connections",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_runs_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_run_events",
                schema: "qp_operations",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_run_events", x => new { x.OwnerId, x.RunId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_agent_run_events_agent_runs_OwnerId_RunId",
                        columns: x => new { x.OwnerId, x.RunId },
                        principalSchema: "qp_operations",
                        principalTable: "agent_runs",
                        principalColumns: new[] { "OwnerId", "RunId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_ConnectionId_OwnerId",
                schema: "qp_operations",
                table: "agent_runs",
                columns: new[] { "ConnectionId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_LibraryId_OwnerId",
                schema: "qp_operations",
                table: "agent_runs",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_OwnerId_CreatedAt",
                schema: "qp_operations",
                table: "agent_runs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_OwnerId_Terminal_LeaseUntil",
                schema: "qp_operations",
                table: "agent_runs",
                columns: new[] { "OwnerId", "Terminal", "LeaseUntil" });

            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.agent_runs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.agent_runs FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.agent_run_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.agent_run_events FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_operations.agent_runs,qp_operations.agent_run_events FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT,INSERT ON qp_operations.agent_runs,qp_operations.agent_run_events TO quickprompt_app;
                GRANT UPDATE ("Record","Sequence","Terminal","LeaseUntil","ApprovalId","ApprovalHash","DraftHash","CommittedPromptId","CommittedRevision")
                  ON qp_operations.agent_runs TO quickprompt_app;
                CREATE POLICY visible_run_owner ON qp_operations.agent_runs FOR ALL TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor())
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (
                    SELECT 1 FROM qp_library.libraries library WHERE library."Id"="LibraryId" AND library."OwnerId"=qp_library.current_actor()));
                CREATE POLICY visible_event_owner ON qp_operations.agent_run_events FOR ALL TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor()) WITH CHECK ("OwnerId"=qp_library.current_actor());
                ALTER TABLE qp_operations.agent_runs ADD CONSTRAINT bounded_visible_run CHECK ((
                  jsonb_typeof("Record")='object' AND octet_length("Record"::text)<=16384
                  AND "Record"->>'RunId'="RunId"::text AND ("Record"->>'Sequence')::bigint="Sequence"
                  AND "Sequence" BETWEEN 0 AND 1000
                  AND "Terminal"=(("Record"->>'Status') IN ('COMPLETED','CANCELLED','FAILED','BLOCKED'))
                  AND "LeaseUntil"<="CreatedAt"+interval '15 minutes'
                  AND (("CommittedPromptId" IS NULL AND "CommittedRevision" IS NULL AND NOT ("Record"->>'EffectCommitted')::boolean)
                    OR ("CommittedPromptId" IS NOT NULL AND "CommittedRevision">0 AND ("Record"->>'EffectCommitted')::boolean AND "Record"->>'Approval'='CONSUMED'))
                  AND (("ApprovalId" IS NULL AND "ApprovalHash" IS NULL AND "DraftHash" IS NULL)
                    OR ("ApprovalId" IS NOT NULL AND length("ApprovalHash")=64 AND length("DraftHash")=64))) IS TRUE);
                ALTER TABLE qp_operations.agent_run_events ADD CONSTRAINT bounded_visible_event CHECK ((
                  jsonb_typeof("Payload")='object' AND octet_length("Payload"::text)<=16384
                  AND "Payload"->>'RunId'="RunId"::text AND ("Payload"->>'Sequence')::bigint="Sequence"
                  AND "Sequence" BETWEEN 1 AND 1000) IS TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Visible execution receipts require a matching verified backup; destructive rollback is unsupported.");
    }
}
