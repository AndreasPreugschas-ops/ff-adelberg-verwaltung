using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>
    /// Gerätewarte sehen von sich aus keine Personaldaten (Andi, 2026-10-25). Die Vorbelegung in Berechtigungen.cs gilt nur
    /// für neue Datenbanken; hier werden die beiden Rechte auch in bestehenden Datenbanken aus der Rolle genommen.
    /// Wer sie dem Gerätewart bewusst gegeben hat, trägt sie in der Rechtematrix wieder ein.
    /// </summary>
    public partial class GeraetewartOhnePersonalakte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM rollen_rechte WHERE rolle = 'Geraetewart' AND recht IN ('Ausbildung.Lesen', 'Ausbildung.Nachweise');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die alte Vorbelegung lässt sich bei Bedarf in der Rechtematrix wiederherstellen.
        }
    }
}
