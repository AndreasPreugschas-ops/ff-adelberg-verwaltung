using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Rundfragen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "nur_volljaehrige",
                table: "einwilligungsarten",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "rundfragen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ziel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    abteilungen = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    jugendveranstaltung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tag = table.Column<DateOnly>(type: "date", nullable: true),
                    optionen = table.Column<List<string>>(type: "text[]", nullable: false),
                    mehrfachauswahl = table.Column<bool>(type: "boolean", nullable: false),
                    anonym = table.Column<bool>(type: "boolean", nullable: false),
                    antwort_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    verschickt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    beendet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verfasser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rundfragen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rundfrage_empfaenger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rundfrage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beantwortet_am = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rundfrage_empfaenger", x => x.id);
                    table.ForeignKey(
                        name: "fk_rundfrage_empfaenger_asp_net_users_konto_id",
                        column: x => x.konto_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rundfrage_empfaenger_rundfragen_rundfrage_id",
                        column: x => x.rundfrage_id,
                        principalTable: "rundfragen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rundfrage_antworten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rundfrage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empfaenger_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auswahl = table.Column<List<int>>(type: "integer[]", nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rundfrage_antworten", x => x.id);
                    table.ForeignKey(
                        name: "fk_rundfrage_antworten_rundfrage_empfaenger_empfaenger_id",
                        column: x => x.empfaenger_id,
                        principalTable: "rundfrage_empfaenger",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rundfrage_antworten_rundfragen_rundfrage_id",
                        column: x => x.rundfrage_id,
                        principalTable: "rundfragen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rundfrage_antworten_empfaenger_id",
                table: "rundfrage_antworten",
                column: "empfaenger_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rundfrage_antworten_rundfrage_id",
                table: "rundfrage_antworten",
                column: "rundfrage_id");

            migrationBuilder.CreateIndex(
                name: "ix_rundfrage_empfaenger_konto_id",
                table: "rundfrage_empfaenger",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_rundfrage_empfaenger_rundfrage_id_konto_id",
                table: "rundfrage_empfaenger",
                columns: new[] { "rundfrage_id", "konto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rundfragen_status_verschickt_am",
                table: "rundfragen",
                columns: new[] { "status", "verschickt_am" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rundfrage_antworten");

            migrationBuilder.DropTable(
                name: "rundfrage_empfaenger");

            migrationBuilder.DropTable(
                name: "rundfragen");

            migrationBuilder.DropColumn(
                name: "nur_volljaehrige",
                table: "einwilligungsarten");
        }
    }
}
