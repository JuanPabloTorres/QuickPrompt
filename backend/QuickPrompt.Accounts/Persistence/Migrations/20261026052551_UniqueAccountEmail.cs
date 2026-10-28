using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPrompt.Accounts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAccountEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "qp_identity",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "qp_identity",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "qp_identity",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "qp_identity",
                table: "AspNetUsers",
                column: "NormalizedEmail");
        }
    }
}
