using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseApiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApiaryId",
                table: "Expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ApiaryId",
                table: "Expenses",
                column: "ApiaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Apiaries_ApiaryId",
                table: "Expenses",
                column: "ApiaryId",
                principalTable: "Apiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Apiaries_ApiaryId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ApiaryId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ApiaryId",
                table: "Expenses");
        }
    }
}
