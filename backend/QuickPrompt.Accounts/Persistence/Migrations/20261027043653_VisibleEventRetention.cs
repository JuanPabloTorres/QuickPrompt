using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VisibleEventRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE POLICY visible_retention_run ON qp_operations.agent_runs FOR SELECT TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor());
                CREATE POLICY visible_retention_event_read ON qp_operations.agent_run_events FOR SELECT TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor());
                CREATE POLICY visible_retention_event_delete ON qp_operations.agent_run_events FOR DELETE TO quickprompt_migrator
                  USING ("OwnerId"=qp_library.current_actor());
                CREATE FUNCTION qp_operations.expire_agent_event_content() RETURNS integer
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $expire$
                DECLARE removed integer;
                BEGIN
                  DELETE FROM qp_operations.agent_run_events WHERE ctid IN (
                    SELECT entry.ctid FROM qp_operations.agent_run_events entry
                    JOIN qp_operations.agent_runs run ON run."OwnerId"=entry."OwnerId" AND run."RunId"=entry."RunId"
                    WHERE entry."OwnerId"=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid
                      AND run."Terminal" AND run."CreatedAt"<clock_timestamp()-interval '7 days'
                      AND (run."Record"->>'UpdatedAt')::timestamptz<clock_timestamp()-interval '7 days'
                    ORDER BY entry."RunId",entry."Sequence" LIMIT 1000);
                  GET DIAGNOSTICS removed=ROW_COUNT;
                  RETURN removed;
                END $expire$;
                REVOKE ALL ON FUNCTION qp_operations.expire_agent_event_content() FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT EXECUTE ON FUNCTION qp_operations.expire_agent_event_content() TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Visible-event retention is a privacy boundary; restore a matching verified backup.");
    }
}
