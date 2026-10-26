using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Einsaetze;

/// <summary>
/// Maßnahmen aus Einsatznachbesprechungen (V17). Kommandant oder Stellvertreter geben frei; ein freigegebener Mangel wird zur Mängelmeldung,
/// die die Gerätewarte in ihrer Mängelliste sehen (Andi zu V17).
/// </summary>
/// <param name="db">Datenbankkontext.</param>
public sealed class Massnahmenliste(AppDbContext db)
{
    /// <summary>Gibt eine vorgeschlagene Maßnahme frei und legt bei einem Mangel an Fahrzeug oder Gerät die Mängelmeldung an.</summary>
    /// <param name="massnahmeId">Kennung der Maßnahme.</param>
    /// <param name="benutzerId">Konto, das freigibt.</param>
    /// <param name="jetzt">Zeitpunkt der Freigabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn freigegeben wurde; <c>false</c>, wenn die Maßnahme fehlt oder nicht mehr vorgeschlagen ist.</returns>
    public async Task<bool> FreigebenAsync(Guid massnahmeId, Guid benutzerId, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        var massnahme = await db.Massnahmen.Include(m => m.Nachbesprechung).FirstOrDefaultAsync(m => m.Id == massnahmeId, ct);
        if (massnahme is not { Status: MassnahmeStatus.Vorgeschlagen })
        {
            return false;
        }

        massnahme.Freigeben(benutzerId, jetzt);
        if (massnahme.Art == MassnahmeArt.Mangel && (massnahme.FahrzeugId is not null || massnahme.GeraetId is not null))
        {
            var mangel = new Mangel
            {
                FahrzeugId = massnahme.GeraetId is null ? massnahme.FahrzeugId : null,
                GeraetId = massnahme.GeraetId,
                EinsatzId = massnahme.Nachbesprechung?.EinsatzId,
                Beschreibung = $"Aus der Einsatznachbesprechung: {massnahme.Titel}",
                Schwere = MangelSchwere.Einschraenkung,
                Status = MangelStatus.Offen,
                GemeldetAm = jetzt,
            };
            db.Maengel.Add(mangel);
            massnahme.MangelId = mangel.Id;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Offene Maßnahmen (vorgeschlagen oder freigegeben) mit Einsatz, Verantwortlichem und Ausrüstung.</summary>
    /// <param name="nurVerantwortlich">Nur die freigegebenen Maßnahmen dieses Mitglieds; <c>null</c> für alle.</param>
    /// <returns>Abfrage, nach Termin sortiert.</returns>
    public IQueryable<Massnahme> Offene(Guid? nurVerantwortlich = null) =>
        db.Massnahmen.AsNoTracking()
            .Include(m => m.Nachbesprechung!).ThenInclude(n => n.Einsatz)
            .Include(m => m.Verantwortlich)
            .Include(m => m.Fahrzeug)
            .Include(m => m.Geraet)
            .Where(m => nurVerantwortlich == null
                ? m.Status == MassnahmeStatus.Vorgeschlagen || m.Status == MassnahmeStatus.Freigegeben
                : m.Status == MassnahmeStatus.Freigegeben && m.VerantwortlichId == nurVerantwortlich)
            .OrderBy(m => m.Status).ThenBy(m => m.ErledigenBis == null).ThenBy(m => m.ErledigenBis);
}
