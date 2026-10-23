using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Erziehungsberechtigte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "erziehungsberechtigte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<int>(type: "integer", nullable: false),
                    verhaeltnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_erziehungsberechtigte", x => x.id);
                    table.ForeignKey(
                        name: "fk_erziehungsberechtigte_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_erziehungsberechtigte_mitglied_id_nummer",
                table: "erziehungsberechtigte",
                columns: new[] { "mitglied_id", "nummer" },
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erziehungsberechtigte");
        }
    }
}
