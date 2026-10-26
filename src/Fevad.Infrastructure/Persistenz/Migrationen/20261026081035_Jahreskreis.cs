using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Jahreskreis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fuehrungspflichten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turnus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    monat = table.Column<int>(type: "integer", nullable: false),
                    tag = table.Column<int>(type: "integer", nullable: false),
                    intervall_jahre = table.Column<int>(type: "integer", nullable: false),
                    bezugsjahr = table.Column<int>(type: "integer", nullable: true),
                    vorlauf_tage = table.Column<int>(type: "integer", nullable: false),
                    nachlauf_tage = table.Column<int>(type: "integer", nullable: false),
                    verantwortlich = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vorlage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rechtsgrundlage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    hinweis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_fuehrungspflichten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pflicht_erledigungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pflicht_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stichtag = table.Column<DateOnly>(type: "date", nullable: false),
                    erledigt_am = table.Column<DateOnly>(type: "date", nullable: false),
                    erledigt_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pflicht_erledigungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_pflicht_erledigungen_fuehrungspflichten_pflicht_id",
                        column: x => x.pflicht_id,
                        principalTable: "fuehrungspflichten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fuehrungspflichten_code",
                table: "fuehrungspflichten",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pflicht_erledigungen_pflicht_id_stichtag",
                table: "pflicht_erledigungen",
                columns: new[] { "pflicht_id", "stichtag" },
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pflicht_erledigungen");

            migrationBuilder.DropTable(
                name: "fuehrungspflichten");
        }
    }
}
