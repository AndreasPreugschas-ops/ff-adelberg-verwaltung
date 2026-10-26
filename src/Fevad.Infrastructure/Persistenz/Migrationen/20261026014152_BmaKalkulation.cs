using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    /// <summary>Brandmeldeanlagen mit Betreiber, Fehlalarm am Einsatz und Kalkulation des Personalstundensatzes (V24).</summary>
    public partial class BmaKalkulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bma_fehlalarm",
                table: "einsaetze",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "bma_objekt_id",
                table: "einsaetze",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bma_objekte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    aufschaltnummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    konzessionaer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    betreiber_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    betreiber_anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ansprechpartner = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bma_objekte", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "satzkalkulationen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kalkuliert_am = table.Column<DateOnly>(type: "date", nullable: false),
                    erstes_jahr = table.Column<int>(type: "integer", nullable: false),
                    kosten1 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    kosten2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    kosten3 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    kosten4 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    angehoerige = table.Column<int>(type: "integer", nullable: false),
                    stunden_je_angehoerigem = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    entschaedigungssatz = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    erlaeuterung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    bearbeiter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_satzkalkulationen", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_bma_objekt_id",
                table: "einsaetze",
                column: "bma_objekt_id");

            migrationBuilder.AddForeignKey(
                name: "fk_einsaetze_bma_objekte_bma_objekt_id",
                table: "einsaetze",
                column: "bma_objekt_id",
                principalTable: "bma_objekte",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_einsaetze_bma_objekte_bma_objekt_id",
                table: "einsaetze");

            migrationBuilder.DropTable(
                name: "bma_objekte");

            migrationBuilder.DropTable(
                name: "satzkalkulationen");

            migrationBuilder.DropIndex(
                name: "ix_einsaetze_bma_objekt_id",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "bma_fehlalarm",
                table: "einsaetze");

            migrationBuilder.DropColumn(
                name: "bma_objekt_id",
                table: "einsaetze");
        }
    }
}
