using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Mangelerweiterung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ersatz_geraet_id",
                table: "maengel",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ersatz_vorher_fahrzeug_id",
                table: "maengel",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ersatz_vorher_lagerort",
                table: "maengel",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "garantie_bis",
                table: "maengel",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "garantiefall",
                table: "maengel",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "kommandant_informiert_am",
                table: "maengel",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "kosten",
                table: "maengel",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ursprung_fahrzeug_id",
                table: "maengel",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ursprung_lagerort",
                table: "maengel",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "werkstatt",
                table: "maengel",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Offen");

            migrationBuilder.AddColumn<string>(
                name: "werkstattname",
                table: "maengel",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "wieder_verladen_am",
                table: "maengel",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mangelfotos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mangel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mangelfotos", x => x.id);
                    table.ForeignKey(
                        name: "fk_mangelfotos_maengel_mangel_id",
                        column: x => x.mangel_id,
                        principalTable: "maengel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_maengel_ersatz_geraet_id",
                table: "maengel",
                column: "ersatz_geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_ersatz_vorher_fahrzeug_id",
                table: "maengel",
                column: "ersatz_vorher_fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_ursprung_fahrzeug_id",
                table: "maengel",
                column: "ursprung_fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_mangelfotos_mangel_id",
                table: "mangelfotos",
                column: "mangel_id");

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_fahrzeuge_ersatz_vorher_fahrzeug_id",
                table: "maengel",
                column: "ersatz_vorher_fahrzeug_id",
                principalTable: "fahrzeuge",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_fahrzeuge_ursprung_fahrzeug_id",
                table: "maengel",
                column: "ursprung_fahrzeug_id",
                principalTable: "fahrzeuge",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_maengel_geraete_ersatz_geraet_id",
                table: "maengel",
                column: "ersatz_geraet_id",
                principalTable: "geraete",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_maengel_fahrzeuge_ersatz_vorher_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropForeignKey(
                name: "fk_maengel_fahrzeuge_ursprung_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropForeignKey(
                name: "fk_maengel_geraete_ersatz_geraet_id",
                table: "maengel");

            migrationBuilder.DropTable(
                name: "mangelfotos");

            migrationBuilder.DropIndex(
                name: "ix_maengel_ersatz_geraet_id",
                table: "maengel");

            migrationBuilder.DropIndex(
                name: "ix_maengel_ersatz_vorher_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropIndex(
                name: "ix_maengel_ursprung_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ersatz_geraet_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ersatz_vorher_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ersatz_vorher_lagerort",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "garantie_bis",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "garantiefall",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "kommandant_informiert_am",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "kosten",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ursprung_fahrzeug_id",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "ursprung_lagerort",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "werkstatt",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "werkstattname",
                table: "maengel");

            migrationBuilder.DropColumn(
                name: "wieder_verladen_am",
                table: "maengel");
        }
    }
}
