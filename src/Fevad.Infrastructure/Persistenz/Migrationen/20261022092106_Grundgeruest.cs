using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Grundgeruest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "abteilungen",
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
                    table.PrimaryKey("pk_abteilungen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_eintraege",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zeitpunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    benutzer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tabelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    datensatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aktion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    aenderungen = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_eintraege", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dienstgrade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mindestwartezeit_monate = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_dienstgrade", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fahrzeuge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    funkrufname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    typ = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    kennzeichen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hersteller = table.Column<string>(type: "text", nullable: true),
                    aufbau = table.Column<string>(type: "text", nullable: true),
                    baujahr = table.Column<int>(type: "integer", nullable: true),
                    indienst_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ausser_dienst_am = table.Column<DateOnly>(type: "date", nullable: true),
                    issi = table.Column<string>(type: "text", nullable: true),
                    fms_kennung = table.Column<string>(type: "text", nullable: true),
                    sitzplaetze = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fahrzeuge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funktionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amtszeit_jahre = table.Column<int>(type: "integer", nullable: true),
                    vorgeschlagene_rolle = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_funktionen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "geraetearten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aussonderung_nach_jahren = table.Column<int>(type: "integer", nullable: true),
                    pruefgrundlage = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_geraetearten", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mitglieder",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personalnummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legacy_mp_feuer_id = table.Column<string>(type: "text", nullable: true),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    anrede = table.Column<string>(type: "text", nullable: true),
                    titel = table.Column<string>(type: "text", nullable: true),
                    vorname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nachname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    geburtsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    strasse = table.Column<string>(type: "text", nullable: true),
                    plz = table.Column<string>(type: "text", nullable: true),
                    ort = table.Column<string>(type: "text", nullable: true),
                    telefon_mobil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefon_festnetz = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    arbeitgeber = table.Column<string>(type: "text", nullable: true),
                    arbeitsort = table.Column<string>(type: "text", nullable: true),
                    notizen = table.Column<string>(type: "text", nullable: true),
                    fe2external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fe2sync_aktiv = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitglieder", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parameter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schluessel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    wert = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    quelle = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qualifikationen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ebene = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    gueltigkeit_monate = table.Column<int>(type: "integer", nullable: true),
                    fe2funktion = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_qualifikationen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "statistik_kategorien",
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
                    table.PrimaryKey("pk_statistik_kategorien", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geraete",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventarnummer = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bezeichnung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hersteller = table.Column<string>(type: "text", nullable: true),
                    typ = table.Column<string>(type: "text", nullable: true),
                    seriennummer = table.Column<string>(type: "text", nullable: true),
                    baujahr = table.Column<int>(type: "integer", nullable: true),
                    indienst_am = table.Column<DateOnly>(type: "date", nullable: true),
                    ausgesondert_am = table.Column<DateOnly>(type: "date", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lagerort = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geraete", x => x.id);
                    table.ForeignKey(
                        name: "fk_geraete_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_geraete_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pruefplan_positionen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraeteart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pruefart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    intervall_monate = table.Column<int>(type: "integer", nullable: false),
                    checkliste = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pruefplan_positionen", x => x.id);
                    table.ForeignKey(
                        name: "fk_pruefplan_positionen_geraetearten_geraeteart_id",
                        column: x => x.geraeteart_id,
                        principalTable: "geraetearten",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "atemschutz_nachweise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    geraetetyp = table.Column<string>(type: "text", nullable: true),
                    minuten = table.Column<int>(type: "integer", nullable: true),
                    taetigkeit = table.Column<string>(type: "text", nullable: true),
                    ort = table.Column<string>(type: "text", nullable: true),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atemschutz_nachweise", x => x.id);
                    table.ForeignKey(
                        name: "fk_atemschutz_nachweise_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "atemschutz_vorsorgen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    untersucht_am = table.Column<DateOnly>(type: "date", nullable: false),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    naechste_faellig = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("pk_atemschutz_vorsorgen", x => x.id);
                    table.ForeignKey(
                        name: "fk_atemschutz_vorsorgen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dienste",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    thema = table.Column<string>(type: "text", nullable: true),
                    beginn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ende = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ort = table.Column<string>(type: "text", nullable: true),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verantwortlicher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    zaehlt_fuer_fortbildung = table.Column<bool>(type: "boolean", nullable: false),
                    bericht = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienste", x => x.id);
                    table.CheckConstraint("ck_dienste_ende_nach_beginn", "ende >= beginn");
                    table.ForeignKey(
                        name: "fk_dienste_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dienste_mitglieder_verantwortlicher_id",
                        column: x => x.verantwortlicher_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "dienstgrad_zuordnungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dienstgrad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ab = table.Column<DateOnly>(type: "date", nullable: false),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dienstgrad_zuordnungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_dienstgrad_zuordnungen_dienstgrade_dienstgrad_id",
                        column: x => x.dienstgrad_id,
                        principalTable: "dienstgrade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dienstgrad_zuordnungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "funktion_zuordnungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funktion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    beschlussdatum = table.Column<DateOnly>(type: "date", nullable: true),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funktion_zuordnungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_funktion_zuordnungen_funktionen_funktion_id",
                        column: x => x.funktion_id,
                        principalTable: "funktionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_funktion_zuordnungen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mitgliedschaften",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    abteilung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gueltig_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    austrittsgrund = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    vordienstzeit = table.Column<bool>(type: "boolean", nullable: false),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitgliedschaften", x => x.id);
                    table.ForeignKey(
                        name: "fk_mitgliedschaften_abteilungen_abteilung_id",
                        column: x => x.abteilung_id,
                        principalTable: "abteilungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mitgliedschaften_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qualifikation_nachweise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualifikation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    erworben_am = table.Column<DateOnly>(type: "date", nullable: false),
                    gueltig_bis = table.Column<DateOnly>(type: "date", nullable: true),
                    lehrgangsort = table.Column<string>(type: "text", nullable: true),
                    stunden = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    dokument_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qualifikation_nachweise", x => x.id);
                    table.ForeignKey(
                        name: "fk_qualifikation_nachweise_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_qualifikation_nachweise_qualifikationen_qualifikation_id",
                        column: x => x.qualifikation_id,
                        principalTable: "qualifikationen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "einsaetze",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatznummer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    leitstellen_nummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    stichwort = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    stichwort_beschreibung = table.Column<string>(type: "text", nullable: true),
                    stichwort_kategorie = table.Column<string>(type: "text", nullable: true),
                    meldung = table.Column<string>(type: "text", nullable: true),
                    strasse = table.Column<string>(type: "text", nullable: true),
                    hausnummer = table.Column<string>(type: "text", nullable: true),
                    plz = table.Column<string>(type: "text", nullable: true),
                    ort = table.Column<string>(type: "text", nullable: true),
                    ortsteil = table.Column<string>(type: "text", nullable: true),
                    objekt = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    alarmiert = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ausgerueckt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eintreffen_einsatzort = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    einsatzende = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    einsatzbereitschaft_hergestellt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    statistik_kategorie_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rechtsgrundlage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    einsatzleiter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lagebericht = table.Column<string>(type: "text", nullable: true),
                    massnahmen = table.Column<string>(type: "text", nullable: true),
                    gerettete = table.Column<int>(type: "integer", nullable: false),
                    verletzte = table.Column<int>(type: "integer", nullable: false),
                    tote = table.Column<int>(type: "integer", nullable: false),
                    verletzte_feuerwehr = table.Column<int>(type: "integer", nullable: false),
                    ueberoertlich = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    freigegeben_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    freigegeben_von = table.Column<Guid>(type: "uuid", nullable: true),
                    erfasst_im_notbetrieb = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsaetze", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsaetze_mitglieder_einsatzleiter_id",
                        column: x => x.einsatzleiter_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_einsaetze_statistik_kategorien_statistik_kategorie_id",
                        column: x => x.statistik_kategorie_id,
                        principalTable: "statistik_kategorien",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maengel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gemeldet_von_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gemeldet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    beschreibung = table.Column<string>(type: "text", nullable: false),
                    schwere = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    behoben_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pruefung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maengel", x => x.id);
                    table.CheckConstraint("ck_maengel_geraet_oder_fahrzeug", "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_maengel_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_maengel_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_maengel_mitglieder_gemeldet_von_id",
                        column: x => x.gemeldet_von_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pruefungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geraet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pruefplan_position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pruefart = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    datum = table.Column<DateOnly>(type: "date", nullable: false),
                    ergebnis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ergebnisse = table.Column<string>(type: "jsonb", nullable: false),
                    naechste_faellig = table.Column<DateOnly>(type: "date", nullable: true),
                    pruefer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pruefer_extern = table.Column<string>(type: "text", nullable: true),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    abgeschlossen = table.Column<bool>(type: "boolean", nullable: false),
                    erfasst_im_notbetrieb = table.Column<bool>(type: "boolean", nullable: false),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pruefungen", x => x.id);
                    table.CheckConstraint("ck_pruefungen_geraet_oder_fahrzeug", "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_pruefungen_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pruefungen_geraete_geraet_id",
                        column: x => x.geraet_id,
                        principalTable: "geraete",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pruefungen_mitglieder_pruefer_id",
                        column: x => x.pruefer_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_pruefungen_pruefplan_positionen_pruefplan_position_id",
                        column: x => x.pruefplan_position_id,
                        principalTable: "pruefplan_positionen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "anwesenheiten",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dienst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    von = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bis = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    funktion = table.Column<string>(type: "text", nullable: true),
                    erfasst_ueber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bemerkung = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anwesenheiten", x => x.id);
                    table.ForeignKey(
                        name: "fk_anwesenheiten_dienste_dienst_id",
                        column: x => x.dienst_id,
                        principalTable: "dienste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anwesenheiten_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alarm_importe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quelle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    externe_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    empfangen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rohdaten = table.Column<string>(type: "jsonb", nullable: false),
                    verarbeitet = table.Column<bool>(type: "boolean", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    loeschen_ab = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alarm_importe", x => x.id);
                    table.ForeignKey(
                        name: "fk_alarm_importe_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "einsatz_fahrzeuge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ausgerueckt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eingetroffen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    zurueck = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    einsatzbereit = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kilometer = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatz_fahrzeuge", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_fahrzeuge_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatz_fahrzeuge_fahrzeuge_fahrzeug_id",
                        column: x => x.fahrzeug_id,
                        principalTable: "fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "einsatz_teilnahmen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mitglied_id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_fahrzeug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funktion = table.Column<string>(type: "text", nullable: true),
                    von = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bis = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    atemschutz = table.Column<bool>(type: "boolean", nullable: false),
                    atemschutz_minuten = table.Column<int>(type: "integer", nullable: true),
                    quelle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bestaetigt = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_einsatz_teilnahmen", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_teilnahmen_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_einsatz_teilnahmen_einsatz_fahrzeuge_einsatz_fahrzeug_id",
                        column: x => x.einsatz_fahrzeug_id,
                        principalTable: "einsatz_fahrzeuge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_einsatz_teilnahmen_mitglieder_mitglied_id",
                        column: x => x.mitglied_id,
                        principalTable: "mitglieder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_abteilungen_code",
                table: "abteilungen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alarm_importe_einsatz_id",
                table: "alarm_importe",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_alarm_importe_loeschen_ab",
                table: "alarm_importe",
                column: "loeschen_ab");

            migrationBuilder.CreateIndex(
                name: "ix_alarm_importe_quelle_externe_id",
                table: "alarm_importe",
                columns: new[] { "quelle", "externe_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_anwesenheiten_dienst_id_mitglied_id",
                table: "anwesenheiten",
                columns: new[] { "dienst_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_anwesenheiten_mitglied_id",
                table: "anwesenheiten",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atemschutz_nachweise_mitglied_id_art_datum",
                table: "atemschutz_nachweise",
                columns: new[] { "mitglied_id", "art", "datum" });

            migrationBuilder.CreateIndex(
                name: "ix_atemschutz_vorsorgen_mitglied_id",
                table: "atemschutz_vorsorgen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_eintraege_tabelle_datensatz_id",
                table: "audit_eintraege",
                columns: new[] { "tabelle", "datensatz_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_eintraege_zeitpunkt",
                table: "audit_eintraege",
                column: "zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_abteilung_id",
                table: "dienste",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_beginn",
                table: "dienste",
                column: "beginn");

            migrationBuilder.CreateIndex(
                name: "ix_dienste_verantwortlicher_id",
                table: "dienste",
                column: "verantwortlicher_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienstgrad_zuordnungen_dienstgrad_id",
                table: "dienstgrad_zuordnungen",
                column: "dienstgrad_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienstgrad_zuordnungen_mitglied_id",
                table: "dienstgrad_zuordnungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_dienstgrade_code",
                table: "dienstgrade",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_alarmiert",
                table: "einsaetze",
                column: "alarmiert");

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_einsatzleiter_id",
                table: "einsaetze",
                column: "einsatzleiter_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_einsatznummer",
                table: "einsaetze",
                column: "einsatznummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_leitstellen_nummer",
                table: "einsaetze",
                column: "leitstellen_nummer");

            migrationBuilder.CreateIndex(
                name: "ix_einsaetze_statistik_kategorie_id",
                table: "einsaetze",
                column: "statistik_kategorie_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_fahrzeuge_einsatz_id",
                table: "einsatz_fahrzeuge",
                column: "einsatz_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_fahrzeuge_fahrzeug_id",
                table: "einsatz_fahrzeuge",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_teilnahmen_einsatz_fahrzeug_id",
                table: "einsatz_teilnahmen",
                column: "einsatz_fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_teilnahmen_einsatz_id_mitglied_id",
                table: "einsatz_teilnahmen",
                columns: new[] { "einsatz_id", "mitglied_id" },
                unique: true,
                filter: "geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_teilnahmen_mitglied_id",
                table: "einsatz_teilnahmen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_fahrzeuge_funkrufname",
                table: "fahrzeuge",
                column: "funkrufname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funktion_zuordnungen_funktion_id",
                table: "funktion_zuordnungen",
                column: "funktion_id");

            migrationBuilder.CreateIndex(
                name: "ix_funktion_zuordnungen_mitglied_id",
                table: "funktion_zuordnungen",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_funktionen_code",
                table: "funktionen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_geraete_fahrzeug_id",
                table: "geraete",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_geraete_geraeteart_id",
                table: "geraete",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_geraete_inventarnummer",
                table: "geraete",
                column: "inventarnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_geraetearten_code",
                table: "geraetearten",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maengel_fahrzeug_id",
                table: "maengel",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_gemeldet_von_id",
                table: "maengel",
                column: "gemeldet_von_id");

            migrationBuilder.CreateIndex(
                name: "ix_maengel_geraet_id",
                table: "maengel",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_mitglieder_fe2external_id",
                table: "mitglieder",
                column: "fe2external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mitglieder_nachname_vorname",
                table: "mitglieder",
                columns: new[] { "nachname", "vorname" });

            migrationBuilder.CreateIndex(
                name: "ix_mitglieder_personalnummer",
                table: "mitglieder",
                column: "personalnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mitgliedschaften_abteilung_id",
                table: "mitgliedschaften",
                column: "abteilung_id");

            migrationBuilder.CreateIndex(
                name: "ix_mitgliedschaften_mitglied_id_abteilung_id",
                table: "mitgliedschaften",
                columns: new[] { "mitglied_id", "abteilung_id" },
                unique: true,
                filter: "gueltig_bis IS NULL AND geloescht_am IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_schluessel_gueltig_ab",
                table: "parameter",
                columns: new[] { "schluessel", "gueltig_ab" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pruefplan_positionen_geraeteart_id",
                table: "pruefplan_positionen",
                column: "geraeteart_id");

            migrationBuilder.CreateIndex(
                name: "ix_pruefungen_fahrzeug_id",
                table: "pruefungen",
                column: "fahrzeug_id");

            migrationBuilder.CreateIndex(
                name: "ix_pruefungen_geraet_id",
                table: "pruefungen",
                column: "geraet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pruefungen_naechste_faellig",
                table: "pruefungen",
                column: "naechste_faellig");

            migrationBuilder.CreateIndex(
                name: "ix_pruefungen_pruefer_id",
                table: "pruefungen",
                column: "pruefer_id");

            migrationBuilder.CreateIndex(
                name: "ix_pruefungen_pruefplan_position_id",
                table: "pruefungen",
                column: "pruefplan_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_qualifikation_nachweise_gueltig_bis",
                table: "qualifikation_nachweise",
                column: "gueltig_bis");

            migrationBuilder.CreateIndex(
                name: "ix_qualifikation_nachweise_mitglied_id",
                table: "qualifikation_nachweise",
                column: "mitglied_id");

            migrationBuilder.CreateIndex(
                name: "ix_qualifikation_nachweise_qualifikation_id",
                table: "qualifikation_nachweise",
                column: "qualifikation_id");

            migrationBuilder.CreateIndex(
                name: "ix_qualifikationen_code",
                table: "qualifikationen",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_statistik_kategorien_code",
                table: "statistik_kategorien",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alarm_importe");

            migrationBuilder.DropTable(
                name: "anwesenheiten");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "atemschutz_nachweise");

            migrationBuilder.DropTable(
                name: "atemschutz_vorsorgen");

            migrationBuilder.DropTable(
                name: "audit_eintraege");

            migrationBuilder.DropTable(
                name: "dienstgrad_zuordnungen");

            migrationBuilder.DropTable(
                name: "einsatz_teilnahmen");

            migrationBuilder.DropTable(
                name: "funktion_zuordnungen");

            migrationBuilder.DropTable(
                name: "maengel");

            migrationBuilder.DropTable(
                name: "mitgliedschaften");

            migrationBuilder.DropTable(
                name: "parameter");

            migrationBuilder.DropTable(
                name: "pruefungen");

            migrationBuilder.DropTable(
                name: "qualifikation_nachweise");

            migrationBuilder.DropTable(
                name: "dienste");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "dienstgrade");

            migrationBuilder.DropTable(
                name: "einsatz_fahrzeuge");

            migrationBuilder.DropTable(
                name: "funktionen");

            migrationBuilder.DropTable(
                name: "geraete");

            migrationBuilder.DropTable(
                name: "pruefplan_positionen");

            migrationBuilder.DropTable(
                name: "qualifikationen");

            migrationBuilder.DropTable(
                name: "abteilungen");

            migrationBuilder.DropTable(
                name: "einsaetze");

            migrationBuilder.DropTable(
                name: "fahrzeuge");

            migrationBuilder.DropTable(
                name: "geraetearten");

            migrationBuilder.DropTable(
                name: "mitglieder");

            migrationBuilder.DropTable(
                name: "statistik_kategorien");
        }
    }
}
