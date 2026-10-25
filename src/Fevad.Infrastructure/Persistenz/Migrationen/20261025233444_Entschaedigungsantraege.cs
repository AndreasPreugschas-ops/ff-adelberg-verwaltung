using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Entschädigungsanträge der Mitglieder mit Tagen und Belegen (V32) und steuerliche Art je Abrechnungsposten (V34).</summary>
    public partial class Entschaedigungsantraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "entschaedigungsantrag_id",
                table: "abrechnungs_posten",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "steuerart",
                table: "abrechnungs_posten",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Aufwandsentschaedigung");

            migrationBuilder.Sql("UPDATE abrechnungs_posten SET steuerart = 'Uebungsleiter' WHERE art = 'Leitung';");

            migrationBuilder.CreateTable(
                name: "entschaedigungsantraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anlass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lehrgangsteilnahme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erwerbslage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    verdienstausfall = table.Column<bool>(type: "boolean", nullable: false),
                    verdienstausfall_betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    sozialversicherung_betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    auslagen_betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fahrtart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kilometer = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    fahrkosten = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    stand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    eingereicht_am = table.Column<DateOnly>(type: "date", nullable: false),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entschieden_von = table.Column<Guid>(type: "uuid", nullable: true),
                    entscheidungsvermerk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entschaedigungsantraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_entschaedigungsantraege_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_entschaedigungsantraege_lehrgangsteilnahmen_lehrgangsteilna",
                        column: x => x.lehrgangsteilnahme_id,
                        principalTable: "lehrgangsteilnahmen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_entschaedigungsantraege_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "antrag_belege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entschaedigungsantrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    pruefsumme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_antrag_belege", x => x.id);
                    table.ForeignKey(
                        name: "fk_antrag_belege_entschaedigungsantraege_entschaedigungsantrag",
                        column: x => x.entschaedigungsantrag_id,
                        principalTable: "entschaedigungsantraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "antrag_tage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entschaedigungsantrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    von = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bis = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_antrag_tage", x => x.id);
                    table.ForeignKey(
                        name: "fk_antrag_tage_entschaedigungsantraege_entschaedigungsantrag_id",
                        column: x => x.entschaedigungsantrag_id,
                        principalTable: "entschaedigungsantraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_entschaedigungsantrag_id",
                table: "abrechnungs_posten",
                column: "entschaedigungsantrag_id");

            migrationBuilder.CreateIndex(
                name: "ix_antrag_belege_entschaedigungsantrag_id",
                table: "antrag_belege",
                column: "entschaedigungsantrag_id");

            migrationBuilder.CreateIndex(
                name: "ix_antrag_tage_entschaedigungsantrag_id",
                table: "antrag_tage",
                column: "entschaedigungsantrag_id");

            migrationBuilder.CreateIndex(
                name: "ix_entschaedigungsantraege_einsatz_id",
                table: "entschaedigungsantraege",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_entschaedigungsantraege_lehrgangsteilnahme_id",
                table: "entschaedigungsantraege",
                column: "lehrgangsteilnahme_id");

            migrationBuilder.CreateIndex(
                name: "ix_entschaedigungsantraege_mitglied_id_stand",
                table: "entschaedigungsantraege",
                columns: new[] { "mitglied_id", "stand" });

            migrationBuilder.AddForeignKey(
                name: "fk_abrechnungs_posten_entschaedigungsantraege_entschaedigungsa",
                table: "abrechnungs_posten",
                column: "entschaedigungsantrag_id",
                principalTable: "entschaedigungsantraege",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_abrechnungs_posten_entschaedigungsantraege_entschaedigungsa",
                table: "abrechnungs_posten");

            migrationBuilder.DropTable(
                name: "antrag_belege");

            migrationBuilder.DropTable(
                name: "antrag_tage");

            migrationBuilder.DropTable(
                name: "entschaedigungsantraege");

            migrationBuilder.DropIndex(
                name: "ix_abrechnungs_posten_entschaedigungsantrag_id",
                table: "abrechnungs_posten");

            migrationBuilder.DropColumn(
                name: "entschaedigungsantrag_id",
                table: "abrechnungs_posten");

            migrationBuilder.DropColumn(
                name: "steuerart",
                table: "abrechnungs_posten");
        }
    }
}
