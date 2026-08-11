using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistantSessionBeehive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BeehiveId",
                table: "AiAssistantSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantSessions_BeehiveId",
                table: "AiAssistantSessions",
                column: "BeehiveId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiAssistantSessions_Beehives_BeehiveId",
                table: "AiAssistantSessions",
                column: "BeehiveId",
                principalTable: "Beehives",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiAssistantSessions_Beehives_BeehiveId",
                table: "AiAssistantSessions");

            migrationBuilder.DropIndex(
                name: "IX_AiAssistantSessions_BeehiveId",
                table: "AiAssistantSessions");

            migrationBuilder.DropColumn(
                name: "BeehiveId",
                table: "AiAssistantSessions");
        }
    }
}
