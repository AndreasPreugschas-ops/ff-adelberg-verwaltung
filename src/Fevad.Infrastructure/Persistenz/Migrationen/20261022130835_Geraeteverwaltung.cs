using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Geraeteverwaltung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pruefer",
                table: "pruefplan_positionen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "beschreibung",
                table: "maengel",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "massnahme",
                table: "maengel",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fuer_fahrzeuge",
                table: "geraetearten",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "typ",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "seriennummer",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lagerort",
                table: "geraete",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hersteller",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "anschaffungspreis",
                table: "geraete",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aussonderungsgrund",
                table: "geraete",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bemerkung",
                table: "geraete",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "beschafft_am",
                table: "geraete",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "beschaffung_id",
                table: "geraete",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "groesse",
                table: "geraete",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "herstelldatum",
                table: "geraete",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bemerkung",
                table: "fahrzeuge",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pruefvorlage_id",
                table: "fahrzeuge",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "beschaffungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    menge = table.Column<int>(type: "integer", nullable: false),
                    begruendung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    prioritaet = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    haushaltsjahr = table.Column<int>(type: "integer", nullable: true),
                    kostenschaetzung = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    foerderfaehig = table.Column<bool>(type: "boolean", nullable: false),
                    genehmigt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    genehmigt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    lieferant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bestellnummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bestellt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    geliefert_am = table.Column<DateOnly>(type: "date", nullable: true),
                    istkosten = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
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
                    table.PrimaryKey("pk_beschaffungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_beschaffungen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_beschaffungen_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_beschaffungen_maengel_mangel_id",
                        column: x => x.mangel_id,
                        principalTable: "maengel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventuren",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    stichtag = table.Column<DateOnly>(type: "date", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    abgeschlossen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventuren", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventuren_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventur_positionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    soll_standort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    befund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    erfasst_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    erfasst_von = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_inventur_positionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventur_positionen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventur_positionen_inventuren_inventur_id",
                        column: x => x.inventur_id,
                        principalTable: "inventuren",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_maengel_pruefung_id",
                table: "maengel",
                column: "pruefung_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_status",
                table: "maengel",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_geraete_beschaffung_id",
                table: "geraete",
                column: "beschaffung_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrzeuge_pruefvorlage_id",
                table: "fahrzeuge",
                column: "pruefvorlage_id");

            migrationBuilder.CreateIndex(
                name: "ix_beschaffungen_fahrzeug_id",
                table: "beschaffungen",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_beschaffungen_geraeteart_id",
                table: "beschaffungen",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_beschaffungen_mangel_id",
                table: "beschaffungen",
                column: "mangel_id");

            migrationBuilder.CreateIndex(
                name: "ix_beschaffungen_status",
                table: "beschaffungen",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_inventur_positionen_geraet_id",
                table: "inventur_positionen",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventur_positionen_inventur_id_geraet_id",
                table: "inventur_positionen",
                columns: new[] { "inventur_id", "geraet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventuren_fahrzeug_id",
                table: "inventuren",
                column: "fahrzeug_id");

            migrationBuilder.AddForeignKey(
                name: "fk_fahrzeuge_geraetearten_pruefvorlage_id",
                table: "fahrzeuge",
                column: "pruefvorlage_id",
                principalTable: "geraetearten",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_geraete_beschaffungen_beschaffung_id",
                table: "geraete",
                column: "beschaffung_id",
                principalTable: "beschaffungen",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_pruefungen_pruefung_id",
                table: "maengel",
                column: "pruefung_id",
                principalTable: "pruefungen",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_fahrzeuge_geraetearten_pruefvorlage_id",
                table: "fahrzeuge");

            migrationBuilder.DropForeignKey(
                name: "fk_geraete_beschaffungen_beschaffung_id",
                table: "geraete");

            migrationBuilder.DropForeignKey(
                name: "fk_maengel_pruefungen_pruefung_id",
                table: "maengel");

            migrationBuilder.DropTable(
                name: "beschaffungen");

            migrationBuilder.DropTable(
                name: "inventur_positionen");

            migrationBuilder.DropTable(
                name: "inventuren");

            migrationBuilder.DropIndex(
                name: "ix_maengel_pruefung_id",
                table: "maengel");

            migrationBuilder.DropIndex(
                name: "ix_maengel_status",
                table: "maengel");

            migrationBuilder.DropIndex(
                name: "ix_geraete_beschaffung_id",
                table: "geraete");

            migrationBuilder.DropIndex(
                name: "ix_fahrzeuge_pruefvorlage_id",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "pruefer",
                table: "pruefplan_positionen");

            migrationBuilder.DropColumn(
                name: "massnahme",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "fuer_fahrzeuge",
                table: "geraetearten");

            migrationBuilder.DropColumn(
                name: "anschaffungspreis",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "aussonderungsgrund",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "bemerkung",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "beschafft_am",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "beschaffung_id",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "groesse",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "herstelldatum",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "bemerkung",
                table: "fahrzeuge");

            migrationBuilder.DropColumn(
                name: "pruefvorlage_id",
                table: "fahrzeuge");

            migrationBuilder.AlterColumn<string>(
                name: "beschreibung",
                table: "maengel",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "typ",
                table: "geraete",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "seriennummer",
                table: "geraete",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "lagerort",
                table: "geraete",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hersteller",
                table: "geraete",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
