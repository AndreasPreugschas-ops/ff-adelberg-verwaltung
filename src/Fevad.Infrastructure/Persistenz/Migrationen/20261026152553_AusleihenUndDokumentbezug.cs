using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AusleihenUndDokumentbezug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "geraet_id",
                table: "dokumente",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "geraeteart_id",
                table: "dokumente",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ausleihen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    richtung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    partner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis_geplant = table.Column<DateOnly>(type: "date", nullable: true),
                    zurueck_am = table.Column<DateOnly>(type: "date", nullable: true),
                    rueckgabevermerk = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ausleihen", x => x.id);
                    table.CheckConstraint("ck_ausleihen_gegenstand", "geraet_id IS NOT NULL OR bezeichnung IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_ausleihen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dokumente_geraet_id",
                table: "dokumente",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_dokumente_geraeteart_id",
                table: "dokumente",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausleihen_geraet_id",
                table: "ausleihen",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausleihen_zurueck_am",
                table: "ausleihen",
                column: "zurueck_am");

            migrationBuilder.AddForeignKey(
                name: "fk_dokumente_geraete_geraet_id",
                table: "dokumente",
                column: "geraet_id",
                principalTable: "geraete",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_dokumente_geraetearten_geraeteart_id",
                table: "dokumente",
                column: "geraeteart_id",
                principalTable: "geraetearten",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dokumente_geraete_geraet_id",
                table: "dokumente");

            migrationBuilder.DropForeignKey(
                name: "fk_dokumente_geraetearten_geraeteart_id",
                table: "dokumente");

            migrationBuilder.DropTable(
                name: "ausleihen");

            migrationBuilder.DropIndex(
                name: "ix_dokumente_geraet_id",
                table: "dokumente");

            migrationBuilder.DropIndex(
                name: "ix_dokumente_geraeteart_id",
                table: "dokumente");

            migrationBuilder.DropColumn(
                name: "geraet_id",
                table: "dokumente");

            migrationBuilder.DropColumn(
                name: "geraeteart_id",
                table: "dokumente");
        }
    }
}
