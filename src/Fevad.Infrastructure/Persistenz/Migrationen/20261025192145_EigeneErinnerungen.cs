using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EigeneErinnerungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ohne_erinnerung_ausstattung",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ohne_erinnerung_fristen",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ohne_erinnerung_rueckmeldungen",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ohne_erinnerung_ausstattung",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ohne_erinnerung_fristen",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ohne_erinnerung_rueckmeldungen",
                table: "AspNetUsers");
        }
    }
}
