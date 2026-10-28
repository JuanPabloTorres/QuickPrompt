using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collections",
                schema: "qp_library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.Id);
                    table.UniqueConstraint("AK_collections_LibraryId_Id_OwnerId", x => new { x.LibraryId, x.Id, x.OwnerId });
                    table.CheckConstraint("collection_revision_positive", "\"Revision\">0");
                    table.ForeignKey(
                        name: "FK_collections_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_members",
                schema: "qp_library",
                columns: table => new
                {
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_members", x => new { x.LibraryId, x.CollectionId, x.PromptId });
                    table.ForeignKey(
                        name: "FK_collection_members_collections_LibraryId_CollectionId_Owner~",
                        columns: x => new { x.LibraryId, x.CollectionId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "collections",
                        principalColumns: new[] { "LibraryId", "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collection_members_prompts_LibraryId_PromptId",
                        columns: x => new { x.LibraryId, x.PromptId },
                        principalSchema: "qp_library",
                        principalTable: "prompts",
                        principalColumns: new[] { "LibraryId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_members_LibraryId_CollectionId_OwnerId",
                schema: "qp_library",
                table: "collection_members",
                columns: new[] { "LibraryId", "CollectionId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_members_LibraryId_PromptId",
                schema: "qp_library",
                table: "collection_members",
                columns: new[] { "LibraryId", "PromptId" });

            migrationBuilder.CreateIndex(
                name: "IX_collections_LibraryId_Name",
                schema: "qp_library",
                table: "collections",
                columns: new[] { "LibraryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_collections_LibraryId_OwnerId",
                schema: "qp_library",
                table: "collections",
                columns: new[] { "LibraryId", "OwnerId" });
            migrationBuilder.Sql("""
                ALTER TABLE qp_library.collections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.collections FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.collection_members ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.collection_members FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON qp_library.collections,qp_library.collection_members FROM quickprompt_app;
                GRANT SELECT,INSERT,DELETE ON qp_library.collections,qp_library.collection_members TO quickprompt_app;
                GRANT UPDATE ("Name","Revision","UpdatedAt") ON qp_library.collections TO quickprompt_app;
                CREATE POLICY owned_collection ON qp_library.collections TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()))
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()));
                CREATE POLICY owned_collection_member ON qp_library.collection_members TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.collections g WHERE g."Id"="CollectionId" AND g."LibraryId"=collection_members."LibraryId" AND g."OwnerId"=qp_library.current_actor()))
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS (SELECT 1 FROM qp_library.collections g WHERE g."Id"="CollectionId" AND g."LibraryId"=collection_members."LibraryId" AND g."OwnerId"=qp_library.current_actor()));
                ALTER TABLE qp_library.collections ADD CONSTRAINT collection_name_valid CHECK (char_length("Name") BETWEEN 1 AND 80 AND "Name"=btrim("Name"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Private collections cannot be dropped by rollback. Restore a verified backup with its matching application.");
        }
    }
}
