using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionContentRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForgottenAt",
                schema: "qp_operations",
                table: "executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_ForgottenAt_FinishedAt",
                schema: "qp_operations",
                table: "executions",
                columns: new[] { "ForgottenAt", "FinishedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.executions ADD CONSTRAINT execution_forgotten_shape CHECK (
                  "ForgottenAt" IS NULL OR ("Payload"='{}'::jsonb
                    AND "Status" IN ('Completed','Failed','Cancelled','Interrupted')
                    AND "FinishedAt" IS NOT NULL AND "ForgottenAt">="FinishedAt"));

                -- FORCE RLS remains enabled. Only the trusted function owner receives these policies;
                -- neither application nor dispatcher receives general content mutation privileges.
                CREATE POLICY retention_owner_read ON qp_operations.executions FOR SELECT TO quickprompt_migrator USING (true);
                CREATE POLICY retention_owner_erase ON qp_operations.executions FOR UPDATE TO quickprompt_migrator
                  USING ("Status" IN ('Completed','Failed','Cancelled','Interrupted'))
                  WITH CHECK ("Status" IN ('Completed','Failed','Cancelled','Interrupted') AND "ForgottenAt" IS NOT NULL AND "Payload"='{}'::jsonb);
                CREATE POLICY retention_result_read ON qp_operations.execution_results FOR SELECT TO quickprompt_migrator USING (true);
                CREATE POLICY retention_result_erase ON qp_operations.execution_results FOR DELETE TO quickprompt_migrator USING (true);

                -- Private writer shared by two separately authorized entry points. No dynamic SQL,
                -- user-supplied schema, role, retention interval or content replacement is accepted.
                CREATE FUNCTION qp_operations.erase_execution_content(target uuid) RETURNS integer
                LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $erase$
                DECLARE job qp_operations.executions%ROWTYPE;
                BEGIN
                  SELECT * INTO job FROM qp_operations.executions WHERE "Id"=target FOR UPDATE;
                  IF NOT FOUND THEN RETURN 0; END IF;
                  IF job."Status" IN ('Queued','Running') THEN RETURN 3; END IF;
                  IF job."ForgottenAt" IS NOT NULL THEN RETURN 2; END IF;
                  DELETE FROM qp_operations.execution_results WHERE "ExecutionId"=target;
                  UPDATE qp_operations.executions SET "Payload"='{}'::jsonb,"ForgottenAt"=clock_timestamp() WHERE "Id"=target;
                  RETURN 1;
                END $erase$;

                CREATE FUNCTION qp_operations.forget_execution_content(library uuid,target uuid) RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $forget$
                DECLARE actor uuid := NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid;
                BEGIN
                  PERFORM pg_advisory_xact_lock(5283,0);
                  IF actor IS NULL OR NOT EXISTS (SELECT 1 FROM qp_operations.executions
                    WHERE "Id"=target AND "LibraryId"=library AND "OwnerId"=actor) THEN RETURN 0; END IF;
                  RETURN qp_operations.erase_execution_content(target);
                END $forget$;

                CREATE FUNCTION qp_operations.expire_execution_content() RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $expire$
                DECLARE target uuid; erased integer := 0;
                BEGIN
                  PERFORM pg_advisory_xact_lock(5283,0);
                  FOR target IN SELECT "Id" FROM qp_operations.executions
                    WHERE "ForgottenAt" IS NULL AND "FinishedAt"<=clock_timestamp()-interval '7 days'
                      AND "Status" IN ('Completed','Failed','Cancelled','Interrupted')
                    ORDER BY "FinishedAt","Id" LIMIT 100
                  LOOP
                    IF qp_operations.erase_execution_content(target)=1 THEN erased := erased+1; END IF;
                  END LOOP;
                  RETURN erased;
                END $expire$;
                REVOKE ALL ON FUNCTION qp_operations.erase_execution_content(uuid) FROM PUBLIC,quickprompt_app,quickprompt_execution;
                REVOKE ALL ON FUNCTION qp_operations.forget_execution_content(uuid,uuid) FROM PUBLIC,quickprompt_execution;
                REVOKE ALL ON FUNCTION qp_operations.expire_execution_content() FROM PUBLIC,quickprompt_app;
                GRANT EXECUTE ON FUNCTION qp_operations.forget_execution_content(uuid,uuid) TO quickprompt_app;
                GRANT EXECUTE ON FUNCTION qp_operations.expire_execution_content() TO quickprompt_execution;
                DROP POLICY result_dispatch_insert ON qp_operations.execution_results;
                CREATE POLICY result_dispatch_insert ON qp_operations.execution_results FOR INSERT TO quickprompt_execution
                  WITH CHECK ("OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid
                    AND EXISTS (SELECT 1 FROM qp_operations.executions job WHERE job."Id"="ExecutionId"
                      AND job."OwnerId"=execution_results."OwnerId" AND job."LibraryId"=execution_results."LibraryId"
                      AND job."Status"='Running' AND job."ForgottenAt" IS NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Erased execution content cannot be recovered by rollback. Restore only an authorized backup with its matching application and retention policy.");
        }
    }
}
