using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Kostenersatz-Vorgänge mit Schuldnern und Zusatzkosten (V21) und VOKeFw-Klasse am Fahrzeug.</summary>
    public partial class Kostenersatzvorgaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kostenklasse",
                table: "fahrzeuge",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Die Stammfahrzeuge Adelberg bekommen ihre Klasse; TLF 8/18 wie TLF 2000 (Andi, F1).
            migrationBuilder.Sql("""
                UPDATE fahrzeuge SET kostenklasse = 'HLF 10' WHERE funkrufname = 'Florian Adelberg 43' AND kostenklasse IS NULL;
                UPDATE fahrzeuge SET kostenklasse = 'TLF 2000' WHERE funkrufname = 'Florian Adelberg 21' AND kostenklasse IS NULL;
                UPDATE fahrzeuge SET kostenklasse = 'MTW' WHERE funkrufname = 'Florian Adelberg 19' AND kostenklasse IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "kostenersatzvorgaenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tatbestand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    sachverhalt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    gesamtschuld = table.Column<bool>(type: "boolean", nullable: false),
                    verwaltungsgebuehr = table.Column<bool>(type: "boolean", nullable: false),
                    empfehlung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    empfehlung_grund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    empfehlung_begruendung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    empfohlen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    empfohlen_von = table.Column<Guid>(type: "uuid", nullable: true),
                    empfohlen_von_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    entscheidung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entscheidung_grund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entscheidung_begruendung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entschieden_von = table.Column<Guid>(type: "uuid", nullable: true),
                    entschieden_von_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kostenersatzvorgaenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_kostenersatzvorgaenge_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kostenschuldner",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostenersatzvorgang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kennzeichen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    halterauskunft = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    versicherung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    versicherungsnummer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zustellweg = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    anteil_prozent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    einsatz_beteiligter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kostenschuldner", x => x.id);
                    table.ForeignKey(
                        name: "fk_kostenschuldner_kostenersatzvorgaenge_kostenersatzvorgang_id",
                        column: x => x.kostenersatzvorgang_id,
                        principalTable: "kostenersatzvorgaenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zusatzkosten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostenersatzvorgang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zusatzkosten", x => x.id);
                    table.ForeignKey(
                        name: "fk_zusatzkosten_kostenersatzvorgaenge_kostenersatzvorgang_id",
                        column: x => x.kostenersatzvorgang_id,
                        principalTable: "kostenersatzvorgaenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kostenersatzvorgaenge_einsatz_id",
                table: "kostenersatzvorgaenge",
                column: "einsatz_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kostenschuldner_kostenersatzvorgang_id",
                table: "kostenschuldner",
                column: "kostenersatzvorgang_id");

            migrationBuilder.CreateIndex(
                name: "ix_zusatzkosten_kostenersatzvorgang_id",
                table: "zusatzkosten",
                column: "kostenersatzvorgang_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kostenschuldner");

            migrationBuilder.DropTable(
                name: "zusatzkosten");

            migrationBuilder.DropTable(
                name: "kostenersatzvorgaenge");

            migrationBuilder.DropColumn(
                name: "kostenklasse",
                table: "fahrzeuge");
        }
    }
}
