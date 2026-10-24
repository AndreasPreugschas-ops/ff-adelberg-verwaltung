using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class DienstAbteilungenUndUebungsleiter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "abgeschlossen",
                table: "dienste",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "als_dienst",
                table: "dienste",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "dienst_abteilungen",
                columns: table => new
                {
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienst_abteilungen", x => new { x.dienst_id, x.abteilung_id });
                    table.ForeignKey(
                        name: "fk_dienst_abteilungen_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dienst_abteilungen_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienst_abteilungen_abteilung_id",
                table: "dienst_abteilungen",
                column: "abteilung_id");

            // Bisherige Zielabteilung in die neue Zuordnung übernehmen, bevor die Spalte wegfällt.
            migrationBuilder.Sql("""
                INSERT INTO dienst_abteilungen (dienst_id, abteilung_id)
                SELECT id, abteilung_id FROM dienste WHERE abteilung_id IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_dienste_abteilungen_abteilung_id",
                table: "dienste");

            migrationBuilder.DropIndex(
                name: "ix_dienste_abteilung_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "abteilung_id",
                table: "dienste");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "abteilung_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            // Bei mehreren Abteilungen bleibt nur eine erhalten.
            migrationBuilder.Sql("""
                UPDATE dienste d SET abteilung_id = (SELECT min(x.abteilung_id::text)::uuid FROM dienst_abteilungen x WHERE x.dienst_id = d.id);
                """);

            migrationBuilder.DropTable(
                name: "dienst_abteilungen");

            migrationBuilder.DropColumn(
                name: "abgeschlossen",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "als_dienst",
                table: "dienste");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_abteilung_id",
                table: "dienste",
                column: "abteilung_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_abteilungen_abteilung_id",
                table: "dienste",
                column: "abteilung_id",
                principalTable: "abteilungen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
