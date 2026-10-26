using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class JugendBetreuerFuehrungszeugnis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jugendbetreuer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    feuerwehrangehoeriger = table.Column<bool>(type: "boolean", nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aufgabe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: true),
                    zeugnis_noetig = table.Column<bool>(type: "boolean", nullable: false),
                    zeugnis_nicht_noetig_grund = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    zeugnis_vom = table.Column<DateOnly>(type: "date", nullable: true),
                    eingesehen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    eingesehen_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ohne_eintrag = table.Column<bool>(type: "boolean", nullable: true),
                    juleica_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    erste_hilfe_am = table.Column<DateOnly>(type: "date", nullable: true),
                    verhaltenskodex_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jugendbetreuer", x => x.id);
                    table.ForeignKey(
                        name: "fk_jugendbetreuer_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jugendbetreuer_abteilung_id",
                table: "jugendbetreuer",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_jugendbetreuer_bis",
                table: "jugendbetreuer",
                column: "bis");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jugendbetreuer");
        }
    }
}
