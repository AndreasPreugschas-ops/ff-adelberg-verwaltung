using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class GliederungUndMitgliederakte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "probezeit_bemerkung",
                table: "mitgliedschaften",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "probezeit_bis",
                table: "mitgliedschaften",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arbeitgeber_email",
                table: "mitglieder",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arbeitgeber_mobil",
                table: "mitglieder",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arbeitgeber_plz",
                table: "mitglieder",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arbeitgeber_strasse",
                table: "mitglieder",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arbeitgeber_telefon",
                table: "mitglieder",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "beruf",
                table: "mitglieder",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "geschlecht",
                table: "mitglieder",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "abteilung_id",
                table: "gruppen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "art",
                table: "gruppen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Gruppe");

            migrationBuilder.AddColumn<Guid>(
                name: "uebergeordnete_id",
                table: "gruppen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "art",
                table: "abteilungen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Einsatz");

            // Bisher hingen die Regeln am Code; die Art daraus ableiten (MU war der vorgesehene Code der Musikabteilung).
            migrationBuilder.Sql("""
                UPDATE abteilungen SET art = CASE code
                    WHEN 'JF' THEN 'Jugend'
                    WHEN 'AA' THEN 'Alters'
                    WHEN 'MU' THEN 'Musik'
                    ELSE 'Einsatz' END;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "uebergeordnete_id",
                table: "abteilungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_gruppen_abteilung_id",
                table: "gruppen",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_gruppen_uebergeordnete_id",
                table: "gruppen",
                column: "uebergeordnete_id");

            migrationBuilder.CreateIndex(
                name: "ix_abteilungen_uebergeordnete_id",
                table: "abteilungen",
                column: "uebergeordnete_id");

            migrationBuilder.AddForeignKey(
                name: "fk_abteilungen_abteilungen_uebergeordnete_id",
                table: "abteilungen",
                column: "uebergeordnete_id",
                principalTable: "abteilungen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_gruppen_abteilungen_abteilung_id",
                table: "gruppen",
                column: "abteilung_id",
                principalTable: "abteilungen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_gruppen_gruppen_uebergeordnete_id",
                table: "gruppen",
                column: "uebergeordnete_id",
                principalTable: "gruppen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_abteilungen_abteilungen_uebergeordnete_id",
                table: "abteilungen");

            migrationBuilder.DropForeignKey(
                name: "fk_gruppen_abteilungen_abteilung_id",
                table: "gruppen");

            migrationBuilder.DropForeignKey(
                name: "fk_gruppen_gruppen_uebergeordnete_id",
                table: "gruppen");

            migrationBuilder.DropIndex(
                name: "ix_gruppen_abteilung_id",
                table: "gruppen");

            migrationBuilder.DropIndex(
                name: "ix_gruppen_uebergeordnete_id",
                table: "gruppen");

            migrationBuilder.DropIndex(
                name: "ix_abteilungen_uebergeordnete_id",
                table: "abteilungen");

            migrationBuilder.DropColumn(
                name: "probezeit_bemerkung",
                table: "mitgliedschaften");

            migrationBuilder.DropColumn(
                name: "probezeit_bis",
                table: "mitgliedschaften");

            migrationBuilder.DropColumn(
                name: "arbeitgeber_email",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "arbeitgeber_mobil",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "arbeitgeber_plz",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "arbeitgeber_strasse",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "arbeitgeber_telefon",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "beruf",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "geschlecht",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "abteilung_id",
                table: "gruppen");

            migrationBuilder.DropColumn(
                name: "art",
                table: "gruppen");

            migrationBuilder.DropColumn(
                name: "uebergeordnete_id",
                table: "gruppen");

            migrationBuilder.DropColumn(
                name: "art",
                table: "abteilungen");

            migrationBuilder.DropColumn(
                name: "uebergeordnete_id",
                table: "abteilungen");
        }
    }
}
