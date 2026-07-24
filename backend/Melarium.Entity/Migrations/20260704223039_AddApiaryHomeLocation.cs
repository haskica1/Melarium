using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddApiaryHomeLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ToPastureId",
                table: "ApiaryMoves",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<double>(
                name: "HomeLatitude",
                table: "Apiaries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLongitude",
                table: "Apiaries",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Apiaries",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "HomeLatitude", "HomeLongitude" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apiaries",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "HomeLatitude", "HomeLongitude" },
                values: new object[] { null, null });

            // Backfill: an apiary currently at its matična lokacija (never moved, or reverted back)
            // has its current coordinates AS its home coordinates — capture them. Apiaries currently
            // away on a pasture keep Home = null; their true original location was never stored
            // separately before this migration and must be set manually (documented trade-off).
            migrationBuilder.Sql(
                """
                UPDATE "Apiaries"
                SET "HomeLatitude" = "Latitude", "HomeLongitude" = "Longitude"
                WHERE "CurrentPastureId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeLatitude",
                table: "Apiaries");

            migrationBuilder.DropColumn(
                name: "HomeLongitude",
                table: "Apiaries");

            migrationBuilder.AlterColumn<int>(
                name: "ToPastureId",
                table: "ApiaryMoves",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
