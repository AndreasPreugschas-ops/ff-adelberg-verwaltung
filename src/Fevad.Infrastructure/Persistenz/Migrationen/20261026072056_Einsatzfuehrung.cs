using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Einsatzfuehrung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fuehrungsstufe",
                table: "einsaetze",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "einsatzabschnitte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    leiter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leiter_extern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    auftrag = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatzabschnitte", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatzabschnitte_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatzabschnitte_mitglieder_leiter_id",
                        column: x => x.leiter_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "einsatzfunktionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualifikationen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    atemschutz = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_einsatzfunktionen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "einsatzleitungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ab = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    @extern = table.Column<string>(name: "extern", type: "character varying(200)", maxLength: 200, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatzleitungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatzleitungen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatzleitungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tagebucheintraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    verfasser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verfasst_von = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tagebucheintraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_tagebucheintraege_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsatzabschnitte_einsatz_id",
                table: "einsatzabschnitte",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatzabschnitte_leiter_id",
                table: "einsatzabschnitte",
                column: "leiter_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatzfunktionen_code",
                table: "einsatzfunktionen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_einsatzleitungen_einsatz_id",
                table: "einsatzleitungen",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatzleitungen_mitglied_id",
                table: "einsatzleitungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_tagebucheintraege_einsatz_id_zeitpunkt",
                table: "tagebucheintraege",
                columns: new[] { "einsatz_id", "zeitpunkt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einsatzabschnitte");

            migrationBuilder.DropTable(
                name: "einsatzfunktionen");

            migrationBuilder.DropTable(
                name: "einsatzleitungen");

            migrationBuilder.DropTable(
                name: "tagebucheintraege");

            migrationBuilder.DropColumn(
                name: "fuehrungsstufe",
                table: "einsaetze");
        }
    }
}
