using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Laufbahn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "befoerderungswege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dienstgrad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mindestjahre_im_dienstgrad = table.Column<int>(type: "integer", nullable: false),
                    alle_qualifikationen = table.Column<List<string>>(type: "text[]", nullable: false),
                    eine_der_qualifikationen = table.Column<List<string>>(type: "text[]", nullable: false),
                    probezeit_beendet = table.Column<bool>(type: "boolean", nullable: false),
                    ueberspringen_erlaubt = table.Column<bool>(type: "boolean", nullable: false),
                    nur_von_hand = table.Column<bool>(type: "boolean", nullable: false),
                    hinweis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_befoerderungswege", x => x.id);
                    table.ForeignKey(
                        name: "fk_befoerderungswege_dienstgrade_dienstgrad_id",
                        column: x => x.dienstgrad_id,
                        principalTable: "dienstgrade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ehrungsarten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grundlage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    jahre = table.Column<int>(type: "integer", nullable: true),
                    reihe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    verleiher = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vorlauf_monate = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_ehrungsarten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ehrungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ehrungsart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    voraussetzung_am = table.Column<DateOnly>(type: "date", nullable: true),
                    beantragt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    verliehen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    urkundennummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_ehrungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_ehrungen_ehrungsarten_ehrungsart_id",
                        column: x => x.ehrungsart_id,
                        principalTable: "ehrungsarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ehrungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_befoerderungswege_dienstgrad_id",
                table: "befoerderungswege",
                column: "dienstgrad_id");

            migrationBuilder.CreateIndex(
                name: "ix_ehrungen_ehrungsart_id",
                table: "ehrungen",
                column: "ehrungsart_id");

            migrationBuilder.CreateIndex(
                name: "ix_ehrungen_mitglied_id_ehrungsart_id",
                table: "ehrungen",
                columns: new[] { "mitglied_id", "ehrungsart_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ehrungen_verliehen_am",
                table: "ehrungen",
                column: "verliehen_am");

            migrationBuilder.CreateIndex(
                name: "ix_ehrungsarten_code",
                table: "ehrungsarten",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "befoerderungswege");

            migrationBuilder.DropTable(
                name: "ehrungen");

            migrationBuilder.DropTable(
                name: "ehrungsarten");
        }
    }
}
