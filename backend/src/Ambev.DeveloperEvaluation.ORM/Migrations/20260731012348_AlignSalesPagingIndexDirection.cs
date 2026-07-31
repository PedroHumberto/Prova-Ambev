using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambev.DeveloperEvaluation.ORM.Migrations
{
    /// <inheritdoc />
    public partial class AlignSalesPagingIndexDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_SaleDate_Id",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate_Id",
                table: "Sales",
                columns: new[] { "SaleDate", "Id" },
                descending: new[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_SaleDate_Id",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate_Id",
                table: "Sales",
                columns: new[] { "SaleDate", "Id" },
                descending: new bool[0]);
        }
    }
}
