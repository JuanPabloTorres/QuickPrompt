using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionActiveStateCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION qp_operations.erase_execution_content(target uuid) RETURNS integer
                LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $erase$
                DECLARE job qp_operations.executions%ROWTYPE;
                BEGIN
                  -- Both authorized entry points already hold the queue fence. Read active state
                  -- before FOR UPDATE, whose terminal-only RLS policy deliberately hides active rows.
                  SELECT * INTO job FROM qp_operations.executions WHERE "Id"=target;
                  IF NOT FOUND THEN RETURN 0; END IF;
                  IF job."Status" IN ('Queued','Running') THEN RETURN 3; END IF;
                  IF job."ForgottenAt" IS NOT NULL THEN RETURN 2; END IF;
                  PERFORM 1 FROM qp_operations.executions WHERE "Id"=target FOR UPDATE;
                  DELETE FROM qp_operations.execution_results WHERE "ExecutionId"=target;
                  UPDATE qp_operations.executions SET "Payload"='{}'::jsonb,"ForgottenAt"=clock_timestamp() WHERE "Id"=target;
                  RETURN 1;
                END $erase$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The corrected erasure boundary cannot be reverted independently. Use a matching, verified application and backup.");
        }
    }
}
