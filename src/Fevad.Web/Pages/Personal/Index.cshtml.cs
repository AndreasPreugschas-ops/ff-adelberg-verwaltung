using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Personal;

/// <summary>Fristenübersicht der Einsatzabteilung: Lehrgänge mit Ablauf, wiederkehrende Nachweise, Atemschutz.</summary>
public class IndexModel(Personalfristenabfrage fristen, TimeProvider zeit, IAuthorizationService autorisierung, Leseprotokoll leseprotokoll) : PageModel
{
    /// <summary>Nur dieser Bereich.</summary>
    [BindProperty(SupportsGet = true)]
    public Fristbereich? Bereich { get; set; }

    /// <summary>Auch gültige Fristen zeigen, nicht nur gelbe und rote.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    /// <summary>Name des Mitglieds oder Bezeichnung der Frist.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Suche { get; set; }

    /// <summary>Angezeigte Seite.</summary>
    [BindProperty(SupportsGet = true, Name = "Seite")]
    public int Seitennummer { get; set; } = 1;

    /// <summary>Die Fristen der angezeigten Seite.</summary>
    public Seite<Fristeintrag> Eintraege { get; private set; } = Seite<Fristeintrag>.Leer;
    /// <summary>Zahl der abgelaufenen Fristen (rot).</summary>
    public int Abgelaufen { get; private set; }
    /// <summary>Zahl der bald ablaufenden Fristen (gelb).</summary>
    public int LaufenAb { get; private set; }
    /// <summary>Darf Lehrgänge und Nachweise eintragen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Darf Kataloge pflegen.</summary>
    public bool DarfKataloge { get; private set; }
    /// <summary>Wer welche Frist verlängern darf.</summary>
    public Fristrechte Rechte { get; private set; } = new(false, false, false);

    /// <summary>Lädt die Fristen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var gesundheit = await DarfAsync(Berechtigungen.AtemschutzLesen);
        DarfBearbeiten = await DarfAsync(Berechtigungen.AusbildungBearbeiten) || await DarfAsync(Berechtigungen.AusbildungNachweise) || await DarfAsync(Berechtigungen.AtemschutzBearbeiten);
        DarfKataloge = await DarfAsync(Berechtigungen.AusbildungKataloge);
        Rechte = new(await DarfAsync(Berechtigungen.AusbildungBearbeiten), await DarfAsync(Berechtigungen.AusbildungNachweise), await DarfAsync(Berechtigungen.AtemschutzBearbeiten));

        var sichtbar = (await fristen.LadenAsync(heute, ct: ct))
            .Where(f => gesundheit || !f.Gesundheitsbezogen)
            .ToList();
        Abgelaufen = sichtbar.Count(f => f.Ampel == Fristampel.Rot);
        LaufenAb = sichtbar.Count(f => f.Ampel == Fristampel.Gelb);

        var suche = Suche?.Trim();
        Eintraege = sichtbar
            .Where(f => Bereich is null || f.Bereich == Bereich)
            .Where(f => Alle || f.Ampel != Fristampel.Gruen)
            .Where(f => string.IsNullOrEmpty(suche)
                || f.Mitglied.Contains(suche, StringComparison.CurrentCultureIgnoreCase)
                || f.Bezeichnung.Contains(suche, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(f => f.Ampel)
            .ThenBy(f => f.FaelligAm ?? DateOnly.MinValue)
            .ThenBy(f => f.Mitglied)
            .ToList()
            .Seite(Seitennummer);
        await leseprotokoll.ProtokollierenAsync(Leseprotokoll.Atemschutz, "Fristenübersicht Personal",
            Eintraege.Eintraege.Where(f => f.Gesundheitsbezogen).Select(f => f.MitgliedId), ct);
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
