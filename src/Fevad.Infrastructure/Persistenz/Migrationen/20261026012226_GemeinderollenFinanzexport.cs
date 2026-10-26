using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    /// <summary>Verpflichtung auf Vertraulichkeit am Konto (V26) und Spaltenzuordnungen des Finanzexports (V23).</summary>
    public partial class GemeinderollenFinanzexport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "verpflichtet_am",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exportformate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    trennzeichen = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    kodierung = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kopfzeile = table.Column<bool>(type: "boolean", nullable: false),
                    dezimal_komma = table.Column<bool>(type: "boolean", nullable: false),
                    datumsformat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    spalten = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    buchungsstelle = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sachkonto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exportformate", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exportformate_art",
                table: "exportformate",
                column: "art",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exportformate");

            migrationBuilder.DropColumn(
                name: "verpflichtet_am",
                table: "AspNetUsers");
        }
    }
}
