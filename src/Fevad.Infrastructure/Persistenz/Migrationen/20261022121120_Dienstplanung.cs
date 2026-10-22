using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Dienstplanung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "gruppe_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "qualifikation_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "rueckmeldung_erbeten",
                table: "dienste",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "serie_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kalender_token",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dienst_serien",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rhythmus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    erster_tag = table.Column<DateOnly>(type: "date", nullable: false),
                    letzter_tag = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienst_serien", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gruppen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_gruppen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rueckmeldungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    antwort = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    begruendung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gemeldet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rueckmeldungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_rueckmeldungen_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rueckmeldungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gruppen_mitgliedschaften",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gruppe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gruppen_mitgliedschaften", x => x.id);
                    table.ForeignKey(
                        name: "fk_gruppen_mitgliedschaften_gruppen_gruppe_id",
                        column: x => x.gruppe_id,
                        principalTable: "gruppen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_gruppen_mitgliedschaften_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienste_gruppe_id",
                table: "dienste",
                column: "gruppe_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_qualifikation_id",
                table: "dienste",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_serie_id",
                table: "dienste",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_kalender_token",
                table: "AspNetUsers",
                column: "kalender_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gruppen_code",
                table: "gruppen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gruppen_mitgliedschaften_gruppe_id_mitglied_id",
                table: "gruppen_mitgliedschaften",
                columns: new[] { "gruppe_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_gruppen_mitgliedschaften_mitglied_id",
                table: "gruppen_mitgliedschaften",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_rueckmeldungen_dienst_id_mitglied_id",
                table: "rueckmeldungen",
                columns: new[] { "dienst_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_rueckmeldungen_mitglied_id",
                table: "rueckmeldungen",
                column: "mitglied_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_dienst_serien_serie_id",
                table: "dienste",
                column: "serie_id",
                principalTable: "dienst_serien",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_gruppen_gruppe_id",
                table: "dienste",
                column: "gruppe_id",
                principalTable: "gruppen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_qualifikationen_qualifikation_id",
                table: "dienste",
                column: "qualifikation_id",
                principalTable: "qualifikationen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dienste_dienst_serien_serie_id",
                table: "dienste");

            migrationBuilder.DropForeignKey(
                name: "fk_dienste_gruppen_gruppe_id",
                table: "dienste");

            migrationBuilder.DropForeignKey(
                name: "fk_dienste_qualifikationen_qualifikation_id",
                table: "dienste");

            migrationBuilder.DropTable(
                name: "dienst_serien");

            migrationBuilder.DropTable(
                name: "gruppen_mitgliedschaften");

            migrationBuilder.DropTable(
                name: "rueckmeldungen");

            migrationBuilder.DropTable(
                name: "gruppen");

            migrationBuilder.DropIndex(
                name: "ix_dienste_gruppe_id",
                table: "dienste");

            migrationBuilder.DropIndex(
                name: "ix_dienste_qualifikation_id",
                table: "dienste");

            migrationBuilder.DropIndex(
                name: "ix_dienste_serie_id",
                table: "dienste");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_kalender_token",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "gruppe_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "qualifikation_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "rueckmeldung_erbeten",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "serie_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "kalender_token",
                table: "AspNetUsers");
        }
    }
}
