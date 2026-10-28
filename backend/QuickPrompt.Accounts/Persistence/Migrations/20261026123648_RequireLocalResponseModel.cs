using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireLocalResponseModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.execution_results DROP CONSTRAINT bounded_model_response;
                ALTER TABLE qp_operations.execution_results ADD CONSTRAINT bounded_model_response CHECK (
                  ("Response" IS NULL AND "Model" IS NULL)
                  OR ("Response" IS NOT NULL AND "Model" IS NOT NULL AND char_length("Response") BETWEEN 1 AND 64000 AND "Model"='qwen3:8b'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Do not weaken the model/response invariant. Restore a verified backup with its matching application.");
        }
    }
}
