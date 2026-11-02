using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanionOperationalPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companion_presence",
                schema: "qp_operations",
                columns: table => new
                {
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Signal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companion_presence", x => x.ConnectionId);
                    table.CheckConstraint("bounded_companion_presence", "\"ObservationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Signal\" IN ('POLLING','WORKING','STOPPED') AND \"ExpiresAt\" >= \"ObservedAt\" AND \"ExpiresAt\" <= \"ObservedAt\" + interval '20 seconds'");
                    table.ForeignKey(
                        name: "FK_companion_presence_connections_ConnectionId_OwnerId",
                        columns: x => new { x.ConnectionId, x.OwnerId },
                        principalSchema: "qp_operations",
                        principalTable: "connections",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_companion_presence_ConnectionId_OwnerId",
                schema: "qp_operations",
                table: "companion_presence",
                columns: new[] { "ConnectionId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_companion_presence_OwnerId_ConnectionId",
                schema: "qp_operations",
                table: "companion_presence",
                columns: new[] { "OwnerId", "ConnectionId" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.companion_presence ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.companion_presence FORCE ROW LEVEL SECURITY;
                CREATE POLICY companion_presence_actor ON qp_operations.companion_presence TO quickprompt_app
                    USING ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid)
                    WITH CHECK ("OwnerId" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid
                        AND EXISTS (SELECT 1 FROM qp_operations.connections c WHERE c."Id"="ConnectionId" AND c."OwnerId"=companion_presence."OwnerId"
                            AND c."ProfileId" IN ('ollama-qwen3-8b','ollama-qwen3-8b-proposals') AND c."LibraryId" IS NULL));
                CREATE POLICY companion_presence_maintenance ON qp_operations.companion_presence TO quickprompt_migrator USING (true) WITH CHECK (true);
                REVOKE ALL ON qp_operations.companion_presence FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT SELECT,INSERT ON qp_operations.companion_presence TO quickprompt_app;
                GRANT UPDATE ("ObservationId","Signal","ObservedAt","ExpiresAt") ON qp_operations.companion_presence TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Do not remove the companion-presence admission boundary during a destructive rollback. Use the reviewed restore procedure.");
        }
    }
}
