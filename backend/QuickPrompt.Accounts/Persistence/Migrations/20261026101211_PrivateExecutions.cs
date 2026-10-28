using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qp_operations");

            migrationBuilder.CreateTable(
                name: "executions",
                schema: "qp_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                    table.UniqueConstraint("AK_executions_Id_OwnerId_LibraryId", x => new { x.Id, x.OwnerId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_executions_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "execution_results",
                schema: "qp_operations",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptRevision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Text = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_results", x => x.ExecutionId);
                    table.ForeignKey(
                        name: "FK_execution_results_executions_ExecutionId_OwnerId_LibraryId",
                        columns: x => new { x.ExecutionId, x.OwnerId, x.LibraryId },
                        principalSchema: "qp_operations",
                        principalTable: "executions",
                        principalColumns: new[] { "Id", "OwnerId", "LibraryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_results_ExecutionId_OwnerId_LibraryId",
                schema: "qp_operations",
                table: "execution_results",
                columns: new[] { "ExecutionId", "OwnerId", "LibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_results_OwnerId_LibraryId_CreatedAt",
                schema: "qp_operations",
                table: "execution_results",
                columns: new[] { "OwnerId", "LibraryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_LeaseUntil",
                schema: "qp_operations",
                table: "executions",
                column: "LeaseUntil");

            migrationBuilder.CreateIndex(
                name: "IX_executions_LibraryId_OwnerId",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_OwnerId_OperationId",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "OwnerId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_OwnerId_Status",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_Status_CreatedAt",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "Status", "CreatedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.executions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.executions FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.execution_results ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.execution_results FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_operations.executions, qp_operations.execution_results FROM quickprompt_app;
                GRANT SELECT ON qp_operations.executions, qp_operations.execution_results TO quickprompt_app;
                GRANT UPDATE ("CancelRequested") ON qp_operations.executions TO quickprompt_app;
                GRANT SELECT, INSERT ON qp_operations.executions TO quickprompt_execution;
                GRANT UPDATE ("Status", "StartedAt", "FinishedAt", "LeaseToken", "LeaseUntil", "Attempts", "ErrorCode")
                  ON qp_operations.executions TO quickprompt_execution;
                GRANT SELECT, INSERT ON qp_operations.execution_results TO quickprompt_execution;
                CREATE POLICY execution_owner_read ON qp_operations.executions FOR SELECT TO quickprompt_app
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY execution_owner_cancel ON qp_operations.executions FOR UPDATE TO quickprompt_app
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid AND "Status" IN ('Queued','Running'))
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid AND "CancelRequested");
                CREATE POLICY execution_dispatch_read ON qp_operations.executions FOR SELECT TO quickprompt_execution USING (true);
                CREATE POLICY execution_dispatch_insert ON qp_operations.executions FOR INSERT TO quickprompt_execution
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid
                    AND "Status"='Queued' AND NOT "CancelRequested" AND "Attempts"=0
                    AND "StartedAt" IS NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL AND "ErrorCode" IS NULL);
                CREATE POLICY execution_dispatch_update ON qp_operations.executions FOR UPDATE TO quickprompt_execution USING (true) WITH CHECK (true);
                CREATE POLICY result_owner_read ON qp_operations.execution_results FOR SELECT TO quickprompt_app
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY result_dispatch_read ON qp_operations.execution_results FOR SELECT TO quickprompt_execution
                  USING ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                CREATE POLICY result_dispatch_insert ON qp_operations.execution_results FOR INSERT TO quickprompt_execution
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid);
                ALTER TABLE qp_operations.executions ADD CONSTRAINT execution_state_shape CHECK (
                  "Kind"='CompilePrompt' AND "Attempts" BETWEEN 0 AND 1 AND octet_length("Payload"::text)<=1048576
                  AND (("Status"='Queued' AND "Attempts"=0 AND "StartedAt" IS NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)
                    OR ("Status"='Running' AND "Attempts"=1 AND "StartedAt" IS NOT NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NOT NULL AND "LeaseUntil">"StartedAt")
                    OR ("Status" IN ('Completed','Failed','Cancelled','Interrupted') AND "FinishedAt" IS NOT NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)));
                ALTER TABLE qp_operations.execution_results ADD CONSTRAINT bounded_execution_result
                  CHECK ("PromptRevision">0 AND char_length("Text")<=64000);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Private execution history cannot be dropped by rollback. Restore the verified backup and matching application instead.");
        }
    }
}
