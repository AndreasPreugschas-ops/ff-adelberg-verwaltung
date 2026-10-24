using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Materialstamm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventurgruppen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_inventurgruppen", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventurgruppen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materialien",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hersteller = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    typ = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    herstellerteilenummer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stueckwert = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    inventarisierungspflichtig = table.Column<bool>(type: "boolean", nullable: false),
                    inventurpflichtig = table.Column<bool>(type: "boolean", nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_materialien", x => x.id);
                    table.CheckConstraint("ck_materialien_inventur_nur_mit_inventarisierung", "NOT inventurpflichtig OR inventarisierungspflichtig");
                    table.ForeignKey(
                        name: "fk_materialien_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<string>(
                name: "rueckgabe_maengel",
                table: "ausstattungen",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "rueckgabe_ordnungsgemaess",
                table: "ausstattungen",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inventurgruppe_id",
                table: "geraete",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "material_id",
                table: "geraete",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inventurgruppe_id",
                table: "inventuren",
                type: "uuid",
                nullable: true);

            // Bestand übernehmen: je Geräteart, Bezeichnung, Hersteller und Typ ein Materialstamm. Arten der Kleiderkammer
            // (persönliche Ausrüstung) sind nicht inventurpflichtig, alles andere schon.
            migrationBuilder.Sql("""
                INSERT INTO materialien (id, bezeichnung, hersteller, typ, geraeteart_id, inventarisierungspflichtig, inventurpflichtig, aktiv, erstellt_am)
                SELECT gen_random_uuid(), x.bezeichnung, x.hersteller, x.typ, x.geraeteart_id, true,
                       NOT EXISTS (SELECT 1 FROM artikel a WHERE a.geraeteart_id = x.geraeteart_id), true, now()
                FROM (SELECT DISTINCT bezeichnung, hersteller, typ, geraeteart_id FROM geraete) x;

                UPDATE geraete g SET material_id = m.id
                FROM materialien m
                WHERE m.geraeteart_id = g.geraeteart_id AND m.bezeichnung = g.bezeichnung
                  AND m.hersteller IS NOT DISTINCT FROM g.hersteller AND m.typ IS NOT DISTINCT FROM g.typ;

                INSERT INTO inventurgruppen (id, bezeichnung, art, fahrzeug_id, sortierung, aktiv, erstellt_am)
                SELECT gen_random_uuid(), f.funkrufname, 'Fahrzeug', f.id, 0, true, now()
                FROM fahrzeuge f
                WHERE EXISTS (SELECT 1 FROM geraete g WHERE g.fahrzeug_id = f.id)
                   OR EXISTS (SELECT 1 FROM inventuren i WHERE i.fahrzeug_id = f.id);

                UPDATE geraete g SET inventurgruppe_id = ig.id FROM inventurgruppen ig WHERE ig.fahrzeug_id = g.fahrzeug_id;
                UPDATE inventuren i SET inventurgruppe_id = ig.id FROM inventurgruppen ig WHERE ig.fahrzeug_id = i.fahrzeug_id;

                INSERT INTO inventurgruppen (id, bezeichnung, art, fahrzeug_id, sortierung, aktiv, erstellt_am)
                SELECT gen_random_uuid(), 'Lager', 'Raum', NULL, 100, true, now()
                WHERE EXISTS (SELECT 1 FROM geraete g JOIN materialien m ON m.id = g.material_id
                              WHERE g.inventurgruppe_id IS NULL AND m.inventurpflichtig);

                UPDATE geraete g SET inventurgruppe_id = (SELECT id FROM inventurgruppen WHERE bezeichnung = 'Lager' AND fahrzeug_id IS NULL)
                FROM materialien m
                WHERE m.id = g.material_id AND g.inventurgruppe_id IS NULL AND m.inventurpflichtig;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "material_id",
                table: "geraete",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_inventuren_fahrzeuge_fahrzeug_id",
                table: "inventuren");

            migrationBuilder.DropIndex(
                name: "ix_inventuren_fahrzeug_id",
                table: "inventuren");

            migrationBuilder.DropColumn(
                name: "fahrzeug_id",
                table: "inventuren");

            migrationBuilder.DropColumn(
                name: "hersteller",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "typ",
                table: "geraete");

            migrationBuilder.CreateIndex(
                name: "ix_inventuren_inventurgruppe_id",
                table: "inventuren",
                column: "inventurgruppe_id");

            migrationBuilder.CreateIndex(
                name: "ix_geraete_inventurgruppe_id",
                table: "geraete",
                column: "inventurgruppe_id");

            migrationBuilder.CreateIndex(
                name: "ix_geraete_material_id",
                table: "geraete",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventurgruppen_bezeichnung",
                table: "inventurgruppen",
                column: "bezeichnung",
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inventurgruppen_fahrzeug_id",
                table: "inventurgruppen",
                column: "fahrzeug_id",
                unique: true,
                filter: "fahrzeug_id IS NOT NULL AND geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_materialien_geraeteart_id",
                table: "materialien",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_materialien_herstellerteilenummer",
                table: "materialien",
                column: "herstellerteilenummer");

            migrationBuilder.AddForeignKey(
                name: "fk_geraete_inventurgruppen_inventurgruppe_id",
                table: "geraete",
                column: "inventurgruppe_id",
                principalTable: "inventurgruppen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_geraete_materialien_material_id",
                table: "geraete",
                column: "material_id",
                principalTable: "materialien",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_inventuren_inventurgruppen_inventurgruppe_id",
                table: "inventuren",
                column: "inventurgruppe_id",
                principalTable: "inventurgruppen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hersteller",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "typ",
                table: "geraete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fahrzeug_id",
                table: "inventuren",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE geraete g SET hersteller = m.hersteller, typ = m.typ FROM materialien m WHERE m.id = g.material_id;
                UPDATE inventuren i SET fahrzeug_id = ig.fahrzeug_id FROM inventurgruppen ig WHERE ig.id = i.inventurgruppe_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_geraete_inventurgruppen_inventurgruppe_id",
                table: "geraete");

            migrationBuilder.DropForeignKey(
                name: "fk_geraete_materialien_material_id",
                table: "geraete");

            migrationBuilder.DropForeignKey(
                name: "fk_inventuren_inventurgruppen_inventurgruppe_id",
                table: "inventuren");

            migrationBuilder.DropIndex(
                name: "ix_inventuren_inventurgruppe_id",
                table: "inventuren");

            migrationBuilder.DropColumn(
                name: "inventurgruppe_id",
                table: "inventuren");

            migrationBuilder.DropIndex(
                name: "ix_geraete_inventurgruppe_id",
                table: "geraete");

            migrationBuilder.DropIndex(
                name: "ix_geraete_material_id",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "inventurgruppe_id",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "material_id",
                table: "geraete");

            migrationBuilder.DropColumn(
                name: "rueckgabe_maengel",
                table: "ausstattungen");

            migrationBuilder.DropColumn(
                name: "rueckgabe_ordnungsgemaess",
                table: "ausstattungen");

            migrationBuilder.DropTable(
                name: "inventurgruppen");

            migrationBuilder.DropTable(
                name: "materialien");

            migrationBuilder.CreateIndex(
                name: "ix_inventuren_fahrzeug_id",
                table: "inventuren",
                column: "fahrzeug_id");

            migrationBuilder.AddForeignKey(
                name: "fk_inventuren_fahrzeuge_fahrzeug_id",
                table: "inventuren",
                column: "fahrzeug_id",
                principalTable: "fahrzeuge",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
