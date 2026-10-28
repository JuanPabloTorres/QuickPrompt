using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalModelExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConnectionId",
                schema: "qp_operations",
                table: "executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                schema: "qp_operations",
                table: "execution_results",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Response",
                schema: "qp_operations",
                table: "execution_results",
                type: "character varying(64000)",
                maxLength: 64000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_ConnectionId_OwnerId",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "ConnectionId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_ConnectionId_Status_CreatedAt",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "ConnectionId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_executions_connections_ConnectionId_OwnerId",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "ConnectionId", "OwnerId" },
                principalSchema: "qp_operations",
                principalTable: "connections",
                principalColumns: new[] { "Id", "OwnerId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.executions DROP CONSTRAINT execution_state_shape;
                ALTER TABLE qp_operations.executions ADD CONSTRAINT execution_state_shape CHECK (
                  (("Kind"='CompilePrompt' AND "ConnectionId" IS NULL) OR ("Kind"='RunLocalPrompt' AND "ConnectionId" IS NOT NULL))
                  AND "Attempts" BETWEEN 0 AND 1 AND octet_length("Payload"::text)<=1048576
                  AND (("Status"='Queued' AND "Attempts"=0 AND "StartedAt" IS NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)
                    OR ("Status"='Running' AND "Attempts"=1 AND "StartedAt" IS NOT NULL AND "FinishedAt" IS NULL AND "LeaseToken" IS NOT NULL AND "LeaseUntil">"StartedAt")
                    OR ("Status" IN ('Completed','Failed','Cancelled','Interrupted') AND "FinishedAt" IS NOT NULL AND "LeaseToken" IS NULL AND "LeaseUntil" IS NULL)));
                ALTER TABLE qp_operations.execution_results ADD CONSTRAINT bounded_model_response CHECK (
                  ("Response" IS NULL AND "Model" IS NULL)
                  OR ("Response" IS NOT NULL AND char_length("Response") BETWEEN 1 AND 64000 AND "Model"='qwen3:8b'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Local model inputs/results cannot be discarded by rollback. Restore a verified backup with the matching application.");
        }
    }
}
