using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistHostDataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qp_host");

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                schema: "qp_host",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_protection_keys", x => x.Id);
                });
            migrationBuilder.Sql("""
                REVOKE ALL ON SCHEMA qp_host FROM PUBLIC,quickprompt_execution;
                GRANT USAGE ON SCHEMA qp_host TO quickprompt_app;
                REVOKE ALL ON qp_host.data_protection_keys FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT,INSERT ON qp_host.data_protection_keys TO quickprompt_app;
                GRANT USAGE,SELECT ON SEQUENCE qp_host."data_protection_keys_Id_seq" TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Host key-ring rollback is blocked. Preserve the additive schema and certificates during application rollback; destructive key removal requires an explicit recovery procedure.");
        }
    }
}
