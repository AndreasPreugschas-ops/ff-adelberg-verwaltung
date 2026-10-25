using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class GeraeteNachEinsatzUndPruefpraxis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "storniert_am",
                table: "pruefungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "storniert_von",
                table: "pruefungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stornogrund",
                table: "pruefungen",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "einsatz_id",
                table: "maengel",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "erledigt_durch_pruefung_id",
                table: "maengel",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ruecklauf",
                table: "geraetearten",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Geraetewart");

            migrationBuilder.AddColumn<bool>(
                name: "psa_waesche",
                table: "einsatz_teilnahmen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "psa_waesche_erledigt_am",
                table: "einsatz_teilnahmen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "einsatz_geraete",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anzahl = table.Column<int>(type: "integer", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    belastung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erledigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    erledigt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatz_geraete", x => x.id);
                    table.CheckConstraint("ck_einsatz_geraete_geraet_oder_material", "(geraet_id IS NULL) <> (material_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_einsatz_geraete_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatz_geraete_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_einsatz_geraete_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_einsatz_geraete_maengel_mangel_id",
                        column: x => x.mangel_id,
                        principalTable: "maengel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_einsatz_geraete_materialien_material_id",
                        column: x => x.material_id,
                        principalTable: "materialien",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_maengel_einsatz_id",
                table: "maengel",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_erledigt_durch_pruefung_id",
                table: "maengel",
                column: "erledigt_durch_pruefung_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_einsatz_id",
                table: "einsatz_geraete",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_erledigt_am",
                table: "einsatz_geraete",
                column: "erledigt_am");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_fahrzeug_id",
                table: "einsatz_geraete",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_geraet_id",
                table: "einsatz_geraete",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_mangel_id",
                table: "einsatz_geraete",
                column: "mangel_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_geraete_material_id",
                table: "einsatz_geraete",
                column: "material_id");

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_einsaetze_einsatz_id",
                table: "maengel",
                column: "einsatz_id",
                principalTable: "einsaetze",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_pruefungen_erledigt_durch_pruefung_id",
                table: "maengel",
                column: "erledigt_durch_pruefung_id",
                principalTable: "pruefungen",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Rücklaufwege der bekannten Gerätearten (V13): Schläuche zur Schlauchwerkstatt, Atemschutz zur Atemschutzwerkstatt.
            migrationBuilder.Sql("UPDATE geraetearten SET ruecklauf = 'Schlauchwerkstatt' WHERE code = 'DRUCKSCHLAUCH';");
            migrationBuilder.Sql("UPDATE geraetearten SET ruecklauf = 'Atemschutzwerkstatt' WHERE code IN ('PA', 'PA-FLASCHE', 'PA-MASKE', 'CSA');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_maengel_einsaetze_einsatz_id",
                table: "maengel");

            migrationBuilder.DropForeignKey(
                name: "fk_maengel_pruefungen_erledigt_durch_pruefung_id",
                table: "maengel");

            migrationBuilder.DropTable(
                name: "einsatz_geraete");

            migrationBuilder.DropIndex(
                name: "ix_maengel_einsatz_id",
                table: "maengel");

            migrationBuilder.DropIndex(
                name: "ix_maengel_erledigt_durch_pruefung_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "storniert_am",
                table: "pruefungen");

            migrationBuilder.DropColumn(
                name: "storniert_von",
                table: "pruefungen");

            migrationBuilder.DropColumn(
                name: "stornogrund",
                table: "pruefungen");

            migrationBuilder.DropColumn(
                name: "einsatz_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "erledigt_durch_pruefung_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ruecklauf",
                table: "geraetearten");

            migrationBuilder.DropColumn(
                name: "psa_waesche",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "psa_waesche_erledigt_am",
                table: "einsatz_teilnahmen");
        }
    }
}
