using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Altdatenimport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "importlaeufe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ziel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    zuordnung = table.Column<string>(type: "text", nullable: false),
                    zeilen = table.Column<int>(type: "integer", nullable: false),
                    angelegt = table.Column<int>(type: "integer", nullable: false),
                    aktualisiert = table.Column<int>(type: "integer", nullable: false),
                    fehlerhaft = table.Column<int>(type: "integer", nullable: false),
                    meldungen = table.Column<string>(type: "text", nullable: false),
                    probelauf_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    freigabe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uebernommen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uebernommen_von = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importlaeufe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "importvorlagen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ziel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    zuordnung = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importvorlagen", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_importlaeufe_status",
                table: "importlaeufe",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_importvorlagen_ziel_name",
                table: "importvorlagen",
                columns: new[] { "ziel", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "importlaeufe");

            migrationBuilder.DropTable(
                name: "importvorlagen");
        }
    }
}
