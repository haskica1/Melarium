using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FeedToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FeedEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncFeedings = table.Column<bool>(type: "boolean", nullable: false),
                    SyncTodos = table.Column<bool>(type: "boolean", nullable: false),
                    SyncTreatments = table.Column<bool>(type: "boolean", nullable: false),
                    SyncInspections = table.Column<bool>(type: "boolean", nullable: false),
                    DailyAgendaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSettings_FeedToken",
                table: "CalendarSettings",
                column: "FeedToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSettings_UserId",
                table: "CalendarSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarSettings");
        }
    }
}
