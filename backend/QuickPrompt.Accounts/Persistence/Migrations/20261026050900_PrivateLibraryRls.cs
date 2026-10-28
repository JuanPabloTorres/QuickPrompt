using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuickPrompt.Accounts.Persistence.Migrations;

[DbContext(typeof(AccountDatabase))]
[Migration("20261026050900_PrivateLibraryRls")]
public sealed class PrivateLibraryRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION qp_library.current_actor() RETURNS uuid
        LANGUAGE sql STABLE PARALLEL SAFE
        AS $$ SELECT NULLIF(current_setting('quickprompt.actor_id', true), '')::uuid $$;
        REVOKE ALL ON FUNCTION qp_library.current_actor() FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION qp_library.current_actor() TO quickprompt_app;

        ALTER TABLE qp_library.libraries ENABLE ROW LEVEL SECURITY;
        ALTER TABLE qp_library.libraries FORCE ROW LEVEL SECURITY;
        CREATE POLICY private_library ON qp_library.libraries
          USING ("OwnerId" = qp_library.current_actor())
          WITH CHECK ("OwnerId" = qp_library.current_actor());

        ALTER TABLE qp_library.prompts ENABLE ROW LEVEL SECURITY;
        ALTER TABLE qp_library.prompts FORCE ROW LEVEL SECURITY;
        CREATE POLICY private_prompt ON qp_library.prompts
          USING (EXISTS (SELECT 1 FROM qp_library.libraries l
            WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()))
          WITH CHECK (EXISTS (SELECT 1 FROM qp_library.libraries l
            WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Removing library RLS is not a supported rollback. Restore a verified backup with its matching application/schema instead.");
}
