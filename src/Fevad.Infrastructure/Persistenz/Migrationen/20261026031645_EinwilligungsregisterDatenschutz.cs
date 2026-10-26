using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinwilligungsregisterDatenschutz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "einwilligungsarten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nur_jugend = table.Column<bool>(type: "boolean", nullable: false),
                    foto = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_einwilligungsarten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loeschanfragen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    gestellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erledigt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    antwort = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    erledigt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loeschanfragen", x => x.id);
                    table.ForeignKey(
                        name: "fk_loeschanfragen_asp_net_users_konto_id",
                        column: x => x.konto_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_loeschanfragen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "einwilligungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    einwilligungsart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    erteilt = table.Column<bool>(type: "boolean", nullable: false),
                    am = table.Column<DateOnly>(type: "date", nullable: false),
                    durch = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    durch_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    weg = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nachweis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    jugendlicher_zugestimmt = table.Column<bool>(type: "boolean", nullable: true),
                    widerrufen_am = table.Column<DateOnly>(type: "date", nullable: true),
                    widerrufen_durch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einwilligungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_einwilligungen_asp_net_users_konto_id",
                        column: x => x.konto_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_einwilligungen_einwilligungsarten_einwilligungsart_id",
                        column: x => x.einwilligungsart_id,
                        principalTable: "einwilligungsarten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_einwilligungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einwilligungen_einwilligungsart_id",
                table: "einwilligungen",
                column: "einwilligungsart_id");

            migrationBuilder.CreateIndex(
                name: "ix_einwilligungen_konto_id",
                table: "einwilligungen",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_einwilligungen_mitglied_id_einwilligungsart_id",
                table: "einwilligungen",
                columns: new[] { "mitglied_id", "einwilligungsart_id" });

            migrationBuilder.CreateIndex(
                name: "ix_einwilligungsarten_code",
                table: "einwilligungsarten",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loeschanfragen_erledigt_am",
                table: "loeschanfragen",
                column: "erledigt_am");

            migrationBuilder.CreateIndex(
                name: "ix_loeschanfragen_konto_id",
                table: "loeschanfragen",
                column: "konto_id");

            migrationBuilder.CreateIndex(
                name: "ix_loeschanfragen_mitglied_id",
                table: "loeschanfragen",
                column: "mitglied_id");

            // Die Foto-Arten mit festen Schlüsseln anlegen (wie Stammdaten.Einwilligungsarten) und die bisherige Foto-Einwilligung
            // des Mitglieds auf jeden Foto-Kanal übertragen; minderjährig am Tag der Einwilligung heißt: von den Eltern erklärt.
            migrationBuilder.Sql("""
                INSERT INTO einwilligungsarten (id, code, bezeichnung, sortierung, aktiv, foto, nur_jugend, beschreibung, erstellt_am) VALUES
                ('01980000-0000-7000-8000-00000000f001', 'FOTO-HP', 'Fotos auf der Homepage', 10, true, true, false,
                 'Fotos, auf denen die Person zu erkennen ist, dürfen auf der Homepage der Feuerwehr veröffentlicht werden.', now()),
                ('01980000-0000-7000-8000-00000000f002', 'FOTO-AB', 'Fotos im Amtsblatt', 20, true, true, false,
                 'Fotos dürfen im Amtsblatt der Gemeinde veröffentlicht werden.', now()),
                ('01980000-0000-7000-8000-00000000f003', 'FOTO-SM', 'Fotos in sozialen Medien', 30, true, true, false,
                 'Fotos dürfen auf den Social-Media-Kanälen der Feuerwehr (z. B. Instagram, Facebook) veröffentlicht werden.', now()),
                ('01980000-0000-7000-8000-00000000f004', 'FOTO-PR', 'Fotos für Presse und Kreisfeuerwehrverband', 40, true, true, false,
                 'Fotos dürfen an die Presse und den Kreisfeuerwehrverband Göppingen weitergegeben werden.', now())
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO einwilligungen (id, mitglied_id, einwilligungsart_id, erteilt, am, durch, weg, nachweis, widerrufen_am, erstellt_am)
                SELECT gen_random_uuid(), m.id, a.id, true, m.foto_einwilligung_am,
                       CASE WHEN m.foto_einwilligung_am < (m.geburtsdatum + interval '18 years')::date THEN 'Erziehungsberechtigter' ELSE 'Mitglied' END,
                       'Papier', 'übernommen aus der bisherigen Foto-Einwilligung', m.foto_widerruf_am, now()
                FROM mitglieder m CROSS JOIN einwilligungsarten a
                WHERE m.foto_einwilligung_am IS NOT NULL AND a.foto;
                """);

            migrationBuilder.DropColumn(
                name: "foto_einwilligung_am",
                table: "mitglieder");

            migrationBuilder.DropColumn(
                name: "foto_widerruf_am",
                table: "mitglieder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einwilligungen");

            migrationBuilder.DropTable(
                name: "loeschanfragen");

            migrationBuilder.DropTable(
                name: "einwilligungsarten");

            migrationBuilder.AddColumn<DateOnly>(
                name: "foto_einwilligung_am",
                table: "mitglieder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "foto_widerruf_am",
                table: "mitglieder",
                type: "date",
                nullable: true);
        }
    }
}
