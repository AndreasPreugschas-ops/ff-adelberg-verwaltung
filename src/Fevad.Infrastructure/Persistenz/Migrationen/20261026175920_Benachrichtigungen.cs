using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Benachrichtigungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sammelmail_ohne",
                table: "AspNetUsers",
                type: "integer",
                maxLength: 40,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "benachrichtigungen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empfaenger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    art = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    bezug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erstellt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    gelesen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    gemailt_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_benachrichtigungen", x => x.id);
                    table.ForeignKey(
                        name: "fk_benachrichtigungen_asp_net_users_empfaenger_id",
                        column: x => x.empfaenger_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_benachrichtigungen_bezug_id",
                table: "benachrichtigungen",
                column: "bezug_id");

            migrationBuilder.CreateIndex(
                name: "ix_benachrichtigungen_empfaenger_id_gelesen_am",
                table: "benachrichtigungen",
                columns: new[] { "empfaenger_id", "gelesen_am" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benachrichtigungen");

            migrationBuilder.DropColumn(
                name: "sammelmail_ohne",
                table: "AspNetUsers");
        }
    }
}
