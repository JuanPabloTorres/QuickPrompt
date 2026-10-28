using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivatePromptErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prompts_LibraryId_UpdatedAt",
                schema: "qp_library",
                table: "prompts");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "qp_library",
                table: "prompts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompts_LibraryId_UpdatedAt",
                schema: "qp_library",
                table: "prompts",
                columns: new[] { "LibraryId", "UpdatedAt" },
                filter: "\"DeletedAt\" IS NULL");
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.prompts ADD CONSTRAINT private_prompt_erased_shape CHECK (
                  "DeletedAt" IS NULL OR ("Title"='' AND "Template"='' AND "Description"=''
                    AND "Defaults"='{}'::jsonb AND "Category"=0 AND NOT "IsFavorite" AND "IsArchived"
                    AND "Uses"=0 AND "LastUsedAt" IS NULL));

                -- The non-BYPASSRLS function owner receives only actor/library-bound
                -- collection cleanup policies. Application privileges do not change.
                CREATE POLICY erasure_collection_read ON qp_library.collections FOR SELECT TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l
                    WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()));
                CREATE POLICY erasure_collection_change ON qp_library.collections FOR UPDATE TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l
                    WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()))
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l
                    WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()));
                CREATE POLICY erasure_member_read ON qp_library.collection_members FOR SELECT TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.collections c
                    WHERE c."Id"="CollectionId" AND c."LibraryId"=collection_members."LibraryId" AND c."OwnerId"=qp_library.current_actor()));
                CREATE POLICY erasure_member_delete ON qp_library.collection_members FOR DELETE TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.collections c
                    WHERE c."Id"="CollectionId" AND c."LibraryId"=collection_members."LibraryId" AND c."OwnerId"=qp_library.current_actor()));

                CREATE FUNCTION qp_library.erase_private_prompt(library uuid,target uuid,expected_revision integer) RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $erase$
                DECLARE actor uuid := NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid;
                  source qp_library.prompts%ROWTYPE; groups uuid[];
                BEGIN
                  IF actor IS NULL OR expected_revision<1 OR NOT EXISTS (SELECT 1 FROM qp_library.libraries
                    WHERE "Id"=library AND "OwnerId"=actor) THEN RETURN 0; END IF;
                  -- Same admission and per-library collection lock order as the existing owners.
                  PERFORM pg_advisory_xact_lock(5283,0);
                  PERFORM pg_advisory_xact_lock(5284,hashtext(replace(actor::text,'-','')||replace(library::text,'-','')));
                  SELECT * INTO source FROM qp_library.prompts WHERE "LibraryId"=library AND "Id"=target FOR UPDATE;
                  IF NOT FOUND THEN RETURN 0; END IF;
                  IF source."DeletedAt" IS NOT NULL THEN
                    IF source."Revision"=expected_revision+1 THEN RETURN 2; ELSE RETURN 3; END IF;
                  END IF;
                  IF source."Revision"<>expected_revision THEN RETURN 3; END IF;
                  IF EXISTS (SELECT 1 FROM qp_operations.agent_runs WHERE "OwnerId"=actor AND "LibraryId"=library
                    AND NOT "Terminal" AND "LeaseUntil">clock_timestamp())
                    OR EXISTS (SELECT 1 FROM qp_operations.executions WHERE "OwnerId"=actor AND "LibraryId"=library
                      AND "Status" IN ('Queued','Running')) THEN RETURN 4; END IF;
                  SELECT array_agg("CollectionId") INTO groups FROM qp_library.collection_members
                    WHERE "OwnerId"=actor AND "LibraryId"=library AND "PromptId"=target;
                  DELETE FROM qp_library.collection_members WHERE "OwnerId"=actor AND "LibraryId"=library AND "PromptId"=target;
                  UPDATE qp_library.collections SET "Revision"="Revision"+1,"UpdatedAt"=clock_timestamp()
                    WHERE "OwnerId"=actor AND "LibraryId"=library AND "Id"=ANY(groups);
                  UPDATE qp_library.prompt_versions SET "Title"='',"Template"='',"Description"='',"Defaults"='{}'::jsonb,"Category"=0
                    WHERE "LibraryId"=library AND "PromptId"=target;
                  UPDATE qp_library.prompts SET "Title"='',"Template"='',"Description"='',"Defaults"='{}'::jsonb,"Category"=0,
                    "IsFavorite"=false,"IsArchived"=true,"Uses"=0,"LastUsedAt"=NULL,
                    "Revision"="Revision"+1,"UpdatedAt"=clock_timestamp(),"DeletedAt"=clock_timestamp()
                    WHERE "LibraryId"=library AND "Id"=target;
                  RETURN 1;
                END $erase$;
                ALTER FUNCTION qp_library.erase_private_prompt(uuid,uuid,integer) OWNER TO quickprompt_migrator;
                REVOKE ALL ON FUNCTION qp_library.erase_private_prompt(uuid,uuid,integer) FROM PUBLIC,quickprompt_execution;
                GRANT EXECUTE ON FUNCTION qp_library.erase_private_prompt(uuid,uuid,integer) TO quickprompt_app;

                -- Tombstones cannot be restored or rewritten by a forgotten adapter.
                CREATE FUNCTION qp_library.protect_erased_prompt() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $protect$
                BEGIN
                  IF OLD."DeletedAt" IS NOT NULL THEN RAISE EXCEPTION 'Deleted prompt identity is immutable' USING ERRCODE='42501'; END IF;
                  IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $protect$;
                REVOKE ALL ON FUNCTION qp_library.protect_erased_prompt() FROM PUBLIC;
                CREATE TRIGGER erased_prompt_immutable BEFORE UPDATE OR DELETE ON qp_library.prompts
                  FOR EACH ROW EXECUTE FUNCTION qp_library.protect_erased_prompt();
                CREATE FUNCTION qp_library.reject_erased_prompt_version() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $version$
                BEGIN
                  IF EXISTS (SELECT 1 FROM qp_library.prompts WHERE "LibraryId"=NEW."LibraryId" AND "Id"=NEW."PromptId" AND "DeletedAt" IS NOT NULL)
                    THEN RAISE EXCEPTION 'Deleted prompt cannot receive versions' USING ERRCODE='42501'; END IF;
                  RETURN NEW;
                END $version$;
                REVOKE ALL ON FUNCTION qp_library.reject_erased_prompt_version() FROM PUBLIC;
                CREATE TRIGGER erased_prompt_version_rejected BEFORE INSERT ON qp_library.prompt_versions
                  FOR EACH ROW EXECUTE FUNCTION qp_library.reject_erased_prompt_version();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Private erasure identities and receipts cannot be rolled back. Restore a verified backup with its matching application.");
    }
}
