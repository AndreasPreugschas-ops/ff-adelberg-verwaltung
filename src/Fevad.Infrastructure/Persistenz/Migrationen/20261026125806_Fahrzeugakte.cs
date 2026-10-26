using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Fahrzeugakte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fahrzeugmasse_bis_kg",
                table: "qualifikationen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "besatzung",
                table: "fahrzeuge",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "betriebsstunden",
                table: "fahrzeuge",
                type: "numeric(10,1)",
                precision: 10,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fin",
                table: "fahrzeuge",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "kilometerstand",
                table: "fahrzeuge",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "loeschwasser_liter",
                table: "fahrzeuge",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pumpe",
                table: "fahrzeuge",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "schaummittel_liter",
                table: "fahrzeuge",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "stand_am",
                table: "fahrzeuge",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zulaessige_gesamtmasse_kg",
                table: "fahrzeuge",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zuwendungsbescheid_aktenzeichen",
                table: "fahrzeuge",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "zuwendungsbescheid_am",
                table: "fahrzeuge",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "beladekontrollen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    kontrolliert_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vollstaendig = table.Column<bool>(type: "boolean", nullable: false),
                    fehlend = table.Column<int>(type: "integer", nullable: false),
                    ergebnis = table.Column<string>(type: "jsonb", nullable: false),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_beladekontrollen", x => x.id);
                    table.ForeignKey(
                        name: "fk_beladekontrollen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_beladekontrollen_maengel_mangel_id",
                        column: x => x.mangel_id,
                        principalTable: "maengel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_beladekontrollen_mitglieder_kontrolliert_von_id",
                        column: x => x.kontrolliert_von_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "beladepositionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fach = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    menge = table.Column<int>(type: "integer", nullable: false),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_beladepositionen", x => x.id);
                    table.CheckConstraint("ck_beladepositionen_menge", "menge > 0");
                    table.ForeignKey(
                        name: "fk_beladepositionen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_beladepositionen_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_beladepositionen_materialien_material_id",
                        column: x => x.material_id,
                        principalTable: "materialien",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beladekontrollen_fahrzeug_id_datum",
                table: "beladekontrollen",
                columns: new[] { "fahrzeug_id", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_beladekontrollen_kontrolliert_von_id",
                table: "beladekontrollen",
                column: "kontrolliert_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_beladekontrollen_mangel_id",
                table: "beladekontrollen",
                column: "mangel_id");

            migrationBuilder.CreateIndex(
                name: "ix_beladepositionen_fahrzeug_id_fach",
                table: "beladepositionen",
                columns: new[] { "fahrzeug_id", "fach" });

            migrationBuilder.CreateIndex(
                name: "ix_beladepositionen_geraeteart_id",
                table: "beladepositionen",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_beladepositionen_material_id",
                table: "beladepositionen",
                column: "material_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beladekontrollen");

            migrationBuilder.DropTable(
                name: "beladepositionen");

            migrationBuilder.DropColumn(
                name: "fahrzeugmasse_bis_kg",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "besatzung",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "betriebsstunden",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "fin",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "kilometerstand",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "loeschwasser_liter",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "pumpe",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "schaummittel_liter",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "stand_am",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "zulaessige_gesamtmasse_kg",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "zuwendungsbescheid_aktenzeichen",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "zuwendungsbescheid_am",
                table: "fahrzeuge");
        }
    }
}
