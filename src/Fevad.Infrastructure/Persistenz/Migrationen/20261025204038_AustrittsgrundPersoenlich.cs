using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <summary>
    /// Austrittsgrund „gesundheitliche Gründe“ entfällt (Andi, 25.10.2026): bestehende Einträge werden zu „persönliche Gründe“,
    /// damit der Grund nicht auf die Gesundheit schließen lässt. Rückwärts lässt sich das nicht unterscheiden; Down ändert nichts.
    /// </summary>
    public partial class AustrittsgrundPersoenlich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE mitgliedschaften SET austrittsgrund = 'PersoenlicheGruende' WHERE austrittsgrund = 'Gesundheit';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
