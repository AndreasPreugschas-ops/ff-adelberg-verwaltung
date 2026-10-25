using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Auslagenerstattungen als Art der vorbereiteten Ausgabe mit Mitglied, wiederkehrende Buchungen (V07).</summary>
    public partial class KassenAuslagenUndWiederkehrend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "art",
                table: "zahlungsanordnungen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Ausgabe");

            migrationBuilder.AddColumn<Guid>(
                name: "mitglied_id",
                table: "zahlungsanordnungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "wiederkehrende_buchungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    betrag = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    betrag_parameter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    planposition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    rhythmus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tag = table.Column<int>(type: "integer", nullable: false),
                    naechste_faelligkeit = table.Column<DateOnly>(type: "date", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wiederkehrende_buchungen", x => x.id);
                    table.CheckConstraint("ck_wiederkehrende_buchungen_art", "art <> 'Umbuchung'");
                    table.CheckConstraint("ck_wiederkehrende_buchungen_betrag", "betrag IS NULL OR betrag > 0");
                    table.CheckConstraint("ck_wiederkehrende_buchungen_tag", "tag BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "fk_wiederkehrende_buchungen_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wiederkehrende_buchungen_planpositionen_planposition_id",
                        column: x => x.planposition_id,
                        principalTable: "planpositionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_wiederkehrende_buchungen_zahlungsmittelkonten_konto_id",
                        column: x => x.konto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_zahlungsanordnungen_mitglied_id",
                table: "zahlungsanordnungen",
                column: "mitglied_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_zahlungsanordnungen_mitglied",
                table: "zahlungsanordnungen",
                sql: "art <> 'Auslagenerstattung' OR mitglied_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_wiederkehrende_buchungen_kasse_id_naechste_faelligkeit",
                table: "wiederkehrende_buchungen",
                columns: new[] { "kasse_id", "naechste_faelligkeit" });

            migrationBuilder.CreateIndex(
                name: "ix_wiederkehrende_buchungen_konto_id",
                table: "wiederkehrende_buchungen",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_wiederkehrende_buchungen_planposition_id",
                table: "wiederkehrende_buchungen",
                column: "planposition_id");

            migrationBuilder.AddForeignKey(
                name: "fk_zahlungsanordnungen_mitglieder_mitglied_id",
                table: "zahlungsanordnungen",
                column: "mitglied_id",
                principalTable: "mitglieder",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_zahlungsanordnungen_mitglieder_mitglied_id",
                table: "zahlungsanordnungen");

            migrationBuilder.DropTable(
                name: "wiederkehrende_buchungen");

            migrationBuilder.DropIndex(
                name: "ix_zahlungsanordnungen_mitglied_id",
                table: "zahlungsanordnungen");

            migrationBuilder.DropCheckConstraint(
                name: "ck_zahlungsanordnungen_mitglied",
                table: "zahlungsanordnungen");

            migrationBuilder.DropColumn(
                name: "art",
                table: "zahlungsanordnungen");

            migrationBuilder.DropColumn(
                name: "mitglied_id",
                table: "zahlungsanordnungen");
        }
    }
}
