using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Unfallmeldungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "art_der_verletzung",
                table: "unfaelle");

            migrationBuilder.AddColumn<string>(
                name: "art",
                table: "unfaelle",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unfall");

            migrationBuilder.AddColumn<bool>(
                name: "selbstmeldung",
                table: "unfaelle",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "verletzungsart_id",
                table: "unfaelle",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sachschaeden",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anlass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    gegenstand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hergang = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    weitergeleitet_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    erstattet_betrag = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    abgeschlossen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    regress = table.Column<bool>(type: "boolean", nullable: false),
                    regress_bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_sachschaeden", x => x.id);
                    table.ForeignKey(
                        name: "fk_sachschaeden_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sachschaeden_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sachschaeden_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verletzungsarten",
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
                    table.PrimaryKey("pk_verletzungsarten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meldungsfotos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unfall_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sachschaden_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meldungsfotos", x => x.id);
                    table.CheckConstraint("ck_meldungsfotos_bezug", "(unfall_id IS NULL) <> (sachschaden_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_meldungsfotos_sachschaeden_sachschaden_id",
                        column: x => x.sachschaden_id,
                        principalTable: "sachschaeden",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meldungsfotos_unfaelle_unfall_id",
                        column: x => x.unfall_id,
                        principalTable: "unfaelle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unfaelle_verletzungsart_id",
                table: "unfaelle",
                column: "verletzungsart_id");

            migrationBuilder.CreateIndex(
                name: "ix_meldungsfotos_sachschaden_id",
                table: "meldungsfotos",
                column: "sachschaden_id");

            migrationBuilder.CreateIndex(
                name: "ix_meldungsfotos_unfall_id",
                table: "meldungsfotos",
                column: "unfall_id");

            migrationBuilder.CreateIndex(
                name: "ix_sachschaeden_dienst_id",
                table: "sachschaeden",
                column: "dienst_id");

            migrationBuilder.CreateIndex(
                name: "ix_sachschaeden_einsatz_id",
                table: "sachschaeden",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_sachschaeden_mitglied_id",
                table: "sachschaeden",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_sachschaeden_zeitpunkt",
                table: "sachschaeden",
                column: "zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "ix_verletzungsarten_code",
                table: "verletzungsarten",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_unfaelle_verletzungsarten_verletzungsart_id",
                table: "unfaelle",
                column: "verletzungsart_id",
                principalTable: "verletzungsarten",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_unfaelle_verletzungsarten_verletzungsart_id",
                table: "unfaelle");

            migrationBuilder.DropTable(
                name: "meldungsfotos");

            migrationBuilder.DropTable(
                name: "verletzungsarten");

            migrationBuilder.DropTable(
                name: "sachschaeden");

            migrationBuilder.DropIndex(
                name: "ix_unfaelle_verletzungsart_id",
                table: "unfaelle");

            migrationBuilder.DropColumn(
                name: "art",
                table: "unfaelle");

            migrationBuilder.DropColumn(
                name: "selbstmeldung",
                table: "unfaelle");

            migrationBuilder.DropColumn(
                name: "verletzungsart_id",
                table: "unfaelle");

            migrationBuilder.AddColumn<string>(
                name: "art_der_verletzung",
                table: "unfaelle",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
