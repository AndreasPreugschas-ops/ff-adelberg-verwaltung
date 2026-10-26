using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Mitteilungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mitteilungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ziel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    abteilungen = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    jugendveranstaltung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lesebestaetigung = table.Column<bool>(type: "boolean", nullable: false),
                    sofort_per_mail = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    veroeffentlicht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verfasser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    aktuell_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitteilungen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mitteilung_empfaenger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitteilung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gelesen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bestaetigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitteilung_empfaenger", x => x.id);
                    table.ForeignKey(
                        name: "fk_mitteilung_empfaenger_asp_net_users_konto_id",
                        column: x => x.konto_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mitteilung_empfaenger_mitteilungen_mitteilung_id",
                        column: x => x.mitteilung_id,
                        principalTable: "mitteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mitteilung_empfaenger_konto_id",
                table: "mitteilung_empfaenger",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_mitteilung_empfaenger_mitteilung_id_konto_id",
                table: "mitteilung_empfaenger",
                columns: new[] { "mitteilung_id", "konto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mitteilungen_status_veroeffentlicht_am",
                table: "mitteilungen",
                columns: new[] { "status", "veroeffentlicht_am" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mitteilung_empfaenger");

            migrationBuilder.DropTable(
                name: "mitteilungen");
        }
    }
}
