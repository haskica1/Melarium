using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PlanNotes",
                table: "Organizations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanValidUntil",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Plan", "PlanNotes", "PlanValidUntil" },
                values: new object[] { 1, null, null });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Plan", "PlanNotes", "PlanValidUntil" },
                values: new object[] { 1, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PlanNotes",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PlanValidUntil",
                table: "Organizations");
        }
    }
}
