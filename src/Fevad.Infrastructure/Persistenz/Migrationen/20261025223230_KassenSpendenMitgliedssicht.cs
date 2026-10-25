using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Spenden- und Sponsoringregister (V08) und Mitgliedssicht der Kassen (V09).</summary>
    public partial class KassenSpendenMitgliedssicht : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "berichte_fuer_mitglieder",
                table: "kassen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "zuwendungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eingang = table.Column<DateOnly>(type: "date", nullable: false),
                    geber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anschrift = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    sache = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    zweck = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    zweckgebunden = table.Column<bool>(type: "boolean", nullable: false),
                    sponsoring = table.Column<bool>(type: "boolean", nullable: false),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annahme_am = table.Column<DateOnly>(type: "date", nullable: true),
                    annahme_verweis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dankschreiben_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_zuwendungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_zuwendungen_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_zuwendungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_zuwendungen_buchung_id",
                table: "zuwendungen",
                column: "buchung_id");

            migrationBuilder.CreateIndex(
                name: "ix_zuwendungen_kasse_id_eingang",
                table: "zuwendungen",
                columns: new[] { "kasse_id", "eingang" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zuwendungen");

            migrationBuilder.DropColumn(
                name: "berichte_fuer_mitglieder",
                table: "kassen");
        }
    }
}
