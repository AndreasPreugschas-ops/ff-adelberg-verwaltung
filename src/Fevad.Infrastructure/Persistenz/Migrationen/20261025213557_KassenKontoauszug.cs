using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Kontoauszug-Import mit Bankformaten, Importen und Umsätzen für den Bankabgleich (V07).</summary>
    public partial class KassenKontoauszug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bankformate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kopfzeilen_ueberspringen = table.Column<int>(type: "integer", nullable: false),
                    spalte_buchungstag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    spalte_valuta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_betrag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_soll = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_haben = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_soll_haben = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_gegenpartei = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_verwendungszweck = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    spalte_referenz = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    datumsformat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dezimalkomma = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bankformate", x => x.id);
                    table.CheckConstraint("ck_bankformate_kopfzeilen", "kopfzeilen_ueberspringen BETWEEN 0 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "kontoauszugimporte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    format = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: true),
                    bis = table.Column<DateOnly>(type: "date", nullable: true),
                    endsaldo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    anzahl = table.Column<int>(type: "integer", nullable: false),
                    neu = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kontoauszugimporte", x => x.id);
                    table.ForeignKey(
                        name: "fk_kontoauszugimporte_zahlungsmittelkonten_konto_id",
                        column: x => x.konto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kontoumsaetze",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buchungstag = table.Column<DateOnly>(type: "date", nullable: false),
                    valuta = table.Column<DateOnly>(type: "date", nullable: true),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gegenpartei = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verwendungszweck = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    referenz = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fingerabdruck = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("pk_kontoumsaetze", x => x.id);
                    table.ForeignKey(
                        name: "fk_kontoumsaetze_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_kontoumsaetze_kontoauszugimporte_import_id",
                        column: x => x.import_id,
                        principalTable: "kontoauszugimporte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kontoumsaetze_zahlungsmittelkonten_konto_id",
                        column: x => x.konto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bankformate_name",
                table: "bankformate",
                column: "name",
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_kontoauszugimporte_konto_id_bis",
                table: "kontoauszugimporte",
                columns: new[] { "konto_id", "bis" });

            migrationBuilder.CreateIndex(
                name: "ix_kontoumsaetze_buchung_id",
                table: "kontoumsaetze",
                column: "buchung_id",
                unique: true,
                filter: "buchung_id IS NOT NULL AND geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_kontoumsaetze_import_id",
                table: "kontoumsaetze",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "ix_kontoumsaetze_konto_id_fingerabdruck",
                table: "kontoumsaetze",
                columns: new[] { "konto_id", "fingerabdruck" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kontoumsaetze_konto_id_status",
                table: "kontoumsaetze",
                columns: new[] { "konto_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bankformate");

            migrationBuilder.DropTable(
                name: "kontoumsaetze");

            migrationBuilder.DropTable(
                name: "kontoauszugimporte");
        }
    }
}
