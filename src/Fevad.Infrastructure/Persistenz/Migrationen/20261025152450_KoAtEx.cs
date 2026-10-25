using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class KoAtEx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exposition",
                table: "atemschutz_nachweise",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "maske",
                table: "atemschutz_nachweise",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pa_geraet",
                table: "atemschutz_nachweise",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exposition",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "maske",
                table: "atemschutz_nachweise");

            migrationBuilder.DropColumn(
                name: "pa_geraet",
                table: "atemschutz_nachweise");
        }
    }
}
