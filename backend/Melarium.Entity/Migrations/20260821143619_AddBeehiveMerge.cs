using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddBeehiveMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MergedAt",
                table: "Beehives",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MergedIntoBeehiveId",
                table: "Beehives",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeehiveMerges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceBeehiveId = table.Column<int>(type: "integer", nullable: false),
                    TargetBeehiveId = table.Column<int>(type: "integer", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    QueenOutcome = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UndoJournalJson = table.Column<string>(type: "text", nullable: true),
                    UndoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UndoneById = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeehiveMerges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeehiveMerges_Beehives_SourceBeehiveId",
                        column: x => x.SourceBeehiveId,
                        principalTable: "Beehives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BeehiveMerges_Beehives_TargetBeehiveId",
                        column: x => x.TargetBeehiveId,
                        principalTable: "Beehives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BeehiveMerges_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BeehiveMerges_Users_UndoneById",
                        column: x => x.UndoneById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MergedAt", "MergedIntoBeehiveId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MergedAt", "MergedIntoBeehiveId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "MergedAt", "MergedIntoBeehiveId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Beehives",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "MergedAt", "MergedIntoBeehiveId" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Beehives_MergedIntoBeehiveId",
                table: "Beehives",
                column: "MergedIntoBeehiveId");

            migrationBuilder.CreateIndex(
                name: "IX_BeehiveMerges_CreatedById",
                table: "BeehiveMerges",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BeehiveMerges_MergedAt",
                table: "BeehiveMerges",
                column: "MergedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BeehiveMerges_SourceBeehiveId",
                table: "BeehiveMerges",
                column: "SourceBeehiveId",
                unique: true,
                filter: "\"UndoneAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BeehiveMerges_TargetBeehiveId",
                table: "BeehiveMerges",
                column: "TargetBeehiveId");

            migrationBuilder.CreateIndex(
                name: "IX_BeehiveMerges_UndoneById",
                table: "BeehiveMerges",
                column: "UndoneById");

            migrationBuilder.AddForeignKey(
                name: "FK_Beehives_Beehives_MergedIntoBeehiveId",
                table: "Beehives",
                column: "MergedIntoBeehiveId",
                principalTable: "Beehives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Beehives_Beehives_MergedIntoBeehiveId",
                table: "Beehives");

            migrationBuilder.DropTable(
                name: "BeehiveMerges");

            migrationBuilder.DropIndex(
                name: "IX_Beehives_MergedIntoBeehiveId",
                table: "Beehives");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                table: "Beehives");

            migrationBuilder.DropColumn(
                name: "MergedIntoBeehiveId",
                table: "Beehives");
        }
    }
}
