using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AtemschutzwartOhneMitgliederakte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // E14 (Andi, 26.10.2026): der Atemschutzgerätewart sieht nur den Atemschutzteil und die Kontaktdaten der Geräteträger.
            migrationBuilder.Sql("DELETE FROM rollen_rechte WHERE rolle = 'Atemschutzgeraetewart' AND recht IN ('Mitglieder.Lesen', 'Ausbildung.Lesen');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die Rechte vergibt bei Bedarf wieder die Rechtematrix.
        }
    }
}
