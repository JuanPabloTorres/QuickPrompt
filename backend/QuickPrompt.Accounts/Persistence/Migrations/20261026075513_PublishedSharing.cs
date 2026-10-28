using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublishedSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_libraries_Id_OwnerId",
                schema: "qp_library",
                table: "libraries",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "publications",
                schema: "qp_library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Template = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publications", x => x.Id);
                    table.UniqueConstraint("AK_publications_Id_OwnerId", x => new { x.Id, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_publications_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_publications_prompt_versions_LibraryId_PromptId_SourceRevis~",
                        columns: x => new { x.LibraryId, x.PromptId, x.SourceRevision },
                        principalSchema: "qp_library",
                        principalTable: "prompt_versions",
                        principalColumns: new[] { "LibraryId", "PromptId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                schema: "qp_library",
                columns: table => new
                {
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imports", x => new { x.ActorId, x.OperationId });
                    table.CheckConstraint("import_initial_version", "\"Revision\" = 1");
                    table.ForeignKey(
                        name: "FK_imports_libraries_LibraryId_ActorId",
                        columns: x => new { x.LibraryId, x.ActorId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imports_prompt_versions_LibraryId_PromptId_Revision",
                        columns: x => new { x.LibraryId, x.PromptId, x.Revision },
                        principalSchema: "qp_library",
                        principalTable: "prompt_versions",
                        principalColumns: new[] { "LibraryId", "PromptId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imports_publications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "qp_library",
                        principalTable: "publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "qp_library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublisherId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                    table.CheckConstraint("invitation_not_self", "\"PublisherId\" <> \"RecipientId\"");
                    table.CheckConstraint("invitation_state_valid", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_invitations_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "qp_identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invitations_publications_PublicationId_PublisherId",
                        columns: x => new { x.PublicationId, x.PublisherId },
                        principalSchema: "qp_library",
                        principalTable: "publications",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imports_LibraryId_ActorId",
                schema: "qp_library",
                table: "imports",
                columns: new[] { "LibraryId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_LibraryId_PromptId_Revision",
                schema: "qp_library",
                table: "imports",
                columns: new[] { "LibraryId", "PromptId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_PublicationId",
                schema: "qp_library",
                table: "imports",
                column: "PublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_PublicationId_PublisherId",
                schema: "qp_library",
                table: "invitations",
                columns: new[] { "PublicationId", "PublisherId" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_PublicationId_RecipientId",
                schema: "qp_library",
                table: "invitations",
                columns: new[] { "PublicationId", "RecipientId" },
                unique: true,
                filter: "\"State\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_RecipientId_UpdatedAt",
                schema: "qp_library",
                table: "invitations",
                columns: new[] { "RecipientId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_publications_LibraryId_OwnerId",
                schema: "qp_library",
                table: "publications",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_publications_LibraryId_PromptId_SourceRevision",
                schema: "qp_library",
                table: "publications",
                columns: new[] { "LibraryId", "PromptId", "SourceRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_publications_OwnerId_CreatedAt",
                schema: "qp_library",
                table: "publications",
                columns: new[] { "OwnerId", "CreatedAt" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.publications ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.publications FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.invitations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.invitations FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.imports ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.imports FORCE ROW LEVEL SECURITY;

                CREATE POLICY publication_read ON qp_library.publications FOR SELECT USING (
                  "OwnerId" = qp_library.current_actor() OR ("RevokedAt" IS NULL AND EXISTS (
                    SELECT 1 FROM qp_library.invitations i WHERE i."PublicationId" = publications."Id"
                      AND i."RecipientId" = qp_library.current_actor() AND i."State" = 1)));
                CREATE POLICY publication_create ON qp_library.publications FOR INSERT WITH CHECK (
                  "OwnerId" = qp_library.current_actor() AND "RevokedAt" IS NULL AND EXISTS (
                    SELECT 1 FROM qp_library.libraries l WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor()));
                CREATE POLICY publication_revoke ON qp_library.publications FOR UPDATE
                  USING ("OwnerId" = qp_library.current_actor() AND "RevokedAt" IS NULL)
                  WITH CHECK ("OwnerId" = qp_library.current_actor() AND "RevokedAt" IS NOT NULL);
                REVOKE UPDATE, DELETE ON qp_library.publications FROM quickprompt_app;
                GRANT UPDATE ("RevokedAt") ON qp_library.publications TO quickprompt_app;

                -- No lookup of publications in the SELECT policy: avoid a recursive
                -- publication/invitation policy graph. Publisher identity is a composite FK.
                CREATE POLICY invitation_read ON qp_library.invitations FOR SELECT USING (
                  "PublisherId" = qp_library.current_actor() OR "RecipientId" = qp_library.current_actor());
                CREATE POLICY invitation_create ON qp_library.invitations FOR INSERT WITH CHECK (
                  "PublisherId" = qp_library.current_actor() AND "State" = 0 AND EXISTS (
                    SELECT 1 FROM qp_library.publications p WHERE p."Id" = "PublicationId"
                      AND p."OwnerId" = qp_library.current_actor() AND p."RevokedAt" IS NULL));
                CREATE POLICY invitation_recipient_decide ON qp_library.invitations FOR UPDATE
                  USING ("RecipientId" = qp_library.current_actor() AND "State" = 0)
                  WITH CHECK ("RecipientId" = qp_library.current_actor() AND "State" IN (1, 2));
                CREATE POLICY invitation_publisher_revoke ON qp_library.invitations FOR UPDATE
                  USING ("PublisherId" = qp_library.current_actor() AND "State" IN (0, 1))
                  WITH CHECK ("PublisherId" = qp_library.current_actor() AND "State" = 3);
                REVOKE UPDATE, DELETE ON qp_library.invitations FROM quickprompt_app;
                GRANT UPDATE ("State", "UpdatedAt") ON qp_library.invitations TO quickprompt_app;

                CREATE POLICY import_read ON qp_library.imports FOR SELECT USING ("ActorId" = qp_library.current_actor());
                CREATE POLICY import_create ON qp_library.imports FOR INSERT WITH CHECK (
                  "ActorId" = qp_library.current_actor() AND EXISTS (
                    SELECT 1 FROM qp_library.libraries l WHERE l."Id" = "LibraryId" AND l."OwnerId" = qp_library.current_actor())
                    AND EXISTS (SELECT 1 FROM qp_library.publications p WHERE p."Id" = "PublicationId" AND p."RevokedAt" IS NULL));
                REVOKE UPDATE, DELETE ON qp_library.imports FROM quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Sharing snapshots and receipts cannot be dropped by rollback. Restore the verified backup and matching application instead.");
        }
    }
}
