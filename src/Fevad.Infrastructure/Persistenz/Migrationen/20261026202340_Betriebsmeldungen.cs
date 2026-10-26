using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Betriebsmeldungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "betriebsmeldungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erfolgreich = table.Column<bool>(type: "boolean", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_betriebsmeldungen", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_betriebsmeldungen_art_zeitpunkt",
                table: "betriebsmeldungen",
                columns: new[] { "art", "zeitpunkt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "betriebsmeldungen");
        }
    }
}
