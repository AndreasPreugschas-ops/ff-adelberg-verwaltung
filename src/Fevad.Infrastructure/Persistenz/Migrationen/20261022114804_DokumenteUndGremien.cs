using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class DokumenteUndGremien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dokument_dateien",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_dokument_dateien", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dokumentkategorien",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_dokumentkategorien", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dokumente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    beschreibung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    kategorie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sichtbarkeit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lesebestaetigung_erforderlich = table.Column<bool>(type: "boolean", nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: true),
                    aufgehoben_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dokumente", x => x.id);
                    table.ForeignKey(
                        name: "fk_dokumente_dokumentkategorien_kategorie_id",
                        column: x => x.kategorie_id,
                        principalTable: "dokumentkategorien",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dokument_versionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dokument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nummer = table.Column<int>(type: "integer", nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    groesse = table.Column<long>(type: "bigint", nullable: false),
                    pruefsumme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    datei_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aenderungshinweis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dokument_versionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_dokument_versionen_dokument_dateien_datei_id",
                        column: x => x.datei_id,
                        principalTable: "dokument_dateien",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dokument_versionen_dokumente_dokument_id",
                        column: x => x.dokument_id,
                        principalTable: "dokumente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sitzungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    beginn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ende = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    eingeladen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    tagesordnung = table.Column<string>(type: "text", nullable: true),
                    protokoll = table.Column<string>(type: "text", nullable: true),
                    protokollfuehrer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    protokoll_dokument_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    protokoll_genehmigt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    dem_buergermeister_vorgelegt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sitzungen", x => x.id);
                    table.CheckConstraint("ck_sitzungen_ende_nach_beginn", "ende IS NULL OR ende >= beginn");
                    table.ForeignKey(
                        name: "fk_sitzungen_dokumente_protokoll_dokument_id",
                        column: x => x.protokoll_dokument_id,
                        principalTable: "dokumente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sitzungen_mitglieder_protokollfuehrer_id",
                        column: x => x.protokollfuehrer_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lesebestaetigungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dokument_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    benutzer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bestaetigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesebestaetigungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesebestaetigungen_asp_net_users_benutzer_id",
                        column: x => x.benutzer_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lesebestaetigungen_dokument_versionen_dokument_version_id",
                        column: x => x.dokument_version_id,
                        principalTable: "dokument_versionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "beschluesse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sitzung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    nummer = table.Column<int>(type: "integer", nullable: false),
                    tagesordnungspunkt = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    gegenstand = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ja_stimmen = table.Column<int>(type: "integer", nullable: true),
                    nein_stimmen = table.Column<int>(type: "integer", nullable: true),
                    enthaltungen = table.Column<int>(type: "integer", nullable: true),
                    umsetzen_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    erledigt_am = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_beschluesse", x => x.id);
                    table.ForeignKey(
                        name: "fk_beschluesse_sitzungen_sitzung_id",
                        column: x => x.sitzung_id,
                        principalTable: "sitzungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sitzung_teilnahmen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sitzung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stimmberechtigt = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sitzung_teilnahmen", x => x.id);
                    table.ForeignKey(
                        name: "fk_sitzung_teilnahmen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sitzung_teilnahmen_sitzungen_sitzung_id",
                        column: x => x.sitzung_id,
                        principalTable: "sitzungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wahlen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sitzung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funktion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wahlgang = table.Column<int>(type: "integer", nullable: false),
                    verfahren = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geheim = table.Column<bool>(type: "boolean", nullable: false),
                    sitze = table.Column<int>(type: "integer", nullable: false),
                    stimmberechtigte = table.Column<int>(type: "integer", nullable: true),
                    abgegebene_stimmzettel = table.Column<int>(type: "integer", nullable: true),
                    ungueltige_stimmzettel = table.Column<int>(type: "integer", nullable: true),
                    gueltige_stimmen = table.Column<int>(type: "integer", nullable: true),
                    amtsbeginn = table.Column<DateOnly>(type: "date", nullable: true),
                    amtszeit_jahre = table.Column<int>(type: "integer", nullable: true),
                    zustimmung_gemeinderat_erforderlich = table.Column<bool>(type: "boolean", nullable: false),
                    zustimmung_gemeinderat_am = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("pk_wahlen", x => x.id);
                    table.ForeignKey(
                        name: "fk_wahlen_funktionen_funktion_id",
                        column: x => x.funktion_id,
                        principalTable: "funktionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wahlen_sitzungen_sitzung_id",
                        column: x => x.sitzung_id,
                        principalTable: "sitzungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wahl_bewerber",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wahl_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stimmen = table.Column<int>(type: "integer", nullable: false),
                    platz = table.Column<int>(type: "integer", nullable: true),
                    gewaehlt = table.Column<bool>(type: "boolean", nullable: false),
                    angenommen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wahl_bewerber", x => x.id);
                    table.ForeignKey(
                        name: "fk_wahl_bewerber_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wahl_bewerber_wahlen_wahl_id",
                        column: x => x.wahl_id,
                        principalTable: "wahlen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beschluesse_jahr_nummer",
                table: "beschluesse",
                columns: new[] { "jahr", "nummer" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_beschluesse_sitzung_id",
                table: "beschluesse",
                column: "sitzung_id");

            migrationBuilder.CreateIndex(
                name: "ix_dokument_versionen_datei_id",
                table: "dokument_versionen",
                column: "datei_id");

            migrationBuilder.CreateIndex(
                name: "ix_dokument_versionen_dokument_id_nummer",
                table: "dokument_versionen",
                columns: new[] { "dokument_id", "nummer" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_dokumente_kategorie_id",
                table: "dokumente",
                column: "kategorie_id");

            migrationBuilder.CreateIndex(
                name: "ix_dokumente_titel",
                table: "dokumente",
                column: "titel");

            migrationBuilder.CreateIndex(
                name: "ix_dokumentkategorien_code",
                table: "dokumentkategorien",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesebestaetigungen_benutzer_id",
                table: "lesebestaetigungen",
                column: "benutzer_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesebestaetigungen_dokument_version_id_benutzer_id",
                table: "lesebestaetigungen",
                columns: new[] { "dokument_version_id", "benutzer_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sitzung_teilnahmen_mitglied_id",
                table: "sitzung_teilnahmen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_sitzung_teilnahmen_sitzung_id_mitglied_id",
                table: "sitzung_teilnahmen",
                columns: new[] { "sitzung_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sitzungen_beginn",
                table: "sitzungen",
                column: "beginn");

            migrationBuilder.CreateIndex(
                name: "ix_sitzungen_protokoll_dokument_id",
                table: "sitzungen",
                column: "protokoll_dokument_id");

            migrationBuilder.CreateIndex(
                name: "ix_sitzungen_protokollfuehrer_id",
                table: "sitzungen",
                column: "protokollfuehrer_id");

            migrationBuilder.CreateIndex(
                name: "ix_wahl_bewerber_mitglied_id",
                table: "wahl_bewerber",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_wahl_bewerber_wahl_id_mitglied_id",
                table: "wahl_bewerber",
                columns: new[] { "wahl_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_wahlen_funktion_id",
                table: "wahlen",
                column: "funktion_id");

            migrationBuilder.CreateIndex(
                name: "ix_wahlen_sitzung_id",
                table: "wahlen",
                column: "sitzung_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beschluesse");

            migrationBuilder.DropTable(
                name: "lesebestaetigungen");

            migrationBuilder.DropTable(
                name: "sitzung_teilnahmen");

            migrationBuilder.DropTable(
                name: "wahl_bewerber");

            migrationBuilder.DropTable(
                name: "dokument_versionen");

            migrationBuilder.DropTable(
                name: "wahlen");

            migrationBuilder.DropTable(
                name: "dokument_dateien");

            migrationBuilder.DropTable(
                name: "sitzungen");

            migrationBuilder.DropTable(
                name: "dokumente");

            migrationBuilder.DropTable(
                name: "dokumentkategorien");
        }
    }
}
