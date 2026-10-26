using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Archivangebote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archivangebote",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    datensatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    faellig_am = table.Column<DateOnly>(type: "date", nullable: false),
                    angeboten_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entscheidung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    uebergeben_am = table.Column<DateOnly>(type: "date", nullable: true),
                    pruefsumme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    erledigt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archivangebote", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivangebote_art_datensatz_id",
                table: "archivangebote",
                columns: new[] { "art", "datensatz_id" });

            migrationBuilder.CreateIndex(
                name: "ix_archivangebote_erledigt_am",
                table: "archivangebote",
                column: "erledigt_am");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivangebote");
        }
    }
}
