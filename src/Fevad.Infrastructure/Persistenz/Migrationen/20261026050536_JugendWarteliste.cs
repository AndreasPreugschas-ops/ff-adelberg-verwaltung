using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class JugendWarteliste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interessenten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    geburtsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ansprechpartner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    eingetragen_am = table.Column<DateOnly>(type: "date", nullable: false),
                    wiedervorlage = table.Column<DateOnly>(type: "date", nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    schnupperdienst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schnupper_zustimmung_am = table.Column<DateOnly>(type: "date", nullable: true),
                    erledigt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interessenten", x => x.id);
                    table.ForeignKey(
                        name: "fk_interessenten_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interessenten_dienste_schnupperdienst_id",
                        column: x => x.schnupperdienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_interessenten_abteilung_id",
                table: "interessenten",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_interessenten_schnupperdienst_id",
                table: "interessenten",
                column: "schnupperdienst_id");

            migrationBuilder.CreateIndex(
                name: "ix_interessenten_wiedervorlage",
                table: "interessenten",
                column: "wiedervorlage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interessenten");
        }
    }
}
