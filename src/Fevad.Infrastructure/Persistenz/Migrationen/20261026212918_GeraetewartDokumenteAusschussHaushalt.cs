using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>
    /// Der Gerätewart darf Dokumente hochladen (Andi, 2026-10-26). Die Vorbelegung in Berechtigungen.cs gilt nur für Rechte, die eine
    /// Datenbank noch nicht kennt; hier bekommt die Rolle das Recht auch in bestehenden Datenbanken. Die neue Rolle Ausschussmitglied
    /// erhält ihre Vorbelegung beim Start von selbst.
    /// </summary>
    public partial class GeraetewartDokumenteAusschussHaushalt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO rollen_rechte (id, rolle, recht, abteilungen, erstellt_am)
                SELECT gen_random_uuid(), 'Geraetewart', 'Dokumente.Verwalten', '{}', now()
                WHERE EXISTS (SELECT 1 FROM rechte_eingefuehrt WHERE recht = 'Dokumente.Verwalten')
                  AND NOT EXISTS (SELECT 1 FROM rollen_rechte WHERE rolle = 'Geraetewart' AND recht = 'Dokumente.Verwalten' AND geloescht_am IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Wer das Recht wieder entziehen will, nimmt es in der Rechtematrix heraus.
        }
    }
}
