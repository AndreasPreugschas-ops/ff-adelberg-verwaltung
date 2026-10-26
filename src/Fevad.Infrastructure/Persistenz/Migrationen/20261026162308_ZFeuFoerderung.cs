using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class ZFeuFoerderung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "foerdergegenstaende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    festbetrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    festbetrag_rahmenvertrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    zweckbindung_jahre = table.Column<int>(type: "integer", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: false),
                    fundstelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kurzbezeichnung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foerdergegenstaende", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "foerderantraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    beschaffung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    foerdergegenstand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rahmenvertrag = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    antragsjahr = table.Column<int>(type: "integer", nullable: false),
                    kbm_abgestimmt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    gemeinderatsbeschluss_am = table.Column<DateOnly>(type: "date", nullable: true),
                    gemeinderatsbeschluss = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    beantragt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bescheid_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bescheid_aktenzeichen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zuwendung = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    altfahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rechnung_liegt_vor = table.Column<bool>(type: "boolean", nullable: false),
                    richtigkeitsbescheinigung_liegt_vor = table.Column<bool>(type: "boolean", nullable: false),
                    abnahmebericht_liegt_vor = table.Column<bool>(type: "boolean", nullable: false),
                    beladungserklaerung_liegt_vor = table.Column<bool>(type: "boolean", nullable: false),
                    nachweis_eingereicht_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ausgezahlt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foerderantraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_foerderantraege_beschaffungen_beschaffung_id",
                        column: x => x.beschaffung_id,
                        principalTable: "beschaffungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_foerderantraege_fahrzeuge_altfahrzeug_id",
                        column: x => x.altfahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_foerderantraege_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_foerderantraege_foerdergegenstaende_foerdergegenstand_id",
                        column: x => x.foerdergegenstand_id,
                        principalTable: "foerdergegenstaende",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_foerderantraege_altfahrzeug_id",
                table: "foerderantraege",
                column: "altfahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_foerderantraege_beschaffung_id",
                table: "foerderantraege",
                column: "beschaffung_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_foerderantraege_fahrzeug_id",
                table: "foerderantraege",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_foerderantraege_foerdergegenstand_id",
                table: "foerderantraege",
                column: "foerdergegenstand_id");

            migrationBuilder.CreateIndex(
                name: "ix_foerdergegenstaende_code",
                table: "foerdergegenstaende",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "foerderantraege");

            migrationBuilder.DropTable(
                name: "foerdergegenstaende");
        }
    }
}
