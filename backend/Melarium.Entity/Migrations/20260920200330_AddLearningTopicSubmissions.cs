using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningTopicSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthorId",
                table: "LearningTopics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "LearningTopics",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "LearningTopics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "LearningTopics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedById",
                table: "LearningTopics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "LearningTopics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningTopics_AuthorId",
                table: "LearningTopics",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTopics_ReviewedById",
                table: "LearningTopics",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTopics_ReviewStatus",
                table: "LearningTopics",
                column: "ReviewStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningTopics_Users_AuthorId",
                table: "LearningTopics",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningTopics_Users_ReviewedById",
                table: "LearningTopics",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningTopics_Users_AuthorId",
                table: "LearningTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_LearningTopics_Users_ReviewedById",
                table: "LearningTopics");

            migrationBuilder.DropIndex(
                name: "IX_LearningTopics_AuthorId",
                table: "LearningTopics");

            migrationBuilder.DropIndex(
                name: "IX_LearningTopics_ReviewedById",
                table: "LearningTopics");

            migrationBuilder.DropIndex(
                name: "IX_LearningTopics_ReviewStatus",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "LearningTopics");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "LearningTopics");
        }
    }
}
