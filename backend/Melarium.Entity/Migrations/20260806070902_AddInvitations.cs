using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InviterUserId = table.Column<int>(type: "integer", nullable: true),
                    InviterOrganizationId = table.Column<int>(type: "integer", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmailCanonical = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PersonalMessage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AcceptedOrganizationId = table.Column<int>(type: "integer", nullable: true),
                    RewardDays = table.Column<int>(type: "integer", nullable: true),
                    RewardGrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_AcceptedOrganizationId",
                        column: x => x.AcceptedOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_InviterOrganizationId",
                        column: x => x.InviterOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true,
                filter: "\"ReferralCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedByUserId",
                table: "Invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedOrganizationId",
                table: "Invitations",
                column: "AcceptedOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_EmailCanonical_CreatedAt",
                table: "Invitations",
                columns: new[] { "EmailCanonical", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviterOrganizationId_RewardGrantedAt",
                table: "Invitations",
                columns: new[] { "InviterOrganizationId", "RewardGrantedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviterUserId_CreatedAt",
                table: "Invitations",
                columns: new[] { "InviterUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");
        }
    }
}
