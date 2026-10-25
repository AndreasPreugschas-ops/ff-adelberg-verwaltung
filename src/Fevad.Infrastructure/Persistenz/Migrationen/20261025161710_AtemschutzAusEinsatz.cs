using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AtemschutzAusEinsatz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "exposition_pflicht",
                table: "statistik_kategorien",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "atemschutz_aktiv",
                table: "mitglieder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "dekontaminiert",
                table: "einsatz_teilnahmen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "exposition",
                table: "einsatz_teilnahmen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gefahrstoffe",
                table: "einsatz_teilnahmen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "hautkontakt",
                table: "einsatz_teilnahmen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "maske",
                table: "einsatz_teilnahmen",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pa_geraet",
                table: "einsatz_teilnahmen",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bestaetigt_kommando_am",
                table: "atemschutz_nachweise",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bestaetigt_kommando_von",
                table: "atemschutz_nachweise",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bestaetigt_wart_am",
                table: "atemschutz_nachweise",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bestaetigt_wart_von",
                table: "atemschutz_nachweise",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "gilt_als_einsatzuebung",
                table: "atemschutz_nachweise",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "atemschutz_sperren",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    selbstmeldung = table.Column<bool>(type: "boolean", nullable: false),
                    gemeldet_von = table.Column<Guid>(type: "uuid", nullable: true),
                    gemeldet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    genehmigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    genehmigt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    aufgehoben_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aufgehoben_von = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atemschutz_sperren", x => x.id);
                    table.ForeignKey(
                        name: "fk_atemschutz_sperren_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gefahrstoffgruppen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_gefahrstoffgruppen", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atemschutz_nachweise_einsatz_id",
                table: "atemschutz_nachweise",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_atemschutz_sperren_mitglied_id_bis",
                table: "atemschutz_sperren",
                columns: new[] { "mitglied_id", "bis" });

            migrationBuilder.CreateIndex(
                name: "ix_gefahrstoffgruppen_code",
                table: "gefahrstoffgruppen",
                column: "code",
                unique: true);

            // Bisher überwachte jeden mit Lehrgang Atemschutz; diese bleiben gekennzeichnet.
            migrationBuilder.Sql("""
                UPDATE mitglieder m SET atemschutz_aktiv = TRUE
                WHERE EXISTS (SELECT 1 FROM qualifikation_nachweise q JOIN qualifikationen k ON k.id = q.qualifikation_id
                              WHERE q.mitglied_id = m.id AND k.code = 'AGT' AND q.geloescht_am IS NULL);
                """);

            // Vorhandene Nachweise gelten als bestätigt (Altbestand, ohne Person).
            migrationBuilder.Sql("UPDATE atemschutz_nachweise SET bestaetigt_kommando_am = now(), bestaetigt_wart_am = now();");

            migrationBuilder.Sql("UPDATE statistik_kategorien SET exposition_pflicht = TRUE WHERE code = 'BR';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atemschutz_sperren");

            migrationBuilder.DropTable(
                name: "gefahrstoffgruppen");

            migrationBuilder.DropIndex(
                name: "ix_atemschutz_nachweise_einsatz_id",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "exposition_pflicht",
                table: "statistik_kategorien");

            migrationBuilder.DropColumn(
                name: "atemschutz_aktiv",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "dekontaminiert",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "exposition",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "gefahrstoffe",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "hautkontakt",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "maske",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "pa_geraet",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "bestaetigt_kommando_am",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "bestaetigt_kommando_von",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "bestaetigt_wart_am",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "bestaetigt_wart_von",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "gilt_als_einsatzuebung",
                table: "atemschutz_nachweise");
        }
    }
}
