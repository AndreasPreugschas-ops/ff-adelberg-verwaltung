using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class FunktionWahlUndBeschluss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "beschluss_durch",
                table: "funktionen",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mit_beschluss",
                table: "funktionen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "mit_wahl",
                table: "funktionen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "wahldatum",
                table: "funktion_zuordnungen",
                type: "date",
                nullable: true);

            // Besetzung nach Feuerwehrsatzung Adelberg: gewählt (mit Amtszeit), Kommandanten zusätzlich mit Zustimmung des
            // Gemeinderats, Leiter der Altersabteilung mit Zustimmung des Feuerwehrausschusses, sonst Bestellung durch den Kommandanten.
            migrationBuilder.Sql("""
                UPDATE funktionen SET mit_wahl = amtszeit_jahre IS NOT NULL OR code IN ('KAS', 'SF', 'KP');
                UPDATE funktionen SET mit_beschluss = NOT mit_wahl, beschluss_durch = CASE WHEN mit_wahl THEN NULL ELSE 'Feuerwehrkommandant' END;
                UPDATE funktionen SET mit_beschluss = TRUE, beschluss_durch = 'Gemeinderat' WHERE code IN ('KDT', 'STV', 'ABTKDT', 'ABTSTV');
                UPDATE funktionen SET mit_beschluss = TRUE, beschluss_durch = 'Feuerwehrausschuss' WHERE code = 'AAL';
                UPDATE funktion_zuordnungen SET wahldatum = beschlussdatum, beschlussdatum = NULL WHERE art = 'Wahl';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "beschluss_durch",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "mit_beschluss",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "mit_wahl",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "wahldatum",
                table: "funktion_zuordnungen");
        }
    }
}
