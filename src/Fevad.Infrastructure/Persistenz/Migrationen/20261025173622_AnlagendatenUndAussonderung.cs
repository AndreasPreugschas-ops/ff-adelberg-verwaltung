using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AnlagendatenUndAussonderung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "sperre_bis_pruefung",
                table: "maengel",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pruefer",
                table: "inventuren",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zaehler",
                table: "inventuren",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "anlagennummer",
                table: "geraete",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aussonderungsbeschluss",
                table: "geraete",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eigentuemer",
                table: "geraete",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Gemeinde");

            migrationBuilder.AddColumn<decimal>(
                name: "erloes",
                table: "geraete",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foerdergeber",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "foerdergeber_mitgeteilt_am",
                table: "geraete",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "foerdermittel",
                table: "geraete",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nutzungsdauer_jahre",
                table: "geraete",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verwertung",
                table: "geraete",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "zweckbindung_bis",
                table: "geraete",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "anlagennummer",
                table: "fahrzeuge",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "anschaffungswert",
                table: "fahrzeuge",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aussonderungsbeschluss",
                table: "fahrzeuge",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aussonderungsgrund",
                table: "fahrzeuge",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eigentuemer",
                table: "fahrzeuge",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Gemeinde");

            migrationBuilder.AddColumn<decimal>(
                name: "erloes",
                table: "fahrzeuge",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foerdergeber",
                table: "fahrzeuge",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "foerdergeber_mitgeteilt_am",
                table: "fahrzeuge",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "foerdermittel",
                table: "fahrzeuge",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nutzungsdauer_jahre",
                table: "fahrzeuge",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verwertung",
                table: "fahrzeuge",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "zweckbindung_bis",
                table: "fahrzeuge",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_geraete_anlagennummer",
                table: "geraete",
                column: "anlagennummer");

            migrationBuilder.CreateIndex(
                name: "ix_fahrzeuge_anlagennummer",
                table: "fahrzeuge",
                column: "anlagennummer");

            // Bestehende Einsatzsperren aus V13 werden ebenfalls erst durch eine Prüfung aufgehoben.
            migrationBuilder.Sql("UPDATE maengel SET sperre_bis_pruefung = true WHERE einsatz_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_geraete_anlagennummer",
                table: "geraete");

            migrationBuilder.DropIndex(
                name: "ix_fahrzeuge_anlagennummer",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "sperre_bis_pruefung",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "pruefer",
                table: "inventuren");

            migrationBuilder.DropColumn(
                name: "zaehler",
                table: "inventuren");

            migrationBuilder.DropColumn(
                name: "anlagennummer",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "aussonderungsbeschluss",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "eigentuemer",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "erloes",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "foerdergeber",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "foerdergeber_mitgeteilt_am",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "foerdermittel",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "nutzungsdauer_jahre",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "verwertung",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "zweckbindung_bis",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "anlagennummer",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "anschaffungswert",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "aussonderungsbeschluss",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "aussonderungsgrund",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "eigentuemer",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "erloes",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "foerdergeber",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "foerdergeber_mitgeteilt_am",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "foerdermittel",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "nutzungsdauer_jahre",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "verwertung",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "zweckbindung_bis",
                table: "fahrzeuge");
        }
    }
}
