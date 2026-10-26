using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Verbrauchsmaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lagerartikel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kategorie = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einheit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mindestmenge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    stueckpreis = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    lagerort = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lagerartikel", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lagerchargen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lagerartikel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    verfall_am = table.Column<DateOnly>(type: "date", nullable: true),
                    eingang_am = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lagerchargen", x => x.id);
                    table.CheckConstraint("ck_lagerchargen_menge", "menge >= 0");
                    table.ForeignKey(
                        name: "fk_lagerchargen_lagerartikel_lagerartikel_id",
                        column: x => x.lagerartikel_id,
                        principalTable: "lagerartikel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lagerbewegungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lagerartikel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lagercharge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    menge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    stueckpreis = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zusatzkosten_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lagerbewegungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_lagerbewegungen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lagerbewegungen_lagerartikel_lagerartikel_id",
                        column: x => x.lagerartikel_id,
                        principalTable: "lagerartikel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lagerbewegungen_lagerchargen_lagercharge_id",
                        column: x => x.lagercharge_id,
                        principalTable: "lagerchargen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lagerbewegungen_zusatzkosten_zusatzkosten_id",
                        column: x => x.zusatzkosten_id,
                        principalTable: "zusatzkosten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lagerartikel_bezeichnung",
                table: "lagerartikel",
                column: "bezeichnung");

            migrationBuilder.CreateIndex(
                name: "ix_lagerbewegungen_einsatz_id",
                table: "lagerbewegungen",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_lagerbewegungen_lagerartikel_id_datum",
                table: "lagerbewegungen",
                columns: new[] { "lagerartikel_id", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_lagerbewegungen_lagercharge_id",
                table: "lagerbewegungen",
                column: "lagercharge_id");

            migrationBuilder.CreateIndex(
                name: "ix_lagerbewegungen_zusatzkosten_id",
                table: "lagerbewegungen",
                column: "zusatzkosten_id");

            migrationBuilder.CreateIndex(
                name: "ix_lagerchargen_lagerartikel_id",
                table: "lagerchargen",
                column: "lagerartikel_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lagerbewegungen");

            migrationBuilder.DropTable(
                name: "lagerchargen");

            migrationBuilder.DropTable(
                name: "lagerartikel");
        }
    }
}
