using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinsatzberichteVerdichtet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verdichtet_am",
                table: "einsaetze",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "verdichtet_personenstunden",
                table: "einsaetze",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "verdichtet_teilnehmer",
                table: "einsaetze",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verdichtet_am",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "verdichtet_personenstunden",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "verdichtet_teilnehmer",
                table: "einsaetze");
        }
    }
}
