using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LatinCatalogSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION qp_library.search_text_latin(value text) RETURNS text
                LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE SET search_path=pg_catalog,pg_temp AS $fold$
                  SELECT upper(regexp_replace(normalize(value,NFD), U&'[\0300-\036f]', '', 'g'));
                $fold$;
                REVOKE ALL ON FUNCTION qp_library.search_text_latin(text) FROM PUBLIC,quickprompt_app,quickprompt_execution;
                GRANT EXECUTE ON FUNCTION qp_library.search_text_latin(text) TO quickprompt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Catalog search requires its matching application and verified backup.");
    }
}
