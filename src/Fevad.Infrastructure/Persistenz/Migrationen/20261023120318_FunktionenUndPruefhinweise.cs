using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class FunktionenUndPruefhinweise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "abteilungsart",
                table: "funktionen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grundlage",
                table: "funktionen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voraussetzungen",
                table: "funktionen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "abteilung_id",
                table: "funktion_zuordnungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pruefhinweise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    schluessel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    faellig_am = table.Column<DateOnly>(type: "date", nullable: true),
                    pflicht = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    erkannt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    abgeschlossen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pruefhinweise", x => x.id);
                    table.ForeignKey(
                        name: "fk_pruefhinweise_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funktion_zuordnungen_abteilung_id",
                table: "funktion_zuordnungen",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_pruefhinweise_mitglied_id_schluessel",
                table: "pruefhinweise",
                columns: new[] { "mitglied_id", "schluessel" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pruefhinweise_status",
                table: "pruefhinweise",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_funktion_zuordnungen_abteilungen_abteilung_id",
                table: "funktion_zuordnungen",
                column: "abteilung_id",
                principalTable: "abteilungen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Bestehende Funktionen bekommen Grundlage, Voraussetzungen und Abteilungsart, soweit noch nichts eingetragen ist.
            // Neue Funktionen (ABTKDT, ABTSTV, ZF, SIB, EHF, AUSB, JWSTV, KFW, AAL) legt die Initialisierung beim Start an.
            migrationBuilder.Sql("""
                UPDATE funktionen f SET grundlage = v.grundlage, voraussetzungen = v.voraussetzungen, abteilungsart = v.abteilungsart
                FROM (VALUES
                    ('KDT', 'FwG BW § 8, Feuerwehrsatzung (Wahl, Bestellung durch den Gemeinderat)', 'ZF|VF; LDF', NULL),
                    ('STV', 'FwG BW § 8, Feuerwehrsatzung (Wahl, Bestellung durch den Gemeinderat)', 'ZF|VF; LDF', NULL),
                    ('GF', 'FwDV 3, Feuerwehrsatzung', 'GF|ZF|VF', 'Einsatz'),
                    ('GW', 'DGUV Vorschrift 49, Feuerwehrsatzung', 'GW', NULL),
                    ('AGW', 'FwDV 7, DGUV Vorschrift 49', 'AGW', NULL),
                    ('JW', 'Feuerwehrsatzung (Jugendfeuerwehr), Jugendordnung', 'JFW|JGL', 'Jugend'),
                    ('KAS', 'FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung', NULL, NULL),
                    ('SF', 'Feuerwehrsatzung', NULL, NULL),
                    ('KW', 'Feuerwehrsatzung', NULL, NULL),
                    ('KP', 'FwG BW § 18 (Kameradschaftskasse), Feuerwehrsatzung', NULL, NULL),
                    ('AUS', 'FwG BW, Feuerwehrsatzung (Feuerwehrausschuss)', NULL, NULL))
                    AS v(code, grundlage, voraussetzungen, abteilungsart)
                WHERE f.code = v.code AND f.grundlage IS NULL AND f.voraussetzungen IS NULL;
                UPDATE funktionen SET amtszeit_jahre = 5 WHERE code = 'JW' AND amtszeit_jahre IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_funktion_zuordnungen_abteilungen_abteilung_id",
                table: "funktion_zuordnungen");

            migrationBuilder.DropTable(
                name: "pruefhinweise");

            migrationBuilder.DropIndex(
                name: "ix_funktion_zuordnungen_abteilung_id",
                table: "funktion_zuordnungen");

            migrationBuilder.DropColumn(
                name: "abteilungsart",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "grundlage",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "voraussetzungen",
                table: "funktionen");

            migrationBuilder.DropColumn(
                name: "abteilung_id",
                table: "funktion_zuordnungen");
        }
    }
}
