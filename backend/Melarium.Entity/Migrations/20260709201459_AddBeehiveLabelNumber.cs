using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddBeehiveLabelNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabelNumber",
                table: "Beehives",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 1,
                column: "LabelNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 2,
                column: "LabelNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 3,
                column: "LabelNumber",
                value: null);

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 4,
                column: "LabelNumber",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelNumber",
                table: "Beehives");
        }
    }
}
