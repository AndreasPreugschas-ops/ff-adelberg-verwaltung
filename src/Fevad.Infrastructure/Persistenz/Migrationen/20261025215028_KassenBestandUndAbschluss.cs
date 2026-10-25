using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Bestandsverzeichnis, Jahresabschluss mit Endbeständen und Ausnahmebuchungen der Kassen (V04, V05).</summary>
    public partial class KassenBestandUndAbschluss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ausnahme_begruendung",
                table: "buchungen",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "kein_gegenstand",
                table: "buchungen",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "jahresabschluesse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    abgeschlossen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    abgeschlossen_von = table.Column<Guid>(type: "uuid", nullable: true),
                    einnahmen = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ausgaben = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vorgelegt_am = table.Column<DateOnly>(type: "date", nullable: true),
                    entlastung_beschluss_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_jahresabschluesse", x => x.id);
                    table.ForeignKey(
                        name: "fk_jahresabschluesse_beschluesse_entlastung_beschluss_id",
                        column: x => x.entlastung_beschluss_id,
                        principalTable: "beschluesse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_jahresabschluesse_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vermoegensgegenstaende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kasse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventarnummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anschaffung = table.Column<DateOnly>(type: "date", nullable: false),
                    wert = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    buchung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    standort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    abgang_am = table.Column<DateOnly>(type: "date", nullable: true),
                    abgang_grund = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    erloes = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
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
                    table.PrimaryKey("pk_vermoegensgegenstaende", x => x.id);
                    table.CheckConstraint("ck_vermoegensgegenstaende_abgang", "abgang_am IS NULL OR abgang_am >= anschaffung");
                    table.CheckConstraint("ck_vermoegensgegenstaende_wert", "wert >= 0");
                    table.ForeignKey(
                        name: "fk_vermoegensgegenstaende_buchungen_buchung_id",
                        column: x => x.buchung_id,
                        principalTable: "buchungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vermoegensgegenstaende_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vermoegensgegenstaende_kassen_kasse_id",
                        column: x => x.kasse_id,
                        principalTable: "kassen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kontoendbestaende",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahresabschluss_id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bestand = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kontoendbestaende", x => x.id);
                    table.ForeignKey(
                        name: "fk_kontoendbestaende_jahresabschluesse_jahresabschluss_id",
                        column: x => x.jahresabschluss_id,
                        principalTable: "jahresabschluesse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kontoendbestaende_zahlungsmittelkonten_konto_id",
                        column: x => x.konto_id,
                        principalTable: "zahlungsmittelkonten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jahresabschluesse_entlastung_beschluss_id",
                table: "jahresabschluesse",
                column: "entlastung_beschluss_id");

            migrationBuilder.CreateIndex(
                name: "ix_jahresabschluesse_kasse_id_jahr",
                table: "jahresabschluesse",
                columns: new[] { "kasse_id", "jahr" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_kontoendbestaende_jahresabschluss_id",
                table: "kontoendbestaende",
                column: "jahresabschluss_id");

            migrationBuilder.CreateIndex(
                name: "ix_kontoendbestaende_konto_id",
                table: "kontoendbestaende",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_vermoegensgegenstaende_buchung_id",
                table: "vermoegensgegenstaende",
                column: "buchung_id");

            migrationBuilder.CreateIndex(
                name: "ix_vermoegensgegenstaende_geraet_id",
                table: "vermoegensgegenstaende",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_vermoegensgegenstaende_kasse_id_anschaffung",
                table: "vermoegensgegenstaende",
                columns: new[] { "kasse_id", "anschaffung" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kontoendbestaende");

            migrationBuilder.DropTable(
                name: "vermoegensgegenstaende");

            migrationBuilder.DropTable(
                name: "jahresabschluesse");

            migrationBuilder.DropColumn(
                name: "ausnahme_begruendung",
                table: "buchungen");

            migrationBuilder.DropColumn(
                name: "kein_gegenstand",
                table: "buchungen");
        }
    }
}
