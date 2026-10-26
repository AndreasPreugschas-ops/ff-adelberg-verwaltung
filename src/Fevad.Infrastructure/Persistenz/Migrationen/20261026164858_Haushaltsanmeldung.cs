using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Haushaltsanmeldung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "haushaltsanmeldungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    beraten_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ausschussbeschluss = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    freigegeben_am = table.Column<DateOnly>(type: "date", nullable: true),
                    freigegeben_von = table.Column<Guid>(type: "uuid", nullable: true),
                    eingereicht_am = table.Column<DateOnly>(type: "date", nullable: true),
                    haushalt_beschlossen_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_haushaltsanmeldungen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "haushaltsstellen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    produkt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kostenstelle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    innenauftrag = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sachkonto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    teil = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einzahlung = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_haushaltsstellen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kostenansaetze",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geraeteart_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    satz = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    haushaltsstelle_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_kostenansaetze", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "haushaltspositionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    haushaltsanmeldung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    haushaltsstelle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bereich = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    menge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    einzelpreis = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    preisquelle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    prioritaet = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pflicht = table.Column<bool>(type: "boolean", nullable: false),
                    begruendung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rechtsgrundlage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    folgen_bei_ablehnung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    herkunft = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    beschaffung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_folgejahr1 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    rate_folgejahr2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    rate_folgejahr3 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    verpflichtungsermaechtigung = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    zuwendung = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    folgekosten = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    bedarfsplan = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    beschlossener_betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    rueckmeldung_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_haushaltspositionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_haushaltspositionen_beschaffungen_beschaffung_id",
                        column: x => x.beschaffung_id,
                        principalTable: "beschaffungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_haushaltspositionen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_haushaltspositionen_haushaltsanmeldungen_haushaltsanmeldung",
                        column: x => x.haushaltsanmeldung_id,
                        principalTable: "haushaltsanmeldungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_haushaltspositionen_haushaltsstellen_haushaltsstelle_id",
                        column: x => x.haushaltsstelle_id,
                        principalTable: "haushaltsstellen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_haushaltspositionen_maengel_mangel_id",
                        column: x => x.mangel_id,
                        principalTable: "maengel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_haushaltsanmeldungen_jahr",
                table: "haushaltsanmeldungen",
                column: "jahr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_haushaltspositionen_beschaffung_id",
                table: "haushaltspositionen",
                column: "beschaffung_id");

            migrationBuilder.CreateIndex(
                name: "ix_haushaltspositionen_fahrzeug_id",
                table: "haushaltspositionen",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_haushaltspositionen_haushaltsanmeldung_id_herkunft",
                table: "haushaltspositionen",
                columns: new[] { "haushaltsanmeldung_id", "herkunft" });

            migrationBuilder.CreateIndex(
                name: "ix_haushaltspositionen_haushaltsstelle_id",
                table: "haushaltspositionen",
                column: "haushaltsstelle_id");

            migrationBuilder.CreateIndex(
                name: "ix_haushaltspositionen_mangel_id",
                table: "haushaltspositionen",
                column: "mangel_id");

            migrationBuilder.CreateIndex(
                name: "ix_haushaltsstellen_code",
                table: "haushaltsstellen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kostenansaetze_code",
                table: "kostenansaetze",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "haushaltspositionen");

            migrationBuilder.DropTable(
                name: "kostenansaetze");

            migrationBuilder.DropTable(
                name: "haushaltsanmeldungen");

            migrationBuilder.DropTable(
                name: "haushaltsstellen");
        }
    }
}
