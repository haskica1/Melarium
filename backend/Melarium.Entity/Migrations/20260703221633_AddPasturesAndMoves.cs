using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddPasturesAndMoves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentPastureId",
                table: "Apiaries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pastures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FloraNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pastures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pastures_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiaryMoves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiaryId = table.Column<int>(type: "integer", nullable: false),
                    FromPastureId = table.Column<int>(type: "integer", nullable: true),
                    ToPastureId = table.Column<int>(type: "integer", nullable: false),
                    MovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiaryMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiaryMoves_Apiaries_ApiaryId",
                        column: x => x.ApiaryId,
                        principalTable: "Apiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApiaryMoves_Pastures_FromPastureId",
                        column: x => x.FromPastureId,
                        principalTable: "Pastures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApiaryMoves_Pastures_ToPastureId",
                        column: x => x.ToPastureId,
                        principalTable: "Pastures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiaryMoves_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Apiaries",
                keyColumn: "Id",
                keyValue: 1,
                column: "CurrentPastureId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Apiaries",
                keyColumn: "Id",
                keyValue: 2,
                column: "CurrentPastureId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Apiaries_CurrentPastureId",
                table: "Apiaries",
                column: "CurrentPastureId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiaryMoves_ApiaryId",
                table: "ApiaryMoves",
                column: "ApiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiaryMoves_CreatedById",
                table: "ApiaryMoves",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApiaryMoves_FromPastureId",
                table: "ApiaryMoves",
                column: "FromPastureId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiaryMoves_MovedAt",
                table: "ApiaryMoves",
                column: "MovedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiaryMoves_ToPastureId",
                table: "ApiaryMoves",
                column: "ToPastureId");

            migrationBuilder.CreateIndex(
                name: "IX_Pastures_OrganizationId",
                table: "Pastures",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Apiaries_Pastures_CurrentPastureId",
                table: "Apiaries",
                column: "CurrentPastureId",
                principalTable: "Pastures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apiaries_Pastures_CurrentPastureId",
                table: "Apiaries");

            migrationBuilder.DropTable(
                name: "ApiaryMoves");

            migrationBuilder.DropTable(
                name: "Pastures");

            migrationBuilder.DropIndex(
                name: "IX_Apiaries_CurrentPastureId",
                table: "Apiaries");

            migrationBuilder.DropColumn(
                name: "CurrentPastureId",
                table: "Apiaries");
        }
    }
}
