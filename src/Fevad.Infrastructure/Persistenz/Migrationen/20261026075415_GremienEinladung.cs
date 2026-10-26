using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class GremienEinladung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "buergermeister_eingeladen_am",
                table: "sitzungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "dienst_id",
                table: "sitzungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "einladung_empfaenger",
                table: "sitzungen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "einladung_ohne_mail",
                table: "sitzungen",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "einladung_versandt_am",
                table: "sitzungen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buergermeister_email",
                table: "organisationen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buergermeister_name",
                table: "organisationen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "aufgehoben_am",
                table: "beschluesse",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aufgehoben_durch",
                table: "beschluesse",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "buergermeister_eingeladen_am",
                table: "sitzungen");

            migrationBuilder.DropColumn(
                name: "dienst_id",
                table: "sitzungen");

            migrationBuilder.DropColumn(
                name: "einladung_empfaenger",
                table: "sitzungen");

            migrationBuilder.DropColumn(
                name: "einladung_ohne_mail",
                table: "sitzungen");

            migrationBuilder.DropColumn(
                name: "einladung_versandt_am",
                table: "sitzungen");

            migrationBuilder.DropColumn(
                name: "buergermeister_email",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "buergermeister_name",
                table: "organisationen");

            migrationBuilder.DropColumn(
                name: "aufgehoben_am",
                table: "beschluesse");

            migrationBuilder.DropColumn(
                name: "aufgehoben_durch",
                table: "beschluesse");
        }
    }
}
