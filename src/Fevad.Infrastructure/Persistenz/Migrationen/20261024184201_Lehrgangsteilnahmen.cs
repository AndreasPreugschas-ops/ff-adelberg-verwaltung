using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Lehrgangsteilnahmen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "gueltig_bis",
                table: "nachweise",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<bool>(
                name: "einmalig",
                table: "nachweisarten",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "lehrgangsteilnahmen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    angemeldet_am = table.Column<DateOnly>(type: "date", nullable: true),
                    zugewiesen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    von = table.Column<DateOnly>(type: "date", nullable: true),
                    bis = table.Column<DateOnly>(type: "date", nullable: true),
                    bestanden = table.Column<bool>(type: "boolean", nullable: false),
                    abgeschlossen = table.Column<bool>(type: "boolean", nullable: false),
                    lehrgangsort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nachweis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lehrgangsteilnahmen", x => x.id);
                    table.ForeignKey(
                        name: "fk_lehrgangsteilnahmen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lehrgangsteilnahmen_qualifikation_nachweise_nachweis_id",
                        column: x => x.nachweis_id,
                        principalTable: "qualifikation_nachweise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lehrgangsteilnahmen_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangsteilnahmen_mitglied_id_abgeschlossen",
                table: "lehrgangsteilnahmen",
                columns: new[] { "mitglied_id", "abgeschlossen" });

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangsteilnahmen_nachweis_id",
                table: "lehrgangsteilnahmen",
                column: "nachweis_id");

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangsteilnahmen_qualifikation_id",
                table: "lehrgangsteilnahmen",
                column: "qualifikation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lehrgangsteilnahmen");

            migrationBuilder.DropColumn(
                name: "einmalig",
                table: "nachweisarten");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "gueltig_bis",
                table: "nachweise",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
