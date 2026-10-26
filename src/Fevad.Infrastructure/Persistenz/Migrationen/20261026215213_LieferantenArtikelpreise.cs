using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class LieferantenArtikelpreise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lieferanten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kundennummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    e_mail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    webseite = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    deaktiviert_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lieferanten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "artikel_preise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artikel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lieferant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preis = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bestellnummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    stand_am = table.Column<DateOnly>(type: "date", nullable: true),
                    bevorzugt = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artikel_preise", x => x.id);
                    table.CheckConstraint("ck_artikel_preise_preis", "preis >= 0");
                    table.ForeignKey(
                        name: "fk_artikel_preise_artikel_artikel_id",
                        column: x => x.artikel_id,
                        principalTable: "artikel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artikel_preise_lieferanten_lieferant_id",
                        column: x => x.lieferant_id,
                        principalTable: "lieferanten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artikel_preise_artikel_id_lieferant_id",
                table: "artikel_preise",
                columns: new[] { "artikel_id", "lieferant_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_artikel_preise_lieferant_id",
                table: "artikel_preise",
                column: "lieferant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lieferanten_name",
                table: "lieferanten",
                column: "name",
                unique: true,
                filter: "geloescht_am IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artikel_preise");

            migrationBuilder.DropTable(
                name: "lieferanten");
        }
    }
}
