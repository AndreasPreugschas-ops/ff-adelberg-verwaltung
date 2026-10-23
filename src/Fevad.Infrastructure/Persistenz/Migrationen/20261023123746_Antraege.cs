using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Antraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "foto_einwilligung_am",
                table: "mitglieder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "foto_widerruf_am",
                table: "mitglieder",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "antraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    geburtsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    geburtsort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    geschlecht = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gewuenscht_ab = table.Column<DateOnly>(type: "date", nullable: true),
                    frueher_feuerwehr = table.Column<bool>(type: "boolean", nullable: false),
                    frueher_feuerwehr_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    frueher_von = table.Column<DateOnly>(type: "date", nullable: true),
                    frueher_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    frueher_austrittsgrund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    frueher_lehrgaenge = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    frueher_dienstgrad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    andere_feuerwehr_aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    andere_feuerwehr_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    frueher_ausgeschlossen = table.Column<bool>(type: "boolean", nullable: false),
                    foto_einwilligung = table.Column<bool>(type: "boolean", nullable: true),
                    eingegangen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_antraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_antraege_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_antraege_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "antrag_erziehungsberechtigte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    antrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<int>(type: "integer", nullable: false),
                    verhaeltnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_antrag_erziehungsberechtigte", x => x.id);
                    table.ForeignKey(
                        name: "fk_antrag_erziehungsberechtigte_antraege_antrag_id",
                        column: x => x.antrag_id,
                        principalTable: "antraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_antraege_abteilung_id",
                table: "antraege",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_antraege_mitglied_id",
                table: "antraege",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_antraege_status",
                table: "antraege",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_antrag_erziehungsberechtigte_antrag_id_nummer",
                table: "antrag_erziehungsberechtigte",
                columns: new[] { "antrag_id", "nummer" },
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "antrag_erziehungsberechtigte");

            migrationBuilder.DropTable(
                name: "antraege");

            migrationBuilder.DropColumn(
                name: "foto_einwilligung_am",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "foto_widerruf_am",
                table: "mitglieder");
        }
    }
}
