using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class OrganisationUndAnschriften : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ort",
                table: "abteilungen",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plz",
                table: "abteilungen",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "strasse",
                table: "abteilungen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organisationen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feuerwehr_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    feuerwehr_strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    feuerwehr_plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    feuerwehr_ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    feuerwehr_telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    feuerwehr_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gemeinde_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    gemeinde_vertretung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gemeinde_strasse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gemeinde_plz = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    gemeinde_ort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gemeinde_telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gemeinde_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    logo_inhaltstyp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisationen", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organisationen");

            migrationBuilder.DropColumn(
                name: "ort",
                table: "abteilungen");

            migrationBuilder.DropColumn(
                name: "plz",
                table: "abteilungen");

            migrationBuilder.DropColumn(
                name: "strasse",
                table: "abteilungen");
        }
    }
}
