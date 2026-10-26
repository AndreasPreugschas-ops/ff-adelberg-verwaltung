using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    /// <summary>Einsatzbericht: eingesetzte Mittel, Schadenart, Tiere und Gefahrstoff-Block (V15).</summary>
    public partial class EinsatzberichtLageGefahrstoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "behoerdenmeldung",
                table: "einsaetze",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "dekontamination",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "eingesetzte_mittel",
                table: "einsaetze",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "freisetzung_boden",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "freisetzung_luft",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "freisetzung_wasser",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "gefahrstoff",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "gefahrstoff_bezeichnung",
                table: "einsaetze",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gefahrstoff_menge",
                table: "einsaetze",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "schadenart",
                table: "einsaetze",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tiere_gerettet",
                table: "einsaetze",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tiere_tot",
                table: "einsaetze",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "un_nummer",
                table: "einsaetze",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "behoerdenmeldung",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "dekontamination",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "eingesetzte_mittel",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "freisetzung_boden",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "freisetzung_luft",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "freisetzung_wasser",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "gefahrstoff",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "gefahrstoff_bezeichnung",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "gefahrstoff_menge",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "schadenart",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "tiere_gerettet",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "tiere_tot",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "un_nummer",
                table: "einsaetze");
        }
    }
}
