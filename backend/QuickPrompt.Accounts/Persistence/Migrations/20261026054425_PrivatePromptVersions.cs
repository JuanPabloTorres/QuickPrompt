using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivatePromptVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prompt_versions",
                schema: "qp_library",
                columns: table => new
                {
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Template = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_versions", x => new { x.LibraryId, x.PromptId, x.Revision });
                    table.ForeignKey(
                        name: "FK_prompt_versions_prompts_LibraryId_PromptId",
                        columns: x => new { x.LibraryId, x.PromptId },
                        principalSchema: "qp_library",
                        principalTable: "prompts",
                        principalColumns: new[] { "LibraryId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.prompt_versions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.prompt_versions FORCE ROW LEVEL SECURITY;
                CREATE POLICY private_prompt_version ON qp_library.prompt_versions
                  USING (EXISTS (SELECT 1 FROM qp_library.libraries l
                    WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()))
                  WITH CHECK (EXISTS (SELECT 1 FROM qp_library.libraries l
                    WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()));
                REVOKE UPDATE ON qp_library.prompt_versions FROM quickprompt_app;
                DO $backfill$
                DECLARE owner_id uuid;
                BEGIN
                  FOR owner_id IN SELECT "Id" FROM qp_identity."AspNetUsers" LOOP
                    PERFORM set_config('quickprompt.actor_id', owner_id::text, true);
                    INSERT INTO qp_library.prompt_versions ("LibraryId","PromptId","Revision","Title","Template","CreatedAt")
                    SELECT "LibraryId","Id","Revision","Title","Template","UpdatedAt" FROM qp_library.prompts
                    WHERE "LibraryId" IN (SELECT "Id" FROM qp_library.libraries WHERE "OwnerId"=owner_id);
                  END LOOP;
                  PERFORM set_config('quickprompt.actor_id', '', true);
                END
                $backfill$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Private version history cannot be dropped by a rollback. Restore the verified backup and matching application instead.");
        }
    }
}
