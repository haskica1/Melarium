using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedingCostAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DietId",
                table: "ExpenseItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseItems_DietId",
                table: "ExpenseItems",
                column: "DietId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseItems_Diets_DietId",
                table: "ExpenseItems",
                column: "DietId",
                principalTable: "Diets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseItems_Diets_DietId",
                table: "ExpenseItems");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseItems_DietId",
                table: "ExpenseItems");

            migrationBuilder.DropColumn(
                name: "DietId",
                table: "ExpenseItems");
        }
    }
}
