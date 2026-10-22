using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Personalverwaltung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "lehrgangsort",
                table: "qualifikation_nachweise",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bemerkung",
                table: "qualifikation_nachweise",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "erinnerungslaeufe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    eintraege = table.Column<int>(type: "integer", nullable: false),
                    empfaenger = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_erinnerungslaeufe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nachweisarten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    intervall_monate = table.Column<int>(type: "integer", nullable: false),
                    pflicht = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_nachweisarten", x => x.id);
                    table.CheckConstraint("ck_nachweisarten_intervall", "intervall_monate > 0");
                    table.ForeignKey(
                        name: "fk_nachweisarten_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nachweise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nachweisart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: false),
                    durchgefuehrt_von = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_nachweise", x => x.id);
                    table.ForeignKey(
                        name: "fk_nachweise_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nachweise_nachweisarten_nachweisart_id",
                        column: x => x.nachweisart_id,
                        principalTable: "nachweisarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_erinnerungslaeufe_zeitpunkt",
                table: "erinnerungslaeufe",
                column: "zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "ix_nachweisarten_code",
                table: "nachweisarten",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nachweisarten_qualifikation_id",
                table: "nachweisarten",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_nachweise_mitglied_id_nachweisart_id_datum",
                table: "nachweise",
                columns: new[] { "mitglied_id", "nachweisart_id", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_nachweise_nachweisart_id",
                table: "nachweise",
                column: "nachweisart_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erinnerungslaeufe");

            migrationBuilder.DropTable(
                name: "nachweise");

            migrationBuilder.DropTable(
                name: "nachweisarten");

            migrationBuilder.DropColumn(
                name: "bemerkung",
                table: "qualifikation_nachweise");

            migrationBuilder.AlterColumn<string>(
                name: "lehrgangsort",
                table: "qualifikation_nachweise",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
