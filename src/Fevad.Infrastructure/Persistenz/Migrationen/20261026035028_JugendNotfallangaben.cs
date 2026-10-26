using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class JugendNotfallangaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "abholberechtigte",
                table: "mitglieder",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abholregelung",
                table: "mitglieder",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "notfallmappe_am",
                table: "mitglieder",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "abholberechtigte",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "abholregelung",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "notfallmappe_am",
                table: "mitglieder");
        }
    }
}
