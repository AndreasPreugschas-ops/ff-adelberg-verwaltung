using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Gewählte Funktionsträger mit Amtszeitende; warnt rechtzeitig vor der nächsten Wahl.</summary>
public class AemterModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Aktuelle Funktionsträger, nach Funktion sortiert, mit Ampel zum Amtszeitende.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Wählbare Funktionen, die derzeit niemand innehat.</summary>
    public IReadOnlyList<string> Unbesetzt { get; private set; } = [];
    /// <summary>Vorwarnzeit in Tagen vor dem Amtszeitende (Parameter <c>AmtszeitWarnungTage</c>).</summary>
    public int WarnungTage { get; private set; }

    /// <summary>Eine besetzte Funktion in der Übersicht.</summary>
    /// <param name="Funktion">Bezeichnung der Funktion.</param>
    /// <param name="Sortierung">Sortierschlüssel der Funktion.</param>
    /// <param name="Mitglied">Name des Funktionsträgers („Nachname, Vorname“).</param>
    /// <param name="Art">Wie die Funktion vergeben wird (Wahl, Bestellung, Beschluss).</param>
    /// <param name="GueltigAb">Beginn der Amtszeit.</param>
    /// <param name="GueltigBis">Ende der Amtszeit; <c>null</c> ohne festes Ende.</param>
    /// <param name="Ampel">Bewertung des Amtszeitendes gegen die Vorwarnzeit.</param>
    public sealed record Zeile(string Funktion, int Sortierung, string Mitglied, FunktionArt Art, DateOnly GueltigAb, DateOnly? GueltigBis, Fristampel Ampel);

    /// <summary>Lädt die besetzten und unbesetzten Funktionen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        WarnungTage = await parameter.ZahlAsync(ParameterSchluessel.AmtszeitWarnungTage, heute, 180, ct);

        var laufend = await db.FunktionZuordnungen.AsNoTracking()
            .Where(f => f.GueltigAb <= heute && (f.GueltigBis == null || f.GueltigBis >= heute))
            .Where(f => f.Art == FunktionArt.Wahl || f.Funktion!.AmtszeitJahre != null)
            .Select(f => new { Funktion = f.Funktion!.Bezeichnung, f.Funktion.Sortierung, Mitglied = f.Mitglied!.Nachname + ", " + f.Mitglied.Vorname, f.Art, f.GueltigAb, f.GueltigBis })
            .ToListAsync(ct);

        Zeilen = laufend
            .Select(f => new Zeile(f.Funktion, f.Sortierung, f.Mitglied, f.Art, f.GueltigAb, f.GueltigBis, Frist.Bewerten(f.GueltigBis, heute, WarnungTage)))
            .OrderBy(z => z.Sortierung).ThenBy(z => z.Mitglied, StringComparer.CurrentCulture)
            .ToList();

        var besetzt = Zeilen.Select(z => z.Funktion).ToHashSet();
        Unbesetzt = await db.Funktionen.AsNoTracking()
            .Where(f => f.Aktiv && f.AmtszeitJahre != null)
            .OrderBy(f => f.Sortierung)
            .Select(f => f.Bezeichnung)
            .ToListAsync(ct);
        Unbesetzt = Unbesetzt.Where(f => !besetzt.Contains(f)).ToList();
    }
}
