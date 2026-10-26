using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class UnterweisungUndWache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "auftraggeber",
                table: "dienste",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "auftraggeber_kontakt",
                table: "dienste",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "kostensatz_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unterweisungsthemen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pflicht = table.Column<bool>(type: "boolean", nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nachweisart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    atemschutzunterweisung = table.Column<bool>(type: "boolean", nullable: false),
                    beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_unterweisungsthemen", x => x.id);
                    table.ForeignKey(
                        name: "fk_unterweisungsthemen_nachweisarten_nachweisart_id",
                        column: x => x.nachweisart_id,
                        principalTable: "nachweisarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_unterweisungsthemen_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dienst_unterweisungsthemen",
                columns: table => new
                {
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unterweisungsthema_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienst_unterweisungsthemen", x => new { x.dienst_id, x.unterweisungsthema_id });
                    table.ForeignKey(
                        name: "fk_dienst_unterweisungsthemen_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dienst_unterweisungsthemen_unterweisungsthemen_unterweisung",
                        column: x => x.unterweisungsthema_id,
                        principalTable: "unterweisungsthemen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienste_kostensatz_id",
                table: "dienste",
                column: "kostensatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienst_unterweisungsthemen_unterweisungsthema_id",
                table: "dienst_unterweisungsthemen",
                column: "unterweisungsthema_id");

            migrationBuilder.CreateIndex(
                name: "ix_unterweisungsthemen_code",
                table: "unterweisungsthemen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unterweisungsthemen_nachweisart_id",
                table: "unterweisungsthemen",
                column: "nachweisart_id");

            migrationBuilder.CreateIndex(
                name: "ix_unterweisungsthemen_qualifikation_id",
                table: "unterweisungsthemen",
                column: "qualifikation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_kostensaetze_kostensatz_id",
                table: "dienste",
                column: "kostensatz_id",
                principalTable: "kostensaetze",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dienste_kostensaetze_kostensatz_id",
                table: "dienste");

            migrationBuilder.DropTable(
                name: "dienst_unterweisungsthemen");

            migrationBuilder.DropTable(
                name: "unterweisungsthemen");

            migrationBuilder.DropIndex(
                name: "ix_dienste_kostensatz_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "auftraggeber",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "auftraggeber_kontakt",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "kostensatz_id",
                table: "dienste");
        }
    }
}
