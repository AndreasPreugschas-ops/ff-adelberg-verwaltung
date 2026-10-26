using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Rechtefreigaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rechtefreigaben",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rolle = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recht = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    abteilungen = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    konto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beantragt_von = table.Column<Guid>(type: "uuid", nullable: false),
                    entschieden_von = table.Column<Guid>(type: "uuid", nullable: true),
                    entschieden_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    freigegeben = table.Column<bool>(type: "boolean", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erstellt_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geaendert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    geaendert_von = table.Column<Guid>(type: "uuid", nullable: true),
                    geloescht_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rechtefreigaben", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rechtefreigaben_freigegeben",
                table: "rechtefreigaben",
                column: "freigegeben");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rechtefreigaben");
        }
    }
}
