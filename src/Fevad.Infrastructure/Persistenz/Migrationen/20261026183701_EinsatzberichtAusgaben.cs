using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinsatzberichtAusgaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "einsatzbericht_empfaenger",
                table: "organisationen",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "presse_freigegeben_am",
                table: "einsaetze",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "presse_freigegeben_von",
                table: "einsaetze",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pressetext",
                table: "einsaetze",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "einsatzbericht_versaende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empfaenger = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    mit_teilnehmern = table.Column<bool>(type: "boolean", nullable: false),
                    mit_beteiligten = table.Column<bool>(type: "boolean", nullable: false),
                    versandt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versandt_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatzbericht_versaende", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatzbericht_versaende_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsatzbericht_versaende_einsatz_id",
                table: "einsatzbericht_versaende",
                column: "einsatz_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einsatzbericht_versaende");

            migrationBuilder.DropColumn(
                name: "einsatzbericht_empfaenger",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "presse_freigegeben_am",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "presse_freigegeben_von",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "pressetext",
                table: "einsaetze");
        }
    }
}
