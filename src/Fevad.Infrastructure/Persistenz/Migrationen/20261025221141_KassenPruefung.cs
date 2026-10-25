using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Kassenprüfungen mit zwei Prüfern und Beanstandungen (V05).</summary>
    public partial class KassenPruefung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kassenpruefungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    geprueft_am = table.Column<DateOnly>(type: "date", nullable: true),
                    pruefer1id = table.Column<Guid>(type: "uuid", nullable: false),
                    pruefer1bestaetigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pruefer2id = table.Column<Guid>(type: "uuid", nullable: false),
                    pruefer2bestaetigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ergebnis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    entlastung_empfohlen = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kassenpruefungen", x => x.id);
                    table.CheckConstraint("ck_kassenpruefungen_pruefer", "pruefer1id <> pruefer2id");
                    table.CheckConstraint("ck_kassenpruefungen_zeitraum", "bis >= von");
                    table.ForeignKey(
                        name: "fk_kassenpruefungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kassenpruefungen_mitglieder_pruefer1id",
                        column: x => x.pruefer1id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kassenpruefungen_mitglieder_pruefer2id",
                        column: x => x.pruefer2id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "beanstandungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kassenpruefung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    antwort = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    beantwortet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    erledigt = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beanstandungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_beanstandungen_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_beanstandungen_kassenpruefungen_kassenpruefung_id",
                        column: x => x.kassenpruefung_id,
                        principalTable: "kassenpruefungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beanstandungen_buchung_id",
                table: "beanstandungen",
                column: "buchung_id");

            migrationBuilder.CreateIndex(
                name: "ix_beanstandungen_kassenpruefung_id",
                table: "beanstandungen",
                column: "kassenpruefung_id");

            migrationBuilder.CreateIndex(
                name: "ix_kassenpruefungen_kasse_id_bis",
                table: "kassenpruefungen",
                columns: new[] { "kasse_id", "bis" });

            migrationBuilder.CreateIndex(
                name: "ix_kassenpruefungen_pruefer1id",
                table: "kassenpruefungen",
                column: "pruefer1id");

            migrationBuilder.CreateIndex(
                name: "ix_kassenpruefungen_pruefer2id",
                table: "kassenpruefungen",
                column: "pruefer2id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beanstandungen");

            migrationBuilder.DropTable(
                name: "kassenpruefungen");
        }
    }
}
