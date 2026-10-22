using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Abzeichen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "alter_nach_jahrgang",
                table: "qualifikationen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "hoechstalter_jahre",
                table: "qualifikationen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mindest_jugend_monate",
                table: "qualifikationen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mindestalter_jahre",
                table: "qualifikationen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voraussetzungen",
                table: "qualifikationen",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "vorstufe_abstand_jahre",
                table: "qualifikationen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vorstufe_id",
                table: "qualifikationen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "urkunde",
                table: "qualifikation_nachweise",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_qualifikationen_vorstufe_id",
                table: "qualifikationen",
                column: "vorstufe_id");

            migrationBuilder.AddForeignKey(
                name: "fk_qualifikationen_qualifikationen_vorstufe_id",
                table: "qualifikationen",
                column: "vorstufe_id",
                principalTable: "qualifikationen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Der Ehrungen-Teil legt die Leistungsspange notfalls als "Sonstiges" an; hier bekommt sie ihre Abzeichen-Regeln.
            migrationBuilder.Sql("UPDATE qualifikationen SET art = 'Abzeichen', mindestalter_jahre = 15, alter_nach_jahrgang = TRUE, mindest_jugend_monate = 12 WHERE code = 'LSP' AND art = 'Sonstiges';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_qualifikationen_qualifikationen_vorstufe_id",
                table: "qualifikationen");

            migrationBuilder.DropIndex(
                name: "ix_qualifikationen_vorstufe_id",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "alter_nach_jahrgang",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "hoechstalter_jahre",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "mindest_jugend_monate",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "mindestalter_jahre",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "voraussetzungen",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "vorstufe_abstand_jahre",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "vorstufe_id",
                table: "qualifikationen");

            migrationBuilder.DropColumn(
                name: "urkunde",
                table: "qualifikation_nachweise");
        }
    }
}
