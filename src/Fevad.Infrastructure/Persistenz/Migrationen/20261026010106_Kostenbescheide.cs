using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    /// <summary>Kostenersatzbescheide mit Statuskette (V22); Parameterwerte bis 2000 Zeichen für Textbausteine.</summary>
    public partial class Kostenbescheide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "wert",
                table: "parameter",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "kostenbescheide",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostenersatzvorgang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostenschuldner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kassenzeichen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    anhoerung_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    bekanntgegeben_am = table.Column<DateOnly>(type: "date", nullable: true),
                    faellig_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kostenbescheide", x => x.id);
                    table.ForeignKey(
                        name: "fk_kostenbescheide_kostenersatzvorgaenge_kostenersatzvorgang_id",
                        column: x => x.kostenersatzvorgang_id,
                        principalTable: "kostenersatzvorgaenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kostenbescheide_kostenschuldner_kostenschuldner_id",
                        column: x => x.kostenschuldner_id,
                        principalTable: "kostenschuldner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bescheid_ereignisse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kostenbescheid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schritt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    vermerk = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    bearbeiter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    erfasst_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bescheid_ereignisse", x => x.id);
                    table.ForeignKey(
                        name: "fk_bescheid_ereignisse_kostenbescheide_kostenbescheid_id",
                        column: x => x.kostenbescheid_id,
                        principalTable: "kostenbescheide",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bescheid_ereignisse_kostenbescheid_id",
                table: "bescheid_ereignisse",
                column: "kostenbescheid_id");

            migrationBuilder.CreateIndex(
                name: "ix_kostenbescheide_kostenersatzvorgang_id",
                table: "kostenbescheide",
                column: "kostenersatzvorgang_id");

            migrationBuilder.CreateIndex(
                name: "ix_kostenbescheide_kostenschuldner_id",
                table: "kostenbescheide",
                column: "kostenschuldner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bescheid_ereignisse");

            migrationBuilder.DropTable(
                name: "kostenbescheide");

            migrationBuilder.AlterColumn<string>(
                name: "wert",
                table: "parameter",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
