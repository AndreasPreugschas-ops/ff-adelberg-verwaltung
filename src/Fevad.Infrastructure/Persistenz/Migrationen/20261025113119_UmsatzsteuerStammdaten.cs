using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class UmsatzsteuerStammdaten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finanzamt",
                table: "organisationen",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "kleinunternehmer",
                table: "organisationen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "steuernummer",
                table: "organisationen",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "umsatzsteuer_hinweis",
                table: "organisationen",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "umsatzsteuer_id",
                table: "organisationen",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "umsatzsteuer_paragraph2b_ab",
                table: "organisationen",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "versteuerungsart",
                table: "organisationen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voranmeldungszeitraum",
                table: "organisationen",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Angabe der Kämmerei (Andi 25.10.2026): § 2b UStG gilt für Adelberg ab Januar 2027.
            migrationBuilder.Sql("UPDATE organisationen SET umsatzsteuer_paragraph2b_ab = DATE '2027-01-01' WHERE umsatzsteuer_paragraph2b_ab IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "finanzamt",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "kleinunternehmer",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "steuernummer",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "umsatzsteuer_hinweis",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "umsatzsteuer_id",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "umsatzsteuer_paragraph2b_ab",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "versteuerungsart",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "voranmeldungszeitraum",
                table: "organisationen");
        }
    }
}
