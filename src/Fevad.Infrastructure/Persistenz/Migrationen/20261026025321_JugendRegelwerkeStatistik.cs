using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class JugendRegelwerkeStatistik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "abzeichenregelwerk_id",
                table: "qualifikation_nachweise",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "djf_ausweis_am",
                table: "mitglieder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "djf_ausweisnummer",
                table: "mitglieder",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "abzeichenregelwerke",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geltungsbereich = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    standard = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kurzbezeichnung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abzeichenregelwerke", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "abzeichenregelwerk_stufen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regelwerk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mindestalter_jahre = table.Column<int>(type: "integer", nullable: true),
                    voraussetzungen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    nachweis_hoechstens_monate = table.Column<int>(type: "integer", nullable: true),
                    hinweis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abzeichenregelwerk_stufen", x => x.id);
                    table.ForeignKey(
                        name: "fk_abzeichenregelwerk_stufen_abzeichenregelwerke_regelwerk_id",
                        column: x => x.regelwerk_id,
                        principalTable: "abzeichenregelwerke",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_abzeichenregelwerk_stufen_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_qualifikation_nachweise_abzeichenregelwerk_id",
                table: "qualifikation_nachweise",
                column: "abzeichenregelwerk_id");

            migrationBuilder.CreateIndex(
                name: "ix_abzeichenregelwerk_stufen_qualifikation_id",
                table: "abzeichenregelwerk_stufen",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_abzeichenregelwerk_stufen_regelwerk_id_qualifikation_id",
                table: "abzeichenregelwerk_stufen",
                columns: new[] { "regelwerk_id", "qualifikation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_abzeichenregelwerke_code",
                table: "abzeichenregelwerke",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_qualifikation_nachweise_abzeichenregelwerke_abzeichenregelw",
                table: "qualifikation_nachweise",
                column: "abzeichenregelwerk_id",
                principalTable: "abzeichenregelwerke",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_qualifikation_nachweise_abzeichenregelwerke_abzeichenregelw",
                table: "qualifikation_nachweise");

            migrationBuilder.DropTable(
                name: "abzeichenregelwerk_stufen");

            migrationBuilder.DropTable(
                name: "abzeichenregelwerke");

            migrationBuilder.DropIndex(
                name: "ix_qualifikation_nachweise_abzeichenregelwerk_id",
                table: "qualifikation_nachweise");

            migrationBuilder.DropColumn(
                name: "abzeichenregelwerk_id",
                table: "qualifikation_nachweise");

            migrationBuilder.DropColumn(
                name: "djf_ausweis_am",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "djf_ausweisnummer",
                table: "mitglieder");
        }
    }
}
