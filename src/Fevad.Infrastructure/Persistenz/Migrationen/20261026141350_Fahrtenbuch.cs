using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Fahrtenbuch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "betankungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    kraftstoff = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    liter = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    kilometerstand = table.Column<int>(type: "integer", nullable: true),
                    beleg = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    getankt_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_betankungen", x => x.id);
                    table.CheckConstraint("ck_betankungen_liter", "liter > 0");
                    table.ForeignKey(
                        name: "fk_betankungen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_betankungen_mitglieder_getankt_von_id",
                        column: x => x.getankt_von_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fahrten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    beginn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ende = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    zweck = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ziel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    km_beginn = table.Column<int>(type: "integer", nullable: true),
                    km_ende = table.Column<int>(type: "integer", nullable: true),
                    kilometer = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_fahrten", x => x.id);
                    table.CheckConstraint("ck_fahrten_km", "km_ende IS NULL OR km_beginn IS NULL OR km_ende >= km_beginn");
                    table.ForeignKey(
                        name: "fk_fahrten_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_fahrten_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_fahrten_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fahrten_mitglieder_fahrer_id",
                        column: x => x.fahrer_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_betankungen_fahrzeug_id_datum",
                table: "betankungen",
                columns: new[] { "fahrzeug_id", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_betankungen_getankt_von_id",
                table: "betankungen",
                column: "getankt_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrten_dienst_id",
                table: "fahrten",
                column: "dienst_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrten_einsatz_id",
                table: "fahrten",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrten_fahrer_id",
                table: "fahrten",
                column: "fahrer_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrten_fahrzeug_id_beginn",
                table: "fahrten",
                columns: new[] { "fahrzeug_id", "beginn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "betankungen");

            migrationBuilder.DropTable(
                name: "fahrten");
        }
    }
}
