using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Gesundheitsnachweise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "gesundheitsbezogen",
                table: "nachweisarten",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Fahrerlaubnis C/C1/CE ist 5 Jahre gültig; bestehende Kataloge ohne Angabe bekommen den Standardwert.
            migrationBuilder.Sql("UPDATE qualifikationen SET gueltigkeit_monate = 60 WHERE code IN ('FS-C', 'FS-C1', 'FS-CE') AND gueltigkeit_monate IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gesundheitsbezogen",
                table: "nachweisarten");
        }
    }
}
