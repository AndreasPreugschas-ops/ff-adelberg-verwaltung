using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Dienstpflicht : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "fachberater",
                table: "mitglieder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "fachgebiet",
                table: "mitglieder",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "buergermeister_mitgeteilt_am",
                table: "dokumente",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "revision_am",
                table: "dokumente",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dienstpflichtvorgaenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: true),
                    bis = table.Column<DateOnly>(type: "date", nullable: true),
                    sachverhalt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    buergermeister_mitgeteilt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    erfasst_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienstpflichtvorgaenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_dienstpflichtvorgaenge_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienstpflichtvorgaenge_mitglied_id_datum",
                table: "dienstpflichtvorgaenge",
                columns: new[] { "mitglied_id", "datum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dienstpflichtvorgaenge");

            migrationBuilder.DropColumn(
                name: "fachberater",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "fachgebiet",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "buergermeister_mitgeteilt_am",
                table: "dokumente");

            migrationBuilder.DropColumn(
                name: "revision_am",
                table: "dokumente");
        }
    }
}
