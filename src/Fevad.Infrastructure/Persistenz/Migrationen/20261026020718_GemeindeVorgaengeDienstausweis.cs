using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    /// <summary>Gemeinde-Vorgänge, Bescheide des Bürgermeisters an Mitglieder und Dienstausweise (V27).</summary>
    public partial class GemeindeVorgaengeDienstausweis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dienstausweise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ausgestellt_am = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: false),
                    zurueck_am = table.Column<DateOnly>(type: "date", nullable: true),
                    vermerk = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienstausweise", x => x.id);
                    table.ForeignKey(
                        name: "fk_dienstausweise_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gemeindevorgaenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    betreff = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zustaendig = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sachverhalt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    rechtsgrundlage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    beschlussvorschlag = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    vorgelegt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entscheidungsvermerk = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    eingetragen_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    zurueckgezogen = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gemeindevorgaenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_gemeindevorgaenge_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mitgliederbescheide",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empfaenger_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empfaenger_anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sachverhalt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    tenor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    begruendung = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    anhoerung_am = table.Column<DateOnly>(type: "date", nullable: true),
                    anhoerung_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    stellungnahme = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    erlassen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bekanntgegeben_am = table.Column<DateOnly>(type: "date", nullable: true),
                    eingestellt = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitgliederbescheide", x => x.id);
                    table.ForeignKey(
                        name: "fk_mitgliederbescheide_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienstausweise_mitglied_id",
                table: "dienstausweise",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienstausweise_nummer",
                table: "dienstausweise",
                column: "nummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gemeindevorgaenge_mitglied_id",
                table: "gemeindevorgaenge",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_mitgliederbescheide_mitglied_id",
                table: "mitgliederbescheide",
                column: "mitglied_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dienstausweise");

            migrationBuilder.DropTable(
                name: "gemeindevorgaenge");

            migrationBuilder.DropTable(
                name: "mitgliederbescheide");
        }
    }
}
