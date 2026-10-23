using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fevad.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class LehrgangsVoraussetzungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bestehende Datenbanken: Lehrgänge bekommen die vorgeschlagenen Voraussetzungen, soweit noch nichts eingetragen ist.
            // Neue Lehrgänge (TH, MOTS, ABC1, VF, LDF, AUSB, JGL, JFW) legt die Initialisierung beim Start an.
            migrationBuilder.Sql("""
                UPDATE qualifikationen q SET voraussetzungen = v.voraussetzungen
                FROM (VALUES ('TM2', 'TM1'), ('SPRF', 'TM1'), ('AGT', 'TM1'), ('MA', 'TM1'), ('TF', 'TM2'), ('GF', 'TF'), ('ZF', 'GF'), ('AGW', 'AGT'))
                    AS v(code, voraussetzungen)
                WHERE q.code = v.code AND q.voraussetzungen IS NULL;
                UPDATE qualifikationen SET mindestalter_jahre = 18
                WHERE code IN ('AGT', 'MA') AND mindestalter_jahre IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die Werte sind danach Katalogdaten wie alle anderen und bleiben stehen.
        }
    }
}
