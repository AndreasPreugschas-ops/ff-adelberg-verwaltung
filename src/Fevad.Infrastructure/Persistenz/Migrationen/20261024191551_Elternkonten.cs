using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Elternkonten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "datenschutzinformation_am",
                table: "erziehungsberechtigte",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "keine_kontozuordnung",
                table: "erziehungsberechtigte",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "konto_id",
                table: "erziehungsberechtigte",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "konto_zugeordnet_am",
                table: "erziehungsberechtigte",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_erziehungsberechtigte_konto_id",
                table: "erziehungsberechtigte",
                column: "konto_id");

            migrationBuilder.AddForeignKey(
                name: "fk_erziehungsberechtigte_asp_net_users_konto_id",
                table: "erziehungsberechtigte",
                column: "konto_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_erziehungsberechtigte_asp_net_users_konto_id",
                table: "erziehungsberechtigte");

            migrationBuilder.DropIndex(
                name: "ix_erziehungsberechtigte_konto_id",
                table: "erziehungsberechtigte");

            migrationBuilder.DropColumn(
                name: "datenschutzinformation_am",
                table: "erziehungsberechtigte");

            migrationBuilder.DropColumn(
                name: "keine_kontozuordnung",
                table: "erziehungsberechtigte");

            migrationBuilder.DropColumn(
                name: "konto_id",
                table: "erziehungsberechtigte");

            migrationBuilder.DropColumn(
                name: "konto_zugeordnet_am",
                table: "erziehungsberechtigte");
        }
    }
}
