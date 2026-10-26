using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class HaushaltIst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "haushalt_ist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jahr = table.Column<int>(type: "integer", nullable: false),
                    sachkonto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kostenstelle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    innenauftrag = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ist = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    obligo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    stand = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_haushalt_ist", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_haushalt_ist_jahr",
                table: "haushalt_ist",
                column: "jahr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "haushalt_ist");
        }
    }
}
