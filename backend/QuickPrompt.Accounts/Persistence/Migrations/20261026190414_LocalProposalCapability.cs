using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalProposalCapability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "proposal_results",
                schema: "qp_operations",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_results", x => x.ExecutionId);
                    table.ForeignKey(
                        name: "FK_proposal_results_executions_ExecutionId_OwnerId_LibraryId",
                        columns: x => new { x.ExecutionId, x.OwnerId, x.LibraryId },
                        principalSchema: "qp_operations",
                        principalTable: "executions",
                        principalColumns: new[] { "Id", "OwnerId", "LibraryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_results_ExecutionId_OwnerId_LibraryId",
                schema: "qp_operations",
                table: "proposal_results",
                columns: new[] { "ExecutionId", "OwnerId", "LibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_proposal_results_OwnerId_LibraryId_CreatedAt",
                schema: "qp_operations",
                table: "proposal_results",
                columns: new[] { "OwnerId", "LibraryId", "CreatedAt" });

            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.proposal_results ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.proposal_results FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_operations.proposal_results FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT ON qp_operations.proposal_results TO quickprompt_app;
                GRANT SELECT,INSERT ON qp_operations.proposal_results TO quickprompt_execution;
                CREATE POLICY proposal_owner_read ON qp_operations.proposal_results FOR SELECT TO quickprompt_app
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY proposal_dispatch_read ON qp_operations.proposal_results FOR SELECT TO quickprompt_execution
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY proposal_dispatch_insert ON qp_operations.proposal_results FOR INSERT TO quickprompt_execution
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid
                    AND EXISTS (SELECT 1 FROM qp_operations.executions job WHERE job."Id"="ExecutionId"
                      AND job."OwnerId"=proposal_results."OwnerId" AND job."LibraryId"=proposal_results."LibraryId"
                      AND job."Kind"='ModelProposal' AND job."Status"='Running' AND job."ForgottenAt" IS NULL));
                CREATE POLICY retention_proposal_read ON qp_operations.proposal_results FOR SELECT TO quickprompt_migrator USING (true);
                CREATE POLICY retention_proposal_erase ON qp_operations.proposal_results FOR DELETE TO quickprompt_migrator USING (true);
                ALTER TABLE qp_operations.proposal_results ADD CONSTRAINT bounded_proposal_result
                  CHECK (jsonb_typeof("Payload")='object' AND octet_length("Payload"::text)<=256000);

                ALTER TABLE qp_operations.executions DROP CONSTRAINT execution_state_shape;
                ALTER TABLE qp_operations.executions ADD CONSTRAINT execution_state_shape CHECK (
                  (("Kind"='CompilePrompt' AND "ConnectionId" IS NULL) OR ("Kind" IN ('RunLocalPrompt','ModelProposal') AND "ConnectionId" IS NOT NULL))
                  AND "Attempts" BETWEEN 0 AND 1 AND octet_length("Payload"::text)<=1048576
                  AND (("Status"='Queued' AND "Attempts"=0 AND "StartedAt" IS NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)
                    OR ("Status"='Running' AND "Attempts"=1 AND "StartedAt" IS NOT NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NOT NULL AND "LeaseUntil">"StartedAt")
                    OR ("Status" IN ('Completed','Failed','Cancelled','Interrupted') AND "FinishedAt" IS NOT NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)));
                DROP POLICY result_dispatch_insert ON qp_operations.execution_results;
                CREATE POLICY result_dispatch_insert ON qp_operations.execution_results FOR INSERT TO quickprompt_execution
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid
                    AND EXISTS (SELECT 1 FROM qp_operations.executions job WHERE job."Id"="ExecutionId"
                      AND job."OwnerId"=execution_results."OwnerId" AND job."LibraryId"=execution_results."LibraryId"
                      AND job."Kind" IN ('CompilePrompt','RunLocalPrompt') AND job."Status"='Running' AND job."ForgottenAt" IS NULL));
                ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
                ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
                  (("ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals') AND "LibraryId" IS NULL AND "McpCapabilities"=0)
                    OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=1)
                    OR ("ProfileId"='mcp-library-import' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=3)
                    OR ("ProfileId"='mcp-library' AND "LibraryId" IS NOT NULL AND "McpCapabilities" IN (1,3,5,7,9,11,13,15)))
                  AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');

                CREATE OR REPLACE FUNCTION qp_operations.erase_execution_content(target uuid) RETURNS integer
                LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $erase$
                DECLARE job qp_operations.executions%ROWTYPE;
                BEGIN
                  SELECT * INTO job FROM qp_operations.executions WHERE "Id"=target;
                  IF NOT FOUND THEN RETURN 0; END IF;
                  IF job."Status" IN ('Queued','Running') THEN RETURN 3; END IF;
                  IF job."ForgottenAt" IS NOT NULL THEN RETURN 2; END IF;
                  PERFORM 1 FROM qp_operations.executions WHERE "Id"=target FOR UPDATE;
                  DELETE FROM qp_operations.execution_results WHERE "ExecutionId"=target;
                  DELETE FROM qp_operations.proposal_results WHERE "ExecutionId"=target;
                  UPDATE qp_operations.executions SET "Payload"='{}'::jsonb,"ForgottenAt"=clock_timestamp() WHERE "Id"=target;
                  RETURN 1;
                END $erase$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Private proposal data and approved grants require a matching verified backup and application; no destructive schema rollback.");
        }
    }
}
