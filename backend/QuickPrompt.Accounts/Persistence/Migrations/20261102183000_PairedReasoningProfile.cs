using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations;

/// <summary>Adds one explicitly consented worker profile. Columns, scopes, tenant checks and business effects are unchanged.</summary>
public partial class PairedReasoningProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
        ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
          (("ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals','ollama-qwen3-8b-proposals-reasoning-1') AND "LibraryId" IS NULL AND "McpCapabilities"=0)
            OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=1)
            OR ("ProfileId"='mcp-library-import' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=3)
            OR ("ProfileId"='mcp-library' AND "LibraryId" IS NOT NULL AND "McpCapabilities" IN (1,3,5,7,9,11,13,15)))
          AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
        ALTER POLICY companion_presence_actor ON qp_operations.companion_presence
          USING ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid)
          WITH CHECK ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid
            AND EXISTS (SELECT 1 FROM qp_operations.connections c WHERE c."Id"="ConnectionId" AND c."OwnerId"=companion_presence."OwnerId"
              AND c."ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals','ollama-qwen3-8b-proposals-reasoning-1') AND c."LibraryId" IS NULL));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- A validated CHECK sees all rows even under FORCE RLS, without granting
        -- the migrator account-content access. Its DDL lock lasts to commit.
        DO $guard$ BEGIN
          BEGIN
            ALTER TABLE qp_operations.connections ADD CONSTRAINT rollback_without_reasoning_profiles
              CHECK ("ProfileId"<>'ollama-qwen3-8b-proposals-reasoning-1');
          EXCEPTION WHEN check_violation THEN
            RAISE EXCEPTION 'Reasoning-profile connections exist. Review their retention and removal explicitly before rollback; no grant will be converted or deleted.';
          END;
          ALTER TABLE qp_operations.connections DROP CONSTRAINT rollback_without_reasoning_profiles;
        END $guard$;
        ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;
        ALTER TABLE qp_operations.connections ADD CONSTRAINT bounded_connection CHECK (
          (("ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals') AND "LibraryId" IS NULL AND "McpCapabilities"=0)
            OR ("ProfileId"='mcp-library-read' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=1)
            OR ("ProfileId"='mcp-library-import' AND "LibraryId" IS NOT NULL AND "McpCapabilities"=3)
            OR ("ProfileId"='mcp-library' AND "LibraryId" IS NOT NULL AND "McpCapabilities" IN (1,3,5,7,9,11,13,15)))
          AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '30 days');
        ALTER POLICY companion_presence_actor ON qp_operations.companion_presence
          USING ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid)
          WITH CHECK ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid
            AND EXISTS (SELECT 1 FROM qp_operations.connections c WHERE c."Id"="ConnectionId" AND c."OwnerId"=companion_presence."OwnerId"
              AND c."ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals') AND c."LibraryId" IS NULL));
        """);
}
