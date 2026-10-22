using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Kleiderkammer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "artikel_id",
                table: "beschaffungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "groesse",
                table: "beschaffungen",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "artikel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kategorie = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    groessenart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grundausstattung_abteilung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    norm = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_artikel", x => x.id);
                    table.ForeignKey(
                        name: "fk_artikel_abteilungen_grundausstattung_abteilung_id",
                        column: x => x.grundausstattung_abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_artikel_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "groessenangaben",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groessenart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    wert = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erfasst_am = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groessenangaben", x => x.id);
                    table.ForeignKey(
                        name: "fk_groessenangaben_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kleiderkammervorgaenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    quittiert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kleiderkammervorgaenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_kleiderkammervorgaenge_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lagerbestaende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artikel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groesse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    menge = table.Column<int>(type: "integer", nullable: false),
                    mindestmenge = table.Column<int>(type: "integer", nullable: false),
                    lagerort = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lagerbestaende", x => x.id);
                    table.CheckConstraint("ck_lagerbestaende_menge", "menge >= 0 AND mindestmenge >= 0");
                    table.ForeignKey(
                        name: "fk_lagerbestaende_artikel_artikel_id",
                        column: x => x.artikel_id,
                        principalTable: "artikel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ausstattungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artikel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    groesse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    menge = table.Column<int>(type: "integer", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ausgegeben_am = table.Column<DateOnly>(type: "date", nullable: false),
                    ausgabe_vorgang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zurueck_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ruecknahme_vorgang_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zustand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ausstattungen", x => x.id);
                    table.CheckConstraint("ck_ausstattungen_menge", "menge > 0");
                    table.ForeignKey(
                        name: "fk_ausstattungen_artikel_artikel_id",
                        column: x => x.artikel_id,
                        principalTable: "artikel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ausstattungen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ausstattungen_kleiderkammervorgaenge_ausgabe_vorgang_id",
                        column: x => x.ausgabe_vorgang_id,
                        principalTable: "kleiderkammervorgaenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ausstattungen_kleiderkammervorgaenge_ruecknahme_vorgang_id",
                        column: x => x.ruecknahme_vorgang_id,
                        principalTable: "kleiderkammervorgaenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ausstattungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beschaffungen_artikel_id",
                table: "beschaffungen",
                column: "artikel_id");

            migrationBuilder.CreateIndex(
                name: "ix_artikel_code",
                table: "artikel",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artikel_geraeteart_id",
                table: "artikel",
                column: "geraeteart_id",
                unique: true,
                filter: "geraeteart_id IS NOT NULL AND geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_artikel_grundausstattung_abteilung_id",
                table: "artikel",
                column: "grundausstattung_abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausstattungen_artikel_id",
                table: "ausstattungen",
                column: "artikel_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausstattungen_ausgabe_vorgang_id",
                table: "ausstattungen",
                column: "ausgabe_vorgang_id");

            migrationBuilder.CreateIndex(
                name: "ix_ausstattungen_geraet_id",
                table: "ausstattungen",
                column: "geraet_id",
                unique: true,
                filter: "zurueck_am IS NULL AND geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ausstattungen_mitglied_id_zurueck_am",
                table: "ausstattungen",
                columns: new[] { "mitglied_id", "zurueck_am" });

            migrationBuilder.CreateIndex(
                name: "ix_ausstattungen_ruecknahme_vorgang_id",
                table: "ausstattungen",
                column: "ruecknahme_vorgang_id");

            migrationBuilder.CreateIndex(
                name: "ix_groessenangaben_mitglied_id_groessenart",
                table: "groessenangaben",
                columns: new[] { "mitglied_id", "groessenart" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_kleiderkammervorgaenge_mitglied_id",
                table: "kleiderkammervorgaenge",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_kleiderkammervorgaenge_nummer",
                table: "kleiderkammervorgaenge",
                column: "nummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lagerbestaende_artikel_id_groesse",
                table: "lagerbestaende",
                columns: new[] { "artikel_id", "groesse" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_beschaffungen_artikel_artikel_id",
                table: "beschaffungen",
                column: "artikel_id",
                principalTable: "artikel",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_beschaffungen_artikel_artikel_id",
                table: "beschaffungen");

            migrationBuilder.DropTable(
                name: "ausstattungen");

            migrationBuilder.DropTable(
                name: "groessenangaben");

            migrationBuilder.DropTable(
                name: "lagerbestaende");

            migrationBuilder.DropTable(
                name: "kleiderkammervorgaenge");

            migrationBuilder.DropTable(
                name: "artikel");

            migrationBuilder.DropIndex(
                name: "ix_beschaffungen_artikel_id",
                table: "beschaffungen");

            migrationBuilder.DropColumn(
                name: "artikel_id",
                table: "beschaffungen");

            migrationBuilder.DropColumn(
                name: "groesse",
                table: "beschaffungen");
        }
    }
}
