using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Steuerbereich und Steuersatz je Buchung, Veranstaltungen mit Barkassenberichten (V03).</summary>
    public partial class KassenSteuerUndVeranstaltungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "steuerbereich",
                table: "planpositionen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Intern");

            migrationBuilder.AddColumn<string>(
                name: "steuerbereich",
                table: "buchungen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Intern");

            migrationBuilder.AddColumn<string>(
                name: "steuersatz",
                table: "buchungen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Ohne");

            migrationBuilder.AddColumn<Guid>(
                name: "veranstaltung_id",
                table: "buchungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "veranstaltungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    beginn = table.Column<DateOnly>(type: "date", nullable: false),
                    ende = table.Column<DateOnly>(type: "date", nullable: true),
                    mitveranstalter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    anteil_prozent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    abgerechnet = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_veranstaltungen", x => x.id);
                    table.CheckConstraint("ck_veranstaltungen_anteil", "anteil_prozent > 0 AND anteil_prozent <= 100");
                    table.ForeignKey(
                        name: "fk_veranstaltungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "barkassenberichte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veranstaltung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<DateOnly>(type: "date", nullable: false),
                    stelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    wechselgeld = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gezaehlt_von = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    zweite_person = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    steuersatz = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_barkassenberichte", x => x.id);
                    table.ForeignKey(
                        name: "fk_barkassenberichte_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_barkassenberichte_veranstaltungen_veranstaltung_id",
                        column: x => x.veranstaltung_id,
                        principalTable: "veranstaltungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zaehlpositionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    barkassenbericht_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wert = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    anzahl = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zaehlpositionen", x => x.id);
                    table.CheckConstraint("ck_zaehlpositionen_anzahl", "anzahl >= 0");
                    table.ForeignKey(
                        name: "fk_zaehlpositionen_barkassenberichte_barkassenbericht_id",
                        column: x => x.barkassenbericht_id,
                        principalTable: "barkassenberichte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_veranstaltung_id",
                table: "buchungen",
                column: "veranstaltung_id");

            migrationBuilder.CreateIndex(
                name: "ix_barkassenberichte_buchung_id",
                table: "barkassenberichte",
                column: "buchung_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_barkassenberichte_veranstaltung_id",
                table: "barkassenberichte",
                column: "veranstaltung_id");

            migrationBuilder.CreateIndex(
                name: "ix_veranstaltungen_kasse_id_beginn",
                table: "veranstaltungen",
                columns: new[] { "kasse_id", "beginn" });

            migrationBuilder.CreateIndex(
                name: "ix_zaehlpositionen_barkassenbericht_id_wert",
                table: "zaehlpositionen",
                columns: new[] { "barkassenbericht_id", "wert" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_buchungen_veranstaltungen_veranstaltung_id",
                table: "buchungen",
                column: "veranstaltung_id",
                principalTable: "veranstaltungen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Feste und Verkauf der Kameradschaftskasse gehören zur wirtschaftlichen Tätigkeit; bisherige Buchungen übernehmen den Bereich der Position.
            migrationBuilder.Sql("UPDATE planpositionen SET steuerbereich = 'Wirtschaftlich' WHERE nummer IN ('2', '5') AND kasse_id IN (SELECT id FROM kassen WHERE name = 'Kameradschaftskasse');");
            migrationBuilder.Sql("UPDATE buchungen b SET steuerbereich = p.steuerbereich FROM planpositionen p WHERE b.planposition_id = p.id AND b.art <> 'Umbuchung';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_buchungen_veranstaltungen_veranstaltung_id",
                table: "buchungen");

            migrationBuilder.DropTable(
                name: "zaehlpositionen");

            migrationBuilder.DropTable(
                name: "barkassenberichte");

            migrationBuilder.DropTable(
                name: "veranstaltungen");

            migrationBuilder.DropIndex(
                name: "ix_buchungen_veranstaltung_id",
                table: "buchungen");

            migrationBuilder.DropColumn(
                name: "steuerbereich",
                table: "planpositionen");

            migrationBuilder.DropColumn(
                name: "steuerbereich",
                table: "buchungen");

            migrationBuilder.DropColumn(
                name: "steuersatz",
                table: "buchungen");

            migrationBuilder.DropColumn(
                name: "veranstaltung_id",
                table: "buchungen");
        }
    }
}
