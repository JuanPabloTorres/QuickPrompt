using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionSchedulingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_executions_OwnerId_CreatedAt",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_OwnerId_StartedAt",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "OwnerId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executions_OwnerId_CreatedAt",
                schema: "qp_operations",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_OwnerId_StartedAt",
                schema: "qp_operations",
                table: "executions");
        }
    }
}
