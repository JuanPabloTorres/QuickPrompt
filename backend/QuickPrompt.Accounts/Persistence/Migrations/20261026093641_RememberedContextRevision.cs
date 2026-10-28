using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RememberedContextRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "qp_library",
                table: "agent_contexts",
                type: "integer",
                nullable: false,
                defaultValue: 1);
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.agent_contexts ADD CONSTRAINT agent_context_revision_positive CHECK ("Revision" > 0);
                GRANT UPDATE ("Revision") ON qp_library.agent_contexts TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Context revision guards cannot be removed by rollback. Restore the verified backup and matching application instead.");
        }
    }
}
