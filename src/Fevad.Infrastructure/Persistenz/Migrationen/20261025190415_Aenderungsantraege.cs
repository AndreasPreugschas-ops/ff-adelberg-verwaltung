using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Aenderungsantraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "notfallkontakt_name",
                table: "mitglieder",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notfallkontakt_telefon",
                table: "mitglieder",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aenderungsantraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    entschieden_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entschieden_von = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ablehnungsgrund = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aenderungsantraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_aenderungsantraege_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aenderungsfelder",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aenderungsantrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feld = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aenderungsfelder", x => x.id);
                    table.ForeignKey(
                        name: "fk_aenderungsfelder_aenderungsantraege_aenderungsantrag_id",
                        column: x => x.aenderungsantrag_id,
                        principalTable: "aenderungsantraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aenderungsantraege_mitglied_id_status",
                table: "aenderungsantraege",
                columns: new[] { "mitglied_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_aenderungsfelder_aenderungsantrag_id",
                table: "aenderungsfelder",
                column: "aenderungsantrag_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aenderungsfelder");

            migrationBuilder.DropTable(
                name: "aenderungsantraege");

            migrationBuilder.DropColumn(
                name: "notfallkontakt_name",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "notfallkontakt_telefon",
                table: "mitglieder");
        }
    }
}
