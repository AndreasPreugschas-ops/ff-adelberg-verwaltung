using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Lehrgangswuensche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lehrgangswuensche",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    eingetragen_am = table.Column<DateOnly>(type: "date", nullable: false),
                    begruendung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    prioritaet = table.Column<int>(type: "integer", nullable: true),
                    gemeldet_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    teilnahme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lehrgangswuensche", x => x.id);
                    table.ForeignKey(
                        name: "fk_lehrgangswuensche_lehrgangsteilnahmen_teilnahme_id",
                        column: x => x.teilnahme_id,
                        principalTable: "lehrgangsteilnahmen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lehrgangswuensche_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lehrgangswuensche_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangswuensche_jahr_status",
                table: "lehrgangswuensche",
                columns: new[] { "jahr", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangswuensche_mitglied_id",
                table: "lehrgangswuensche",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangswuensche_qualifikation_id",
                table: "lehrgangswuensche",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_lehrgangswuensche_teilnahme_id",
                table: "lehrgangswuensche",
                column: "teilnahme_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lehrgangswuensche");
        }
    }
}
