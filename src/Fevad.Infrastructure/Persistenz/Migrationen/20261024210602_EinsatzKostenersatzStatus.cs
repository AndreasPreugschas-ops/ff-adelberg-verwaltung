using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinsatzKostenersatzStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "kostenersatz_abgeschlossen_am",
                table: "einsaetze",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "kostenersatz_offen",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kostenersatz_abgeschlossen_am",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "kostenersatz_offen",
                table: "einsaetze");
        }
    }
}
