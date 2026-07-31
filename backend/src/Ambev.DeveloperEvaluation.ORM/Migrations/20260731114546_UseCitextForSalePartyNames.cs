using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambev.DeveloperEvaluation.ORM.Migrations
{
    /// <inheritdoc />
    public partial class UseCitextForSalePartyNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "Sales",
                type: "citext",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Sales",
                type: "citext",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchName",
                table: "Sales",
                column: "BranchName");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerName",
                table: "Sales",
                column: "CustomerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_BranchName",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerName",
                table: "Sales");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "Sales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Sales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 200);
        }
    }
}
