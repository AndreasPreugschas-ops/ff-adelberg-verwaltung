using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class NachweiseTauschVerfuegbarkeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tagesverfuegbarkeit",
                table: "mitglieder",
                type: "integer",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "tagesverfuegbarkeit_stand",
                table: "mitglieder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "quittiert_digital",
                table: "kleiderkammervorgaenge",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "nachweiseinreichungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nachweisart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    aussteller = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geprueft_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    antwort = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nachweiseinreichungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_nachweiseinreichungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nachweiseinreichungen_nachweisarten_nachweisart_id",
                        column: x => x.nachweisart_id,
                        principalTable: "nachweisarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nachweiseinreichungen_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tauschanforderungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ausstattung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gewuenschte_groesse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bearbeitet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    antwort = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tauschanforderungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_tauschanforderungen_ausstattungen_ausstattung_id",
                        column: x => x.ausstattung_id,
                        principalTable: "ausstattungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tauschanforderungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nachweiseinreichungen_mitglied_id",
                table: "nachweiseinreichungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_nachweiseinreichungen_nachweisart_id",
                table: "nachweiseinreichungen",
                column: "nachweisart_id");

            migrationBuilder.CreateIndex(
                name: "ix_nachweiseinreichungen_qualifikation_id",
                table: "nachweiseinreichungen",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_nachweiseinreichungen_status_mitglied_id",
                table: "nachweiseinreichungen",
                columns: new[] { "status", "mitglied_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tauschanforderungen_ausstattung_id",
                table: "tauschanforderungen",
                column: "ausstattung_id");

            migrationBuilder.CreateIndex(
                name: "ix_tauschanforderungen_mitglied_id",
                table: "tauschanforderungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_tauschanforderungen_status_mitglied_id",
                table: "tauschanforderungen",
                columns: new[] { "status", "mitglied_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nachweiseinreichungen");

            migrationBuilder.DropTable(
                name: "tauschanforderungen");

            migrationBuilder.DropColumn(
                name: "tagesverfuegbarkeit",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "tagesverfuegbarkeit_stand",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "quittiert_digital",
                table: "kleiderkammervorgaenge");
        }
    }
}
