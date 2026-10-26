using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Arbeitgeberbescheinigungen mit Prüfcode und Erstattungen an Arbeitgeber nach § 15 Abs. 2 FwG (V33).</summary>
    public partial class Arbeitgeberbescheinigungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arbeitgeberbescheinigungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    pruefcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    arbeitgeber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    zeilen = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    stunden = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    ruhezeit_stunden = table.Column<int>(type: "integer", nullable: true),
                    ausgestellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ausgestellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    widerrufen = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arbeitgeberbescheinigungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_arbeitgeberbescheinigungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arbeitgebererstattungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arbeitgeber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    von = table.Column<DateOnly>(type: "date", nullable: false),
                    bis = table.Column<DateOnly>(type: "date", nullable: false),
                    entgelt = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sozialversicherung = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    pruefcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    stand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    eingegangen_am = table.Column<DateOnly>(type: "date", nullable: false),
                    entschieden_am = table.Column<DateOnly>(type: "date", nullable: true),
                    vermerk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arbeitgebererstattungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_arbeitgebererstattungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arbeitgeberbescheinigungen_mitglied_id",
                table: "arbeitgeberbescheinigungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_arbeitgeberbescheinigungen_pruefcode",
                table: "arbeitgeberbescheinigungen",
                column: "pruefcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arbeitgebererstattungen_mitglied_id_von",
                table: "arbeitgebererstattungen",
                columns: new[] { "mitglied_id", "von" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arbeitgeberbescheinigungen");

            migrationBuilder.DropTable(
                name: "arbeitgebererstattungen");
        }
    }
}
