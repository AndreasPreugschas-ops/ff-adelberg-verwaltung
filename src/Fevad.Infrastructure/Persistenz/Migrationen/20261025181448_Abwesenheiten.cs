using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Abwesenheiten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "abwesenheit_id",
                table: "rueckmeldungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "abwesenheiten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gemeldet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kenntnis_genommen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kenntnis_genommen_von = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abwesenheiten", x => x.id);
                    table.ForeignKey(
                        name: "fk_abwesenheiten_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rueckmeldungen_abwesenheit_id",
                table: "rueckmeldungen",
                column: "abwesenheit_id");

            migrationBuilder.CreateIndex(
                name: "ix_abwesenheiten_mitglied_id_von_bis",
                table: "abwesenheiten",
                columns: new[] { "mitglied_id", "von", "bis" });

            migrationBuilder.AddForeignKey(
                name: "fk_rueckmeldungen_abwesenheiten_abwesenheit_id",
                table: "rueckmeldungen",
                column: "abwesenheit_id",
                principalTable: "abwesenheiten",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_rueckmeldungen_abwesenheiten_abwesenheit_id",
                table: "rueckmeldungen");

            migrationBuilder.DropTable(
                name: "abwesenheiten");

            migrationBuilder.DropIndex(
                name: "ix_rueckmeldungen_abwesenheit_id",
                table: "rueckmeldungen");

            migrationBuilder.DropColumn(
                name: "abwesenheit_id",
                table: "rueckmeldungen");
        }
    }
}
