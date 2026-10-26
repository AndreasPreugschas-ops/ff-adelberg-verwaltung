using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class EinsatzberichtRueckmeldung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "korrekturhinweis",
                table: "einsatz_teilnahmen",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rueckmeldung_am",
                table: "einsatz_teilnahmen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "einsatz_anhaenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    einsatz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    dateiname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inhaltstyp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    groesse = table.Column<long>(type: "bigint", nullable: false),
                    beschreibung = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_einsatz_anhaenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_einsatz_anhaenge_einsaetze_einsatz_id",
                        column: x => x.einsatz_id,
                        principalTable: "einsaetze",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_einsatz_anhaenge_einsatz_id",
                table: "einsatz_anhaenge",
                column: "einsatz_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "einsatz_anhaenge");

            migrationBuilder.DropColumn(
                name: "korrekturhinweis",
                table: "einsatz_teilnahmen");

            migrationBuilder.DropColumn(
                name: "rueckmeldung_am",
                table: "einsatz_teilnahmen");
        }
    }
}
