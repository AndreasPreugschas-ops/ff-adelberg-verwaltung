using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Ausbildungsplanung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ausbildungsthema_id",
                table: "dienste",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fahrzeuge_und_material",
                table: "dienste",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lernziele",
                table: "dienste",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ausbildungsthemen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grundlage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pflicht = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kurzbezeichnung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ausbildungsthemen", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dienste_ausbildungsthema_id",
                table: "dienste",
                column: "ausbildungsthema_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausbildungsthemen_code",
                table: "ausbildungsthemen",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_dienste_ausbildungsthemen_ausbildungsthema_id",
                table: "dienste",
                column: "ausbildungsthema_id",
                principalTable: "ausbildungsthemen",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dienste_ausbildungsthemen_ausbildungsthema_id",
                table: "dienste");

            migrationBuilder.DropTable(
                name: "ausbildungsthemen");

            migrationBuilder.DropIndex(
                name: "ix_dienste_ausbildungsthema_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "ausbildungsthema_id",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "fahrzeuge_und_material",
                table: "dienste");

            migrationBuilder.DropColumn(
                name: "lernziele",
                table: "dienste");
        }
    }
}
