using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivatePromptApplicationPorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                schema: "qp_library",
                table: "prompts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Defaults",
                schema: "qp_library",
                table: "prompts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "qp_library",
                table: "prompts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "qp_library",
                table: "prompts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                schema: "qp_library",
                table: "prompts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUsedAt",
                schema: "qp_library",
                table: "prompts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Uses",
                schema: "qp_library",
                table: "prompts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                schema: "qp_library",
                table: "prompt_versions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Defaults",
                schema: "qp_library",
                table: "prompt_versions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "qp_library",
                table: "prompt_versions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "prompt_uses",
                schema: "qp_library",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_uses", x => new { x.OwnerId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_prompt_uses_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prompt_uses_prompt_versions_LibraryId_PromptId_Revision",
                        columns: x => new { x.LibraryId, x.PromptId, x.Revision },
                        principalSchema: "qp_library",
                        principalTable: "prompt_versions",
                        principalColumns: new[] { "LibraryId", "PromptId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_LibraryId_IsArchived_IsFavorite_UpdatedAt",
                schema: "qp_library",
                table: "prompts",
                columns: new[] { "LibraryId", "IsArchived", "IsFavorite", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_LibraryId_LastUsedAt",
                schema: "qp_library",
                table: "prompts",
                columns: new[] { "LibraryId", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_uses_LibraryId_OwnerId",
                schema: "qp_library",
                table: "prompt_uses",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_uses_LibraryId_PromptId",
                schema: "qp_library",
                table: "prompt_uses",
                columns: new[] { "LibraryId", "PromptId" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_uses_LibraryId_PromptId_Revision",
                schema: "qp_library",
                table: "prompt_uses",
                columns: new[] { "LibraryId", "PromptId", "Revision" });

            migrationBuilder.Sql("""
                ALTER TABLE qp_library.prompts ADD CONSTRAINT private_prompt_metadata CHECK (
                  "Category" BETWEEN 0 AND 28 AND "Uses">=0 AND jsonb_typeof("Defaults")='object'
                  AND octet_length("Defaults"::text)<=512000);
                ALTER TABLE qp_library.prompt_versions ADD CONSTRAINT private_version_metadata CHECK (
                  "Category" BETWEEN 0 AND 28 AND jsonb_typeof("Defaults")='object'
                  AND octet_length("Defaults"::text)<=512000);
                ALTER TABLE qp_library.prompt_uses ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.prompt_uses FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_library.prompt_uses FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT,INSERT ON qp_library.prompt_uses TO quickprompt_app;
                CREATE POLICY prompt_use_owner_read ON qp_library.prompt_uses FOR SELECT TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor());
                CREATE POLICY prompt_use_owner_insert ON qp_library.prompt_uses FOR INSERT TO quickprompt_app
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries library
                    WHERE library."Id"="LibraryId" AND library."OwnerId"=qp_library.current_actor()));
                ALTER TABLE qp_library.prompt_uses ADD CONSTRAINT bounded_prompt_use CHECK (
                  "Revision">0 AND char_length("RequestHash")=64);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Private definitions and use receipts require a verified matching backup and application, not destructive schema rollback.");
    }
}
