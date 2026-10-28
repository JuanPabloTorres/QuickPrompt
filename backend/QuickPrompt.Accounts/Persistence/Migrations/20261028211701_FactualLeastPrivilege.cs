using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FactualLeastPrivilege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bootstrap defaults grant app CRUD to new tables. A narrower GRANT does
            // not remove inherited object privileges, so revoke before granting.
            migrationBuilder.Sql("""
                REVOKE ALL ON qp_operations.factual_reviews,qp_library.factual_documents FROM quickprompt_app;
                GRANT SELECT,INSERT,UPDATE ON qp_operations.factual_reviews TO quickprompt_app;
                GRANT SELECT,INSERT,DELETE ON qp_library.factual_documents TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Do not restore unintended UPDATE/DELETE privileges when rolling back factual documents.");
        }
    }
}
