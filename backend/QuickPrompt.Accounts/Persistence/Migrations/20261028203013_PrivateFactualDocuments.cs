using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateFactualDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "factual_documents",
                schema: "qp_library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptRevision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Text = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    Facts = table.Column<string>(type: "jsonb", nullable: false),
                    ProposalFacts = table.Column<string>(type: "jsonb", nullable: false),
                    Edited = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceFields = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factual_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factual_documents_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factual_documents_prompts_LibraryId_PromptId",
                        columns: x => new { x.LibraryId, x.PromptId },
                        principalSchema: "qp_library",
                        principalTable: "prompts",
                        principalColumns: new[] { "LibraryId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "factual_reviews",
                schema: "qp_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptRevision = table.Column<int>(type: "integer", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "jsonb", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ProposalFacts = table.Column<string>(type: "jsonb", nullable: false),
                    ConfirmedFacts = table.Column<string>(type: "jsonb", nullable: false),
                    Preview = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    Edited = table.Column<bool>(type: "boolean", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factual_reviews", x => x.Id);
                    table.UniqueConstraint("AK_factual_reviews_Id_OwnerId_LibraryId", x => new { x.Id, x.OwnerId, x.LibraryId });
                    table.ForeignKey(
                        name: "FK_factual_reviews_connections_ConnectionId_OwnerId",
                        columns: x => new { x.ConnectionId, x.OwnerId },
                        principalSchema: "qp_operations",
                        principalTable: "connections",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factual_reviews_libraries_LibraryId_OwnerId",
                        columns: x => new { x.LibraryId, x.OwnerId },
                        principalSchema: "qp_library",
                        principalTable: "libraries",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_factual_reviews_prompts_LibraryId_PromptId",
                        columns: x => new { x.LibraryId, x.PromptId },
                        principalSchema: "qp_library",
                        principalTable: "prompts",
                        principalColumns: new[] { "LibraryId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_factual_documents_LibraryId_OwnerId",
                schema: "qp_library",
                table: "factual_documents",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_documents_LibraryId_PromptId",
                schema: "qp_library",
                table: "factual_documents",
                columns: new[] { "LibraryId", "PromptId" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_documents_OwnerId_LibraryId_CreatedAt_Id",
                schema: "qp_library",
                table: "factual_documents",
                columns: new[] { "OwnerId", "LibraryId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_documents_OwnerId_ReviewId",
                schema: "qp_library",
                table: "factual_documents",
                columns: new[] { "OwnerId", "ReviewId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_ConnectionId_OwnerId",
                schema: "qp_operations",
                table: "factual_reviews",
                columns: new[] { "ConnectionId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_ExpiresAt",
                schema: "qp_operations",
                table: "factual_reviews",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_LibraryId_OwnerId",
                schema: "qp_operations",
                table: "factual_reviews",
                columns: new[] { "LibraryId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_LibraryId_PromptId",
                schema: "qp_operations",
                table: "factual_reviews",
                columns: new[] { "LibraryId", "PromptId" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_OwnerId_LibraryId_CreatedAt",
                schema: "qp_operations",
                table: "factual_reviews",
                columns: new[] { "OwnerId", "LibraryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_factual_reviews_OwnerId_OperationId",
                schema: "qp_operations",
                table: "factual_reviews",
                columns: new[] { "OwnerId", "OperationId" },
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE qp_operations.factual_reviews ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.factual_reviews FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.factual_documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qp_library.factual_documents FORCE ROW LEVEL SECURITY;
                ALTER TABLE qp_operations.factual_reviews ADD CONSTRAINT factual_child_owner FOREIGN KEY ("ChildExecutionId","OwnerId","LibraryId")
                  REFERENCES qp_operations.executions("Id","OwnerId","LibraryId") ON DELETE RESTRICT;
                ALTER TABLE qp_library.factual_documents ADD CONSTRAINT factual_document_review_owner FOREIGN KEY ("ReviewId","OwnerId","LibraryId")
                  REFERENCES qp_operations.factual_reviews("Id","OwnerId","LibraryId") ON DELETE RESTRICT;
                CREATE POLICY factual_review_actor ON qp_operations.factual_reviews TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS(SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()))
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS(SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()));
                CREATE POLICY factual_document_actor ON qp_library.factual_documents TO quickprompt_app
                  USING ("OwnerId"=qp_library.current_actor() AND EXISTS(SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()))
                  WITH CHECK ("OwnerId"=qp_library.current_actor() AND EXISTS(SELECT 1 FROM qp_library.libraries l WHERE l."Id"="LibraryId" AND l."OwnerId"=qp_library.current_actor()));
                CREATE POLICY factual_review_maintenance ON qp_operations.factual_reviews TO quickprompt_migrator USING (true) WITH CHECK (true);
                CREATE POLICY factual_document_maintenance ON qp_library.factual_documents TO quickprompt_migrator USING (true) WITH CHECK (true);
                GRANT SELECT,INSERT,UPDATE ON qp_operations.factual_reviews TO quickprompt_app;
                GRANT SELECT,INSERT,DELETE ON qp_library.factual_documents TO quickprompt_app;
                REVOKE ALL ON qp_operations.factual_reviews,qp_library.factual_documents FROM quickprompt_execution;
                ALTER TABLE qp_operations.factual_reviews ADD CONSTRAINT factual_review_shape CHECK (
                  "Version">0 AND "PromptRevision">0 AND "ExpiresAt">"CreatedAt"
                  AND "Status" IN ('ADMITTED','PROPOSING','REVIEW','COMPOSED','APPROVED','CANCELLED','INVALIDATED','EXPIRED','FAILED')
                  AND octet_length("Source"::text)<=128000 AND octet_length("ProposalFacts"::text)<=1048576 AND octet_length("ConfirmedFacts"::text)<=1048576);
                ALTER TABLE qp_library.factual_documents ADD CONSTRAINT factual_document_shape CHECK (
                  "PromptRevision">0 AND length("Text")>0 AND "Model"='qwen3:8b'
                  AND octet_length("Facts"::text)<=1048576 AND octet_length("ProposalFacts"::text)<=1048576 AND octet_length("SourceFields"::text)<=8000);

                CREATE FUNCTION qp_library.validate_factual_document() RETURNS trigger LANGUAGE plpgsql
                  SET search_path=pg_catalog,pg_temp AS $guard$
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM qp_operations.factual_reviews r WHERE r."Id"=NEW."ReviewId"
                    AND r."OwnerId"=NEW."OwnerId" AND r."LibraryId"=NEW."LibraryId" AND r."Status"='COMPOSED'
                    AND r."PromptId"=NEW."PromptId" AND r."PromptRevision"=NEW."PromptRevision"
                    AND r."Preview"=NEW."Text" AND r."Title"=NEW."Title" AND r."ConfirmedFacts"=NEW."Facts"
                    AND r."ProposalFacts"=NEW."ProposalFacts" AND r."Edited"=NEW."Edited" AND r."Source"->'sourceFields'=NEW."SourceFields" AND r."ExpiresAt">clock_timestamp())
                    THEN RAISE EXCEPTION 'Factual document requires its confirmed review' USING ERRCODE='42501'; END IF;
                  RETURN NEW;
                END $guard$;
                REVOKE ALL ON FUNCTION qp_library.validate_factual_document() FROM PUBLIC;
                CREATE TRIGGER factual_document_from_review BEFORE INSERT ON qp_library.factual_documents FOR EACH ROW EXECUTE FUNCTION qp_library.validate_factual_document();

                CREATE FUNCTION qp_operations.expire_factual_reviews() RETURNS integer LANGUAGE plpgsql SECURITY DEFINER
                  SET search_path=pg_catalog,pg_temp AS $expire$
                DECLARE target uuid; removed integer:=0; child uuid;
                BEGIN
                  PERFORM pg_advisory_xact_lock(5283,0);
                  FOR target IN SELECT "Id" FROM qp_operations.factual_reviews WHERE "ExpiresAt"<=clock_timestamp()
                    AND "Status" IN ('ADMITTED','PROPOSING','REVIEW','COMPOSED') ORDER BY "ExpiresAt","Id" LIMIT 100
                  LOOP
                    SELECT COALESCE(r."ChildExecutionId",(SELECT e."Id" FROM qp_operations.executions e
                      WHERE e."OwnerId"=r."OwnerId" AND e."LibraryId"=r."LibraryId" AND e."OperationId"=r."ChildOperationId"
                        AND e."ConnectionId"=r."ConnectionId" AND e."Kind"='ModelProposal'))
                      INTO child FROM qp_operations.factual_reviews r WHERE r."Id"=target FOR UPDATE;
                    UPDATE qp_operations.factual_reviews SET "ChildExecutionId"=child WHERE "Id"=target;
                    UPDATE qp_operations.executions SET "CancelRequested"=true WHERE "Id"=child AND "Status" IN ('Queued','Running');
                    UPDATE qp_operations.factual_reviews SET "Status"='EXPIRED',"Source"='null'::jsonb,"Title"='',"ProposalFacts"='[]'::jsonb,
                      "ConfirmedFacts"='[]'::jsonb,"Preview"=NULL,"Version"="Version"+1 WHERE "Id"=target;
                    removed:=removed+1;
                  END LOOP;
                  RETURN removed;
                END $expire$;
                ALTER FUNCTION qp_operations.expire_factual_reviews() OWNER TO quickprompt_migrator;
                REVOKE ALL ON FUNCTION qp_operations.expire_factual_reviews() FROM PUBLIC,quickprompt_app;
                GRANT EXECUTE ON FUNCTION qp_operations.expire_factual_reviews() TO quickprompt_execution;
                CREATE POLICY factual_expiry_cancel ON qp_operations.executions FOR UPDATE TO quickprompt_migrator
                  USING ("Status" IN ('Queued','Running') AND EXISTS(SELECT 1 FROM qp_operations.factual_reviews r
                    WHERE r."ChildExecutionId"=qp_operations.executions."Id" AND r."ExpiresAt"<=clock_timestamp()))
                  WITH CHECK ("Status" IN ('Queued','Running') AND "CancelRequested");

                CREATE FUNCTION qp_operations.forget_factual_child(library uuid,review uuid) RETURNS integer
                  LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $forget$
                DECLARE actor uuid:=NULLIF(current_setting('quickprompt.actor_id',true),'')::uuid; child uuid;
                BEGIN
                  SELECT r."ChildExecutionId" INTO child FROM qp_operations.factual_reviews r
                    WHERE r."Id"=review AND r."LibraryId"=library AND r."OwnerId"=actor AND r."Status"='APPROVED'
                      AND EXISTS(SELECT 1 FROM qp_library.libraries l WHERE l."Id"=library AND l."OwnerId"=actor);
                  IF NOT FOUND OR child IS NULL THEN RETURN 0; END IF;
                  IF NOT EXISTS(SELECT 1 FROM qp_operations.executions e WHERE e."Id"=child AND e."OwnerId"=actor
                    AND e."LibraryId"=library AND e."Kind"='ModelProposal') THEN RETURN 0; END IF;
                  RETURN qp_operations.erase_execution_content(child);
                END $forget$;
                ALTER FUNCTION qp_operations.forget_factual_child(uuid,uuid) OWNER TO quickprompt_migrator;
                REVOKE ALL ON FUNCTION qp_operations.forget_factual_child(uuid,uuid) FROM PUBLIC,quickprompt_execution;
                GRANT EXECUTE ON FUNCTION qp_operations.forget_factual_child(uuid,uuid) TO quickprompt_app;

                CREATE FUNCTION qp_library.erase_factual_derivatives() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
                  SET search_path=pg_catalog,pg_temp AS $erase$
                DECLARE child uuid;
                BEGIN
                  IF NEW."DeletedAt" IS NULL OR OLD."DeletedAt" IS NOT NULL THEN RETURN NEW; END IF;
                  FOR child IN SELECT e."Id" FROM qp_operations.factual_reviews r JOIN qp_operations.executions e
                    ON e."OwnerId"=r."OwnerId" AND e."LibraryId"=r."LibraryId" AND e."ConnectionId"=r."ConnectionId" AND e."Kind"='ModelProposal'
                      AND (e."Id"=r."ChildExecutionId" OR e."OperationId"=r."ChildOperationId")
                    WHERE r."LibraryId"=NEW."LibraryId" AND r."PromptId"=NEW."Id"
                  LOOP
                    IF child IS NOT NULL THEN PERFORM qp_operations.erase_execution_content(child); END IF;
                  END LOOP;
                  DELETE FROM qp_library.factual_documents WHERE "LibraryId"=NEW."LibraryId" AND "PromptId"=NEW."Id";
                  UPDATE qp_operations.factual_reviews SET "Status"='INVALIDATED',"Source"='null'::jsonb,"Title"='',"ProposalFacts"='[]'::jsonb,
                    "ConfirmedFacts"='[]'::jsonb,"Preview"=NULL,"DocumentId"=NULL,"Version"="Version"+1
                    WHERE "LibraryId"=NEW."LibraryId" AND "PromptId"=NEW."Id";
                  RETURN NEW;
                END $erase$;
                ALTER FUNCTION qp_library.erase_factual_derivatives() OWNER TO quickprompt_migrator;
                REVOKE ALL ON FUNCTION qp_library.erase_factual_derivatives() FROM PUBLIC;
                CREATE TRIGGER erased_prompt_factual_content AFTER UPDATE OF "DeletedAt" ON qp_library.prompts FOR EACH ROW EXECUTE FUNCTION qp_library.erase_factual_derivatives();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Approved factual documents require a verified backup and matching application; no destructive schema rollback.");
        }
    }
}
