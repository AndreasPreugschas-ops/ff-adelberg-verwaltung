using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>Leitungspauschale je geleitetem Dienst (Übungsleiter, Entschädigungssatzung Adelberg 2018, V35).</summary>
    public partial class EntschaedigungssatzungAdelberg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "geleiteter_dienst_id",
                table: "abrechnungs_posten",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_abrechnungs_posten_geleiteter_dienst_id",
                table: "abrechnungs_posten",
                column: "geleiteter_dienst_id");

            migrationBuilder.AddForeignKey(
                name: "fk_abrechnungs_posten_dienste_geleiteter_dienst_id",
                table: "abrechnungs_posten",
                column: "geleiteter_dienst_id",
                principalTable: "dienste",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_abrechnungs_posten_dienste_geleiteter_dienst_id",
                table: "abrechnungs_posten");

            migrationBuilder.DropIndex(
                name: "ix_abrechnungs_posten_geleiteter_dienst_id",
                table: "abrechnungs_posten");

            migrationBuilder.DropColumn(
                name: "geleiteter_dienst_id",
                table: "abrechnungs_posten");
        }
    }
}
