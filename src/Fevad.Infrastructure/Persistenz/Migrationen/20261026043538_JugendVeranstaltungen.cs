using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class JugendVeranstaltungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zuschussgeber",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    satz_je_teilnehmertag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    mindestalter_jahre = table.Column<int>(type: "integer", nullable: true),
                    hoechstalter_jahre = table.Column<int>(type: "integer", nullable: true),
                    mindest_tage = table.Column<int>(type: "integer", nullable: true),
                    mindest_teilnehmer = table.Column<int>(type: "integer", nullable: true),
                    betreuer_zaehlen = table.Column<bool>(type: "boolean", nullable: false),
                    antragsfrist_tage = table.Column<int>(type: "integer", nullable: true),
                    aufbewahrung_monate = table.Column<int>(type: "integer", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zuschussgeber", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jugendveranstaltungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uebernachtung = table.Column<bool>(type: "boolean", nullable: false),
                    anmeldung_noetig = table.Column<bool>(type: "boolean", nullable: false),
                    anmeldung_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    einwilligung_noetig = table.Column<bool>(type: "boolean", nullable: false),
                    teilnehmerbeitrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    zuschussgeber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zuschuss_beantragt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    zuschuss_betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jugendveranstaltungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_jugendveranstaltungen_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_jugendveranstaltungen_zuschussgeber_zuschussgeber_id",
                        column: x => x.zuschussgeber_id,
                        principalTable: "zuschussgeber",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "veranstaltungsanmeldungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jugendveranstaltung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weg = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    angemeldet_am = table.Column<DateOnly>(type: "date", nullable: false),
                    angemeldet_von = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    einwilligung = table.Column<bool>(type: "boolean", nullable: false),
                    tage = table.Column<int>(type: "integer", nullable: true),
                    bezahlt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_veranstaltungsanmeldungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_veranstaltungsanmeldungen_jugendveranstaltungen_jugendveran",
                        column: x => x.jugendveranstaltung_id,
                        principalTable: "jugendveranstaltungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_veranstaltungsanmeldungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "veranstaltungsbetreuer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jugendveranstaltung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jugendbetreuer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tage = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_veranstaltungsbetreuer", x => x.id);
                    table.ForeignKey(
                        name: "fk_veranstaltungsbetreuer_jugendbetreuer_jugendbetreuer_id",
                        column: x => x.jugendbetreuer_id,
                        principalTable: "jugendbetreuer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_veranstaltungsbetreuer_jugendveranstaltungen_jugendveransta",
                        column: x => x.jugendveranstaltung_id,
                        principalTable: "jugendveranstaltungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jugendveranstaltungen_dienst_id",
                table: "jugendveranstaltungen",
                column: "dienst_id",
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_jugendveranstaltungen_zuschussgeber_id",
                table: "jugendveranstaltungen",
                column: "zuschussgeber_id");

            migrationBuilder.CreateIndex(
                name: "ix_veranstaltungsanmeldungen_jugendveranstaltung_id_mitglied_id",
                table: "veranstaltungsanmeldungen",
                columns: new[] { "jugendveranstaltung_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_veranstaltungsanmeldungen_mitglied_id",
                table: "veranstaltungsanmeldungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_veranstaltungsbetreuer_jugendbetreuer_id",
                table: "veranstaltungsbetreuer",
                column: "jugendbetreuer_id");

            migrationBuilder.CreateIndex(
                name: "ix_veranstaltungsbetreuer_jugendveranstaltung_id_jugendbetreue",
                table: "veranstaltungsbetreuer",
                columns: new[] { "jugendveranstaltung_id", "jugendbetreuer_id" },
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "veranstaltungsanmeldungen");

            migrationBuilder.DropTable(
                name: "veranstaltungsbetreuer");

            migrationBuilder.DropTable(
                name: "jugendveranstaltungen");

            migrationBuilder.DropTable(
                name: "zuschussgeber");
        }
    }
}
