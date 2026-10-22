using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EhrungenVwv2025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "vorlauf_monate",
                table: "ehrungsarten",
                newName: "vorlauf_wochen");

            // Vorlauf war in Monaten gespeichert.
            migrationBuilder.Sql("UPDATE ehrungsarten SET vorlauf_wochen = round(vorlauf_wochen * 4.35);");

            migrationBuilder.AddColumn<string>(
                name: "befreiungsgrund",
                table: "mitgliedschaften",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "einsatzdienst_bestaetigt",
                table: "mitgliedschaften",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "vordienstart",
                table: "mitgliedschaften",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "antragsweg",
                table: "ehrungsarten",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "funktion_code",
                table: "ehrungsarten",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kontingent",
                table: "ehrungsarten",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mindestabstand_jahre",
                table: "ehrungsarten",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mit_antragsschluss",
                table: "ehrungsarten",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "nicht_mit_qualifikationen",
                table: "ehrungsarten",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<Guid>(
                name: "vorstufe_id",
                table: "ehrungsarten",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ehrungsarten_vorstufe_id",
                table: "ehrungsarten",
                column: "vorstufe_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ehrungsarten_ehrungsarten_vorstufe_id",
                table: "ehrungsarten",
                column: "vorstufe_id",
                principalTable: "ehrungsarten",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ehrungsarten_ehrungsarten_vorstufe_id",
                table: "ehrungsarten");

            migrationBuilder.DropIndex(
                name: "ix_ehrungsarten_vorstufe_id",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "befreiungsgrund",
                table: "mitgliedschaften");

            migrationBuilder.DropColumn(
                name: "einsatzdienst_bestaetigt",
                table: "mitgliedschaften");

            migrationBuilder.DropColumn(
                name: "vordienstart",
                table: "mitgliedschaften");

            migrationBuilder.DropColumn(
                name: "antragsweg",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "funktion_code",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "kontingent",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "mindestabstand_jahre",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "mit_antragsschluss",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "nicht_mit_qualifikationen",
                table: "ehrungsarten");

            migrationBuilder.DropColumn(
                name: "vorstufe_id",
                table: "ehrungsarten");

            migrationBuilder.RenameColumn(
                name: "vorlauf_wochen",
                table: "ehrungsarten",
                newName: "vorlauf_monate");

            migrationBuilder.Sql("UPDATE ehrungsarten SET vorlauf_monate = round(vorlauf_monate / 4.35);");
        }
    }
}
