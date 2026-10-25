using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Unfallanzeige : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unfaelle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anlass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    taetigkeit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    hergang = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    verletzte_koerperteile = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    art_der_verletzung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    toedlich = table.Column<bool>(type: "boolean", nullable: false),
                    erstbehandlung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    krankenkasse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dienst_eingestellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mehr_als_drei_tage_arbeitsunfaehig = table.Column<bool>(type: "boolean", nullable: false),
                    zuerst_kenntnis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gemeldet_am = table.Column<DateOnly>(type: "date", nullable: true),
                    aktenzeichen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    abgeschlossen_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_unfaelle", x => x.id);
                    table.ForeignKey(
                        name: "fk_unfaelle_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_unfaelle_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_unfaelle_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unfall_zeugen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unfall_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kontakt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    augenzeuge = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unfall_zeugen", x => x.id);
                    table.ForeignKey(
                        name: "fk_unfall_zeugen_unfaelle_unfall_id",
                        column: x => x.unfall_id,
                        principalTable: "unfaelle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unfaelle_dienst_id",
                table: "unfaelle",
                column: "dienst_id");

            migrationBuilder.CreateIndex(
                name: "ix_unfaelle_einsatz_id",
                table: "unfaelle",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_unfaelle_mitglied_id",
                table: "unfaelle",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_unfaelle_zeitpunkt",
                table: "unfaelle",
                column: "zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "ix_unfall_zeugen_unfall_id",
                table: "unfall_zeugen",
                column: "unfall_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unfall_zeugen");

            migrationBuilder.DropTable(
                name: "unfaelle");
        }
    }
}
