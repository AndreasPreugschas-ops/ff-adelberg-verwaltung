using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>
    /// Ersetzt „Hauptamt“ durch „Gemeindeverwaltung“ in den gespeicherten Anzeigetexten (Andi, 05.11.2026): Anzeigename der Rolle in den
    /// Rollenprofilen und die Quellenangaben der mitgelieferten Parameter. Der technische Rollenname „Hauptamt“ und damit alle
    /// Rollenzuordnungen der Konten und die Rechte je Rolle bleiben unverändert. Vom Administrator selbst vergebene Anzeigenamen
    /// werden nicht überschrieben.
    /// </summary>
    public partial class GemeindeverwaltungStattHauptamt : Migration
    {
        private const string AlterAnzeigename = "Gemeinde: Hauptamt";
        private const string NeuerAnzeigename = "Gemeinde: Verwaltung (Rathaus)";

        /// <summary>Stellt Anzeigenamen und Quellenangaben auf „Gemeindeverwaltung“ um.</summary>
        /// <param name="migrationBuilder">Baut die SQL-Befehle der Migration.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE rollenprofile SET bezeichnung = '{NeuerAnzeigename}'
                WHERE name = 'Hauptamt' AND bezeichnung = '{AlterAnzeigename}'
                  AND NOT EXISTS (SELECT 1 FROM rollenprofile p WHERE p.bezeichnung = '{NeuerAnzeigename}');
                """);
            migrationBuilder.Sql("""
                UPDATE parameter SET quelle = 'Festlegung FF Adelberg (vorläufig, mit der Gemeindeverwaltung abstimmen)'
                WHERE quelle = 'Festlegung FF Adelberg (vorläufig, mit Hauptamt abstimmen)';
                UPDATE parameter SET quelle = 'Mustertext FEVAD (von der Gemeindeverwaltung anzupassen)'
                WHERE quelle = 'Mustertext FEVAD (vom Hauptamt anzupassen)';
                """);
        }

        /// <summary>Stellt die früheren Anzeigenamen und Quellenangaben wieder her.</summary>
        /// <param name="migrationBuilder">Baut die SQL-Befehle der Migration.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE rollenprofile SET bezeichnung = '{AlterAnzeigename}'
                WHERE name = 'Hauptamt' AND bezeichnung = '{NeuerAnzeigename}';
                """);
            migrationBuilder.Sql("""
                UPDATE parameter SET quelle = 'Festlegung FF Adelberg (vorläufig, mit Hauptamt abstimmen)'
                WHERE quelle = 'Festlegung FF Adelberg (vorläufig, mit der Gemeindeverwaltung abstimmen)';
                UPDATE parameter SET quelle = 'Mustertext FEVAD (vom Hauptamt anzupassen)'
                WHERE quelle = 'Mustertext FEVAD (von der Gemeindeverwaltung anzupassen)';
                """);
        }
    }
}
