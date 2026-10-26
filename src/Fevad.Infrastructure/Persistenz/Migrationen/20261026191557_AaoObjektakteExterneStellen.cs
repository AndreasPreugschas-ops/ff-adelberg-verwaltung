using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AaoObjektakteExterneStellen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "externe_stelle_id",
                table: "einsatz_organisationen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "feuerwehrplan_dokument_id",
                table: "bma_objekte",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "feuerwehrplan_stand",
                table: "bma_objekte",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "hat_bma",
                table: "bma_objekte",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "nutzung",
                table: "bma_objekte",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aao_staende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    beschlossen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    buergermeister_mitgeteilt_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_aao_staende", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "externe_stellen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    anschrift = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_externe_stellen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objekt_begehungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bma_objekt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    am = table.Column<DateOnly>(type: "date", nullable: false),
                    teilnehmer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ergebnis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objekt_begehungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_objekt_begehungen_bma_objekte_bma_objekt_id",
                        column: x => x.bma_objekt_id,
                        principalTable: "bma_objekte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aao_regeln",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aao_stand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stichwort = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    alarmstufe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fahrzeuge = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    mindeststaerke = table.Column<int>(type: "integer", nullable: true),
                    mindest_atemschutz = table.Column<int>(type: "integer", nullable: true),
                    bemerkung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aao_regeln", x => x.id);
                    table.ForeignKey(
                        name: "fk_aao_regeln_aao_staende_aao_stand_id",
                        column: x => x.aao_stand_id,
                        principalTable: "aao_staende",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_organisationen_externe_stelle_id",
                table: "einsatz_organisationen",
                column: "externe_stelle_id");

            migrationBuilder.CreateIndex(
                name: "ix_bma_objekte_feuerwehrplan_dokument_id",
                table: "bma_objekte",
                column: "feuerwehrplan_dokument_id");

            migrationBuilder.CreateIndex(
                name: "ix_aao_regeln_aao_stand_id",
                table: "aao_regeln",
                column: "aao_stand_id");

            migrationBuilder.CreateIndex(
                name: "ix_objekt_begehungen_bma_objekt_id",
                table: "objekt_begehungen",
                column: "bma_objekt_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bma_objekte_dokumente_feuerwehrplan_dokument_id",
                table: "bma_objekte",
                column: "feuerwehrplan_dokument_id",
                principalTable: "dokumente",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_einsatz_organisationen_externe_stellen_externe_stelle_id",
                table: "einsatz_organisationen",
                column: "externe_stelle_id",
                principalTable: "externe_stellen",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bma_objekte_dokumente_feuerwehrplan_dokument_id",
                table: "bma_objekte");

            migrationBuilder.DropForeignKey(
                name: "fk_einsatz_organisationen_externe_stellen_externe_stelle_id",
                table: "einsatz_organisationen");

            migrationBuilder.DropTable(
                name: "aao_regeln");

            migrationBuilder.DropTable(
                name: "externe_stellen");

            migrationBuilder.DropTable(
                name: "objekt_begehungen");

            migrationBuilder.DropTable(
                name: "aao_staende");

            migrationBuilder.DropIndex(
                name: "ix_einsatz_organisationen_externe_stelle_id",
                table: "einsatz_organisationen");

            migrationBuilder.DropIndex(
                name: "ix_bma_objekte_feuerwehrplan_dokument_id",
                table: "bma_objekte");

            migrationBuilder.DropColumn(
                name: "externe_stelle_id",
                table: "einsatz_organisationen");

            migrationBuilder.DropColumn(
                name: "feuerwehrplan_dokument_id",
                table: "bma_objekte");

            migrationBuilder.DropColumn(
                name: "feuerwehrplan_stand",
                table: "bma_objekte");

            migrationBuilder.DropColumn(
                name: "hat_bma",
                table: "bma_objekte");

            migrationBuilder.DropColumn(
                name: "nutzung",
                table: "bma_objekte");
        }
    }
}
