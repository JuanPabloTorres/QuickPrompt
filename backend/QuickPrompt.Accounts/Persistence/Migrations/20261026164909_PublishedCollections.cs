using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublishedCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PromptId",
                schema: "qp_library",
                table: "publications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionId",
                schema: "qp_library",
                table: "publications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionSnapshot",
                schema: "qp_library",
                table: "publications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCollection",
                schema: "qp_library",
                table: "invitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "collection_imports",
                schema: "qp_library",
                columns: table => new
                {
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PromptIds = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_imports", x => new { x.ActorId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_collection_imports_libraries_LibraryId_ActorId",
                        columns: x => new { x.LibraryId, x.ActorId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_imports_publications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "qp_library",
                        principalTable: "publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_imports_LibraryId_ActorId",
                schema: "qp_library",
                table: "collection_imports",
                columns: new[] { "LibraryId", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_imports_PublicationId",
                schema: "qp_library",
                table: "collection_imports",
                column: "PublicationId");
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.publications ADD CONSTRAINT publication_kind_valid CHECK ("SourceRevision">0 AND (
                  ("PromptId" IS NOT NULL AND "CollectionId" IS NULL AND "CollectionSnapshot" IS NULL)
                  OR ("PromptId" IS NULL AND "CollectionId" IS NOT NULL AND "Template"='' AND char_length("Title")<=80
                    AND "CollectionSnapshot" IS NOT NULL AND octet_length("CollectionSnapshot"::text)<=2097152
                    AND CASE WHEN jsonb_typeof("CollectionSnapshot")='array' THEN jsonb_array_length("CollectionSnapshot") BETWEEN 1 AND 200 ELSE false END)));
                ALTER TABLE qp_library.collection_imports ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.collection_imports FORCE ROW LEVEL SECURITY;
                REVOKE UPDATE,DELETE ON qp_library.collection_imports FROM quickprompt_app;
                CREATE POLICY collection_import_read ON qp_library.collection_imports FOR SELECT TO quickprompt_app USING ("ActorId"=qp_library.current_actor());
                CREATE POLICY collection_import_create ON qp_library.collection_imports FOR INSERT TO quickprompt_app WITH CHECK (
                  "ActorId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor())
                  AND EXISTS (SELECT 1 FROM qp_library.publications p WHERE p."Id"="PublicationId" AND p."RevokedAt" IS NULL AND p."CollectionId" IS NOT NULL));
                ALTER TABLE qp_library.collection_imports ADD CONSTRAINT bounded_collection_import CHECK (
                  cardinality("PromptIds") BETWEEN 1 AND 200 AND "Revision"=cardinality("PromptIds")+1 AND array_position("PromptIds",NULL) IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Published collections and private receipts cannot be dropped. Restore a verified backup with its matching application.");
        }
    }
}
