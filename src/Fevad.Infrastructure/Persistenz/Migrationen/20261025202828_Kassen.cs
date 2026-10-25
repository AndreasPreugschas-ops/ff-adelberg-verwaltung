using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Kassen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kassen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rechtsform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_kassen", x => x.id);
                    table.ForeignKey(
                        name: "fk_kassen_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planpositionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    richtung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    deckungsfaehig = table.Column<bool>(type: "boolean", nullable: false),
                    hinweis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planpositionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_planpositionen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wirtschaftsplaene",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    beschluss_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beschlossen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    zustimmung_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nachtrag = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wirtschaftsplaene", x => x.id);
                    table.ForeignKey(
                        name: "fk_wirtschaftsplaene_beschluesse_beschluss_id",
                        column: x => x.beschluss_id,
                        principalTable: "beschluesse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_wirtschaftsplaene_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zahlungsmittelkonten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    eroeffnet = table.Column<DateOnly>(type: "date", nullable: false),
                    anfangsbestand = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zahlungsmittelkonten", x => x.id);
                    table.ForeignKey(
                        name: "fk_zahlungsmittelkonten_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zahlungsanordnungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    empfaenger = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    zweck = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    planposition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    faellig_am = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ausschussbeschluss_id = table.Column<Guid>(type: "uuid", nullable: true),
                    angeordnet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    angeordnet_von = table.Column<Guid>(type: "uuid", nullable: true),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_zahlungsanordnungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_zahlungsanordnungen_beschluesse_ausschussbeschluss_id",
                        column: x => x.ausschussbeschluss_id,
                        principalTable: "beschluesse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_zahlungsanordnungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_zahlungsanordnungen_planpositionen_planposition_id",
                        column: x => x.planposition_id,
                        principalTable: "planpositionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planansaetze",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wirtschaftsplan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planposition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    erlaeuterung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planansaetze", x => x.id);
                    table.ForeignKey(
                        name: "fk_planansaetze_planpositionen_planposition_id",
                        column: x => x.planposition_id,
                        principalTable: "planpositionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planansaetze_wirtschaftsplaene_wirtschaftsplan_id",
                        column: x => x.wirtschaftsplan_id,
                        principalTable: "wirtschaftsplaene",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "buchungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    belegnummer = table.Column<int>(type: "integer", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gegenkonto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    planposition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    belegart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    eigenbeleg_begruendung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ausschussbeschluss_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beschlussverweis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    festgeschrieben_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    festgeschrieben_von = table.Column<Guid>(type: "uuid", nullable: true),
                    storno_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zahlungsanordnung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buchungen", x => x.id);
                    table.CheckConstraint("ck_buchungen_betrag", "betrag > 0");
                    table.ForeignKey(
                        name: "fk_buchungen_beschluesse_ausschussbeschluss_id",
                        column: x => x.ausschussbeschluss_id,
                        principalTable: "beschluesse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_buchungen_buchungen_storno_von_id",
                        column: x => x.storno_von_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buchungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buchungen_planpositionen_planposition_id",
                        column: x => x.planposition_id,
                        principalTable: "planpositionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buchungen_zahlungsanordnungen_zahlungsanordnung_id",
                        column: x => x.zahlungsanordnung_id,
                        principalTable: "zahlungsanordnungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_buchungen_zahlungsmittelkonten_gegenkonto_id",
                        column: x => x.gegenkonto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buchungen_zahlungsmittelkonten_konto_id",
                        column: x => x.konto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kassenbelege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zahlungsanordnung_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_kassenbelege", x => x.id);
                    table.CheckConstraint("ck_kassenbelege_bezug", "buchung_id IS NOT NULL OR zahlungsanordnung_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_kassenbelege_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kassenbelege_zahlungsanordnungen_zahlungsanordnung_id",
                        column: x => x.zahlungsanordnung_id,
                        principalTable: "zahlungsanordnungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_ausschussbeschluss_id",
                table: "buchungen",
                column: "ausschussbeschluss_id");

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_gegenkonto_id",
                table: "buchungen",
                column: "gegenkonto_id");

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_kasse_id_datum",
                table: "buchungen",
                columns: new[] { "kasse_id", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_kasse_id_jahr_belegnummer",
                table: "buchungen",
                columns: new[] { "kasse_id", "jahr", "belegnummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_konto_id",
                table: "buchungen",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_planposition_id",
                table: "buchungen",
                column: "planposition_id");

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_storno_von_id",
                table: "buchungen",
                column: "storno_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_buchungen_zahlungsanordnung_id",
                table: "buchungen",
                column: "zahlungsanordnung_id");

            migrationBuilder.CreateIndex(
                name: "ix_kassen_abteilung_id",
                table: "kassen",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_kassenbelege_buchung_id",
                table: "kassenbelege",
                column: "buchung_id");

            migrationBuilder.CreateIndex(
                name: "ix_kassenbelege_zahlungsanordnung_id",
                table: "kassenbelege",
                column: "zahlungsanordnung_id");

            migrationBuilder.CreateIndex(
                name: "ix_planansaetze_planposition_id",
                table: "planansaetze",
                column: "planposition_id");

            migrationBuilder.CreateIndex(
                name: "ix_planansaetze_wirtschaftsplan_id_planposition_id",
                table: "planansaetze",
                columns: new[] { "wirtschaftsplan_id", "planposition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planpositionen_kasse_id_nummer",
                table: "planpositionen",
                columns: new[] { "kasse_id", "nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wirtschaftsplaene_beschluss_id",
                table: "wirtschaftsplaene",
                column: "beschluss_id");

            migrationBuilder.CreateIndex(
                name: "ix_wirtschaftsplaene_kasse_id_jahr",
                table: "wirtschaftsplaene",
                columns: new[] { "kasse_id", "jahr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zahlungsanordnungen_ausschussbeschluss_id",
                table: "zahlungsanordnungen",
                column: "ausschussbeschluss_id");

            migrationBuilder.CreateIndex(
                name: "ix_zahlungsanordnungen_kasse_id_status",
                table: "zahlungsanordnungen",
                columns: new[] { "kasse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_zahlungsanordnungen_planposition_id",
                table: "zahlungsanordnungen",
                column: "planposition_id");

            migrationBuilder.CreateIndex(
                name: "ix_zahlungsmittelkonten_kasse_id",
                table: "zahlungsmittelkonten",
                column: "kasse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kassenbelege");

            migrationBuilder.DropTable(
                name: "planansaetze");

            migrationBuilder.DropTable(
                name: "buchungen");

            migrationBuilder.DropTable(
                name: "wirtschaftsplaene");

            migrationBuilder.DropTable(
                name: "zahlungsanordnungen");

            migrationBuilder.DropTable(
                name: "zahlungsmittelkonten");

            migrationBuilder.DropTable(
                name: "planpositionen");

            migrationBuilder.DropTable(
                name: "kassen");
        }
    }
}
