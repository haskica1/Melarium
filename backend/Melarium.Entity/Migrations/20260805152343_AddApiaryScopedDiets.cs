using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Melarium.Entity.Migrations
{
    /// <summary>
    /// SPEC-12 — moves feeding programmes from beehive scope to apiary scope over LIVE data.
    ///
    /// Hand-written on purpose. The scaffolded version turned <c>BeehiveId</c> into <c>ApiaryId</c>
    /// with a RENAME, which keeps every value: each diet would silently end up pointing at the apiary
    /// whose id happens to equal its old hive id. The order below adds the column empty, derives its
    /// value from the hive, and only then enforces the constraint and drops the old column.
    ///
    /// Raw SQL here is the sanctioned exception to the "EF Core LINQ only" rule in CLAUDE.md — that
    /// rule governs repositories and queries, not data migrations.
    ///
    /// There is no automatic rollback: see <see cref="Down"/>. Take a pg_dump before applying.
    /// </summary>
    public partial class AddApiaryScopedDiets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 0. Abort loudly rather than corrupt ──────────────────────────────────
            // A diet whose hive is gone has no apiary to inherit. Beehives → Diets is already
            // ON DELETE CASCADE so this should be impossible — but "should be impossible" and "is
            // impossible" are different things on live data, and the cost of being wrong is every
            // diet landing on apiary 0. The whole migration runs in one transaction, so RAISE
            // EXCEPTION rolls back everything below.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM ""Diets"" d
             LEFT JOIN ""Beehives"" b ON b.""Id"" = d.""BeehiveId""
             WHERE b.""Id"" IS NULL) THEN
    RAISE EXCEPTION 'SPEC-12: orphan diets found (Diets.BeehiveId with no matching Beehive) — aborting.';
  END IF;
END $$;");

            // ── 1. The new link table ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "DietBeehives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DietId = table.Column<int>(type: "integer", nullable: false),
                    BeehiveId = table.Column<int>(type: "integer", nullable: false),
                    RemovedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietBeehives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietBeehives_Beehives_BeehiveId",
                        column: x => x.BeehiveId,
                        principalTable: "Beehives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietBeehives_Diets_DietId",
                        column: x => x.DietId,
                        principalTable: "Diets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 2. New columns, nullable first ───────────────────────────────────────
            // ApiaryId must start NULL: a NOT NULL DEFAULT 0 would either fail the FK or point every
            // existing diet at apiary 0.
            migrationBuilder.AddColumn<int>(
                name: "ApiaryId",
                table: "Diets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPerHive",
                table: "Diets",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmountUnit",
                table: "Diets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmountNote",
                table: "Diets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "FeedingEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // ── 3. Every diet inherits its hive's apiary ─────────────────────────────
            migrationBuilder.Sql(@"
UPDATE ""Diets"" d
SET ""ApiaryId"" = b.""ApiaryId""
FROM ""Beehives"" b
WHERE b.""Id"" = d.""BeehiveId"";");

            // ── 4. Each existing diet keeps exactly its one hive ─────────────────────
            // Diets previously copied across N hives stay N separate single-hive programmes. They are
            // deliberately not merged: guessing which rows "were the same programme" would rewrite
            // someone's completion history.
            migrationBuilder.Sql(@"
INSERT INTO ""DietBeehives"" (""DietId"", ""BeehiveId"", ""RemovedOn"", ""CreatedAt"")
SELECT d.""Id"", d.""BeehiveId"", NULL, d.""CreatedAt""
FROM ""Diets"" d;");

            // ── 5. Only now enforce the constraint and drop the old column ───────────
            migrationBuilder.AlterColumn<int>(
                name: "ApiaryId",
                table: "Diets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Diets_Beehives_BeehiveId",
                table: "Diets");

            migrationBuilder.DropIndex(
                name: "IX_Diets_BeehiveId",
                table: "Diets");

            migrationBuilder.DropColumn(
                name: "BeehiveId",
                table: "Diets");

            migrationBuilder.CreateIndex(
                name: "IX_Diets_ApiaryId",
                table: "Diets",
                column: "ApiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Diets_StartDate",
                table: "Diets",
                column: "StartDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Diets_Apiaries_ApiaryId",
                table: "Diets",
                column: "ApiaryId",
                principalTable: "Apiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_DietBeehives_BeehiveId",
                table: "DietBeehives",
                column: "BeehiveId");

            migrationBuilder.CreateIndex(
                name: "IX_DietBeehives_DietId",
                table: "DietBeehives",
                column: "DietId");

            // Partial, not plain: a hive may be on a programme once *at a time*, while a removed hive
            // stays re-addable. A plain unique index would collide with the old row forever.
            migrationBuilder.CreateIndex(
                name: "IX_DietBeehives_DietId_BeehiveId",
                table: "DietBeehives",
                columns: new[] { "DietId", "BeehiveId" },
                unique: true,
                filter: "\"RemovedOn\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately irreversible. BeehiveId could be restored only for programmes with exactly
            // one active hive; for a multi-hive programme there is no correct answer, and silently
            // picking one would discard the others along with their feeding history.
            // The rollback is the pg_dump taken before deploying — that is the whole plan, not a
            // fallback. Failing loudly here is better than a Down() that quietly loses data.
            throw new NotSupportedException(
                "SPEC-12: AddApiaryScopedDiets cannot be reverted automatically — a multi-hive feeding " +
                "programme has no single BeehiveId to restore. Restore the pre-deploy pg_dump instead.");
        }
    }
}
