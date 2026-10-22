using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Entschaedigung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "abrechnungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    abgeschlossen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    abgeschlossen_von = table.Column<Guid>(type: "uuid", nullable: true),
                    hinweise = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abrechnungen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "abrechnungs_posten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    abrechnung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    vorgang = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einsatz_teilnahme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anwesenheit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funktion_zuordnung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    datum_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    minuten = table.Column<int>(type: "integer", nullable: true),
                    menge = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    einheit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    satz = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abrechnungs_posten", x => x.id);
                    table.ForeignKey(
                        name: "fk_abrechnungs_posten_abrechnungen_abrechnung_id",
                        column: x => x.abrechnung_id,
                        principalTable: "abrechnungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_abrechnungs_posten_anwesenheiten_anwesenheit_id",
                        column: x => x.anwesenheit_id,
                        principalTable: "anwesenheiten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_abrechnungs_posten_einsatz_teilnahmen_einsatz_teilnahme_id",
                        column: x => x.einsatz_teilnahme_id,
                        principalTable: "einsatz_teilnahmen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_abrechnungs_posten_funktion_zuordnungen_funktion_zuordnung_",
                        column: x => x.funktion_zuordnung_id,
                        principalTable: "funktion_zuordnungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_abrechnungs_posten_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungen_von_bis",
                table: "abrechnungen",
                columns: new[] { "von", "bis" });

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_abrechnung_id",
                table: "abrechnungs_posten",
                column: "abrechnung_id");

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_anwesenheit_id",
                table: "abrechnungs_posten",
                column: "anwesenheit_id");

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_einsatz_teilnahme_id",
                table: "abrechnungs_posten",
                column: "einsatz_teilnahme_id");

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_funktion_zuordnung_id",
                table: "abrechnungs_posten",
                column: "funktion_zuordnung_id");

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_mitglied_id",
                table: "abrechnungs_posten",
                column: "mitglied_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abrechnungs_posten");

            migrationBuilder.DropTable(
                name: "abrechnungen");
        }
    }
}
