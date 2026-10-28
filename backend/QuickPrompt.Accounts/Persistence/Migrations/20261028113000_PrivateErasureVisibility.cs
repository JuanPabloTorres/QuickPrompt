using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuickPrompt.Accounts.Persistence.Migrations;

/// <summary>Database backstop for callers that do not know the application tombstone filter.</summary>
[DbContext(typeof(AccountDatabase))]
[Migration("20261028113000_PrivateErasureVisibility")]
public sealed class PrivateErasureVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE POLICY erased_prompt_hidden ON qp_library.prompts AS RESTRICTIVE
          FOR SELECT TO quickprompt_app USING ("DeletedAt" IS NULL);
        CREATE POLICY erased_version_hidden ON qp_library.prompt_versions AS RESTRICTIVE
          FOR SELECT TO quickprompt_app USING (EXISTS (
            SELECT 1 FROM qp_library.prompts p
            WHERE p."LibraryId"=prompt_versions."LibraryId" AND p."Id"=prompt_versions."PromptId" AND p."DeletedAt" IS NULL));
        CREATE POLICY erased_version_insert_rejected ON qp_library.prompt_versions AS RESTRICTIVE
          FOR INSERT TO quickprompt_app WITH CHECK (EXISTS (
            SELECT 1 FROM qp_library.prompts p
            WHERE p."LibraryId"=prompt_versions."LibraryId" AND p."Id"=prompt_versions."PromptId" AND p."DeletedAt" IS NULL));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Private erasure visibility cannot be rolled back after content erasure. Roll forward with a compatible binary.");
}
