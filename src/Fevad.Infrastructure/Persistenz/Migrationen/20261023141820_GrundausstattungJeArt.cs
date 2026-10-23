using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class GrundausstattungJeArt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grundausstattung_art",
                table: "artikel",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Bisher hing die Grundausstattung an einer Abteilung; jetzt gilt sie für alle Abteilungen derselben Art.
            migrationBuilder.Sql("""
                UPDATE artikel a SET grundausstattung_art = ab.art
                FROM abteilungen ab WHERE ab.id = a.grundausstattung_abteilung_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_artikel_abteilungen_grundausstattung_abteilung_id",
                table: "artikel");

            migrationBuilder.DropIndex(
                name: "ix_artikel_grundausstattung_abteilung_id",
                table: "artikel");

            migrationBuilder.DropColumn(
                name: "grundausstattung_abteilung_id",
                table: "artikel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "grundausstattung_abteilung_id",
                table: "artikel",
                type: "uuid",
                nullable: true);

            // Zurück auf die erste aktive Abteilung der Art.
            migrationBuilder.Sql("""
                UPDATE artikel a SET grundausstattung_abteilung_id = (
                    SELECT ab.id FROM abteilungen ab
                    WHERE ab.art = a.grundausstattung_art AND ab.geloescht_am IS NULL
                    ORDER BY ab.aktiv DESC, ab.sortierung LIMIT 1)
                WHERE a.grundausstattung_art IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "grundausstattung_art",
                table: "artikel");

            migrationBuilder.CreateIndex(
                name: "ix_artikel_grundausstattung_abteilung_id",
                table: "artikel",
                column: "grundausstattung_abteilung_id");

            migrationBuilder.AddForeignKey(
                name: "fk_artikel_abteilungen_grundausstattung_abteilung_id",
                table: "artikel",
                column: "grundausstattung_abteilung_id",
                principalTable: "abteilungen",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
