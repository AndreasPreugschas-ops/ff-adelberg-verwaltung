using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinsatzBeteiligteUndKosten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einsatz_beteiligte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rolle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kennzeichen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fahrzeugbeschreibung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    versicherung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    versicherungsnummer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_einsatz_beteiligte", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_beteiligte_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "einsatz_organisationen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ansprechpartner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aktenzeichen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_einsatz_organisationen", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_organisationen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kostensaetze",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    einheit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    quelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kostensaetze", x => x.id);
                    table.ForeignKey(
                        name: "fk_kostensaetze_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "einsatz_ausruestungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostensatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatz_ausruestungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_ausruestungen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatz_ausruestungen_kostensaetze_kostensatz_id",
                        column: x => x.kostensatz_id,
                        principalTable: "kostensaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_ausruestungen_einsatz_id",
                table: "einsatz_ausruestungen",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_ausruestungen_kostensatz_id",
                table: "einsatz_ausruestungen",
                column: "kostensatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_beteiligte_einsatz_id",
                table: "einsatz_beteiligte",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_organisationen_einsatz_id",
                table: "einsatz_organisationen",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_kostensaetze_art_fahrzeug_id_gueltig_ab",
                table: "kostensaetze",
                columns: new[] { "art", "fahrzeug_id", "gueltig_ab" });

            migrationBuilder.CreateIndex(
                name: "ix_kostensaetze_fahrzeug_id",
                table: "kostensaetze",
                column: "fahrzeug_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einsatz_ausruestungen");

            migrationBuilder.DropTable(
                name: "einsatz_beteiligte");

            migrationBuilder.DropTable(
                name: "einsatz_organisationen");

            migrationBuilder.DropTable(
                name: "kostensaetze");
        }
    }
}
