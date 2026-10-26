using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class FuehrungUndNachbesprechung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "psnv_belastend",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "psnv_nachbesprechung",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "psnv_nachsorge_angeboten",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "rueckgabe",
                table: "einsaetze",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "vorgelegt_am",
                table: "einsaetze",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vorgelegt_von",
                table: "einsaetze",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nachbesprechungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    teilnehmer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gut = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    verbesserung = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nachbesprechungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_nachbesprechungen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "massnahmen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nachbesprechung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    verantwortlich_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erledigen_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    freigegeben_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    freigegeben_von = table.Column<Guid>(type: "uuid", nullable: true),
                    erledigt_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_massnahmen", x => x.id);
                    table.ForeignKey(
                        name: "fk_massnahmen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_massnahmen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_massnahmen_mitglieder_verantwortlich_id",
                        column: x => x.verantwortlich_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_massnahmen_nachbesprechungen_nachbesprechung_id",
                        column: x => x.nachbesprechung_id,
                        principalTable: "nachbesprechungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_massnahmen_fahrzeug_id",
                table: "massnahmen",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_massnahmen_geraet_id",
                table: "massnahmen",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_massnahmen_nachbesprechung_id",
                table: "massnahmen",
                column: "nachbesprechung_id");

            migrationBuilder.CreateIndex(
                name: "ix_massnahmen_status",
                table: "massnahmen",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_massnahmen_verantwortlich_id",
                table: "massnahmen",
                column: "verantwortlich_id");

            migrationBuilder.CreateIndex(
                name: "ix_nachbesprechungen_einsatz_id",
                table: "nachbesprechungen",
                column: "einsatz_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "massnahmen");

            migrationBuilder.DropTable(
                name: "nachbesprechungen");

            migrationBuilder.DropColumn(
                name: "psnv_belastend",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "psnv_nachbesprechung",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "psnv_nachsorge_angeboten",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "rueckgabe",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "vorgelegt_am",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "vorgelegt_von",
                table: "einsaetze");
        }
    }
}
