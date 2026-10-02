using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddProductsToHarvests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "HoneyType",
                table: "Harvests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ApiaryId",
                table: "Harvests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "BulkKg",
                table: "Harvests",
                type: "numeric(12,6)",
                nullable: true);

            // SPEC-30 — approved exceptions to ignore.md (existing columns change) and to "no raw SQL"
            // (one backfill below). OrganizationId is added nullable, filled from each harvest's
            // apiary, and only then made required: added straight as NOT NULL it would default to 0,
            // which is no organization, and the foreign key below would refuse every existing row.
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Harvests",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Harvests" AS h
                SET "OrganizationId" = a."OrganizationId"
                FROM "Apiaries" AS a
                WHERE a."Id" = h."ApiaryId";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Harvests",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "Harvests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityKg",
                table: "HarvestEntries",
                type: "numeric(12,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)");

            migrationBuilder.CreateIndex(
                name: "IX_Harvests_OrganizationId",
                table: "Harvests",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Harvests_Organizations_OrganizationId",
                table: "Harvests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema can only hold honey split per hive. Records of the whole organization,
            // other products and honey kept as one figure have no place in it and go first —
            // otherwise restoring NOT NULL below fails. Down is for development rollbacks only.
            migrationBuilder.Sql(
                """
                DELETE FROM "Harvests"
                WHERE "ApiaryId" IS NULL
                   OR "ProductType" <> 1
                   OR "HoneyType" IS NULL
                   OR "BulkKg" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Harvests_Organizations_OrganizationId",
                table: "Harvests");

            migrationBuilder.DropIndex(
                name: "IX_Harvests_OrganizationId",
                table: "Harvests");

            migrationBuilder.DropColumn(
                name: "BulkKg",
                table: "Harvests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Harvests");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Harvests");

            migrationBuilder.AlterColumn<int>(
                name: "HoneyType",
                table: "Harvests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ApiaryId",
                table: "Harvests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityKg",
                table: "HarvestEntries",
                type: "numeric(6,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,6)");
        }
    }
}
