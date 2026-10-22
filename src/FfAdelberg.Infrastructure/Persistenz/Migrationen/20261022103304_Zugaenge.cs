using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FfAdelberg.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Zugaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deaktiviert_am",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "letzte_anmeldung_am",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_mitglied_id",
                table: "AspNetUsers",
                column: "mitglied_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_users_mitglieder_mitglied_id",
                table: "AspNetUsers",
                column: "mitglied_id",
                principalTable: "mitglieder",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_users_mitglieder_mitglied_id",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_mitglied_id",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "deaktiviert_am",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "letzte_anmeldung_am",
                table: "AspNetUsers");
        }
    }
}
