using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Loeschkonzept_L5_L7_L14_L19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anonymisiert_am",
                table: "mitglieder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "anwesenheitsjahressummen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    anwesend = table.Column<int>(type: "integer", nullable: false),
                    entschuldigt = table.Column<int>(type: "integer", nullable: false),
                    unentschuldigt = table.Column<int>(type: "integer", nullable: false),
                    minuten = table.Column<int>(type: "integer", nullable: false),
                    fortbildung_minuten = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anwesenheitsjahressummen", x => x.id);
                    table.ForeignKey(
                        name: "fk_anwesenheitsjahressummen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anwesenheitsjahressummen_mitglied_id_jahr",
                table: "anwesenheitsjahressummen",
                columns: new[] { "mitglied_id", "jahr" },
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anwesenheitsjahressummen");

            migrationBuilder.DropColumn(
                name: "anonymisiert_am",
                table: "mitglieder");
        }
    }
}
