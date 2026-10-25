using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Wiedereintritt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "wiedereintritt_von_id",
                table: "antraege",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_antraege_wiedereintritt_von_id",
                table: "antraege",
                column: "wiedereintritt_von_id");

            migrationBuilder.AddForeignKey(
                name: "fk_antraege_mitglieder_wiedereintritt_von_id",
                table: "antraege",
                column: "wiedereintritt_von_id",
                principalTable: "mitglieder",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_antraege_mitglieder_wiedereintritt_von_id",
                table: "antraege");

            migrationBuilder.DropIndex(
                name: "ix_antraege_wiedereintritt_von_id",
                table: "antraege");

            migrationBuilder.DropColumn(
                name: "wiedereintritt_von_id",
                table: "antraege");
        }
    }
}
