using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Atemschutztechnik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "nutzbar_bis",
                table: "geraete",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bauteilarten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eingebaut_monate = table.Column<int>(type: "integer", nullable: true),
                    gesamt_monate = table.Column<int>(type: "integer", nullable: true),
                    grundlage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kurzbezeichnung = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sortierung = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bauteilarten", x => x.id);
                    table.ForeignKey(
                        name: "fk_bauteilarten_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "flaschenfuellungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gefuellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    druck_bar = table.Column<int>(type: "integer", nullable: false),
                    gefuellt_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anlage_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_flaschenfuellungen", x => x.id);
                    table.CheckConstraint("ck_flaschenfuellungen_druck", "druck_bar > 0 AND druck_bar <= 450");
                    table.ForeignKey(
                        name: "fk_flaschenfuellungen_geraete_anlage_id",
                        column: x => x.anlage_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_flaschenfuellungen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_flaschenfuellungen_mitglieder_gefuellt_von_id",
                        column: x => x.gefuellt_von_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "werkstattauftraege",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    werkstatt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    werkstattname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    abgegeben_am = table.Column<DateOnly>(type: "date", nullable: false),
                    abgegeben_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zurueck_am = table.Column<DateOnly>(type: "date", nullable: true),
                    beleg_werkstatt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_werkstattauftraege", x => x.id);
                    table.ForeignKey(
                        name: "fk_werkstattauftraege_mitglieder_abgegeben_von_id",
                        column: x => x.abgegeben_von_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bauteile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bauteilart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hergestellt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    eingebaut_am = table.Column<DateOnly>(type: "date", nullable: false),
                    ausgebaut_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_bauteile", x => x.id);
                    table.ForeignKey(
                        name: "fk_bauteile_bauteilarten_bauteilart_id",
                        column: x => x.bauteilart_id,
                        principalTable: "bauteilarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bauteile_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "werkstattpositionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    werkstattauftrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leistungen = table.Column<int>(type: "integer", maxLength: 40, nullable: false),
                    pruefung_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_werkstattpositionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_werkstattpositionen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_werkstattpositionen_pruefungen_pruefung_id",
                        column: x => x.pruefung_id,
                        principalTable: "pruefungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_werkstattpositionen_werkstattauftraege_werkstattauftrag_id",
                        column: x => x.werkstattauftrag_id,
                        principalTable: "werkstattauftraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "werkstattprotokolle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    werkstattauftrag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inhalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_werkstattprotokolle", x => x.id);
                    table.ForeignKey(
                        name: "fk_werkstattprotokolle_werkstattauftraege_werkstattauftrag_id",
                        column: x => x.werkstattauftrag_id,
                        principalTable: "werkstattauftraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bauteilarten_code",
                table: "bauteilarten",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bauteilarten_geraeteart_id",
                table: "bauteilarten",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_bauteile_bauteilart_id",
                table: "bauteile",
                column: "bauteilart_id");

            migrationBuilder.CreateIndex(
                name: "ix_bauteile_geraet_id",
                table: "bauteile",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_flaschenfuellungen_anlage_id",
                table: "flaschenfuellungen",
                column: "anlage_id");

            migrationBuilder.CreateIndex(
                name: "ix_flaschenfuellungen_gefuellt_von_id",
                table: "flaschenfuellungen",
                column: "gefuellt_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_flaschenfuellungen_geraet_id_gefuellt_am",
                table: "flaschenfuellungen",
                columns: new[] { "geraet_id", "gefuellt_am" });

            migrationBuilder.CreateIndex(
                name: "ix_werkstattauftraege_abgegeben_von_id",
                table: "werkstattauftraege",
                column: "abgegeben_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_werkstattauftraege_nummer",
                table: "werkstattauftraege",
                column: "nummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_werkstattauftraege_zurueck_am",
                table: "werkstattauftraege",
                column: "zurueck_am");

            migrationBuilder.CreateIndex(
                name: "ix_werkstattpositionen_geraet_id",
                table: "werkstattpositionen",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_werkstattpositionen_pruefung_id",
                table: "werkstattpositionen",
                column: "pruefung_id");

            migrationBuilder.CreateIndex(
                name: "ix_werkstattpositionen_werkstattauftrag_id_geraet_id",
                table: "werkstattpositionen",
                columns: new[] { "werkstattauftrag_id", "geraet_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_werkstattprotokolle_werkstattauftrag_id",
                table: "werkstattprotokolle",
                column: "werkstattauftrag_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bauteile");

            migrationBuilder.DropTable(
                name: "flaschenfuellungen");

            migrationBuilder.DropTable(
                name: "werkstattpositionen");

            migrationBuilder.DropTable(
                name: "werkstattprotokolle");

            migrationBuilder.DropTable(
                name: "bauteilarten");

            migrationBuilder.DropTable(
                name: "werkstattauftraege");

            migrationBuilder.DropColumn(
                name: "nutzbar_bis",
                table: "geraete");
        }
    }
}
