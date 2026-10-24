using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class FruehereAnschriften : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fruehere_anschriften",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    arbeitgeber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bis_datum = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fruehere_anschriften", x => x.id);
                    table.ForeignKey(
                        name: "fk_fruehere_anschriften_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fruehere_anschriften_mitglied_id_art_bis_datum",
                table: "fruehere_anschriften",
                columns: new[] { "mitglied_id", "art", "bis_datum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fruehere_anschriften");
        }
    }
}
