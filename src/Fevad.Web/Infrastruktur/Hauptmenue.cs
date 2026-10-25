using System.Security.Claims;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.Authorization;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Hauptmenü nach Bereichen (Mannschaft, Dienstbetrieb, Ausrüstung, Verwaltung), jeder Eintrag nur mit dem passenden Recht.
/// Dieselbe Liste dient der Navigation und der Bereichsauswahl auf der Startseite; leere Bereiche fehlen.
/// </summary>
public class Hauptmenue(IAuthorizationService autorisierung)
{
    public sealed record Eintrag(string Titel, string Seite, string? Beschreibung = null);

    public sealed record Bereich(string Titel, string Beschreibung, IReadOnlyList<Eintrag> Eintraege);

    public async Task<IReadOnlyList<Bereich>> LadenAsync(ClaimsPrincipal benutzer)
    {
        if (benutzer.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        async Task<bool> Darf(string policy) => (await autorisierung.AuthorizeAsync(benutzer, policy)).Succeeded;

        var mannschaft = new List<Eintrag>();
        if (await Darf(Berechtigungen.MitgliederSehen))
        {
            mannschaft.Add(new("Mitglieder", "/Mitglieder/Index", "Mitgliederakte, Stammdaten, Gliederung"));
            mannschaft.Add(new("Abteilungen", "/Abteilungen/Index", "Abteilungen mit Mitgliedern und Funktionen"));
        }
        if (await Darf(Berechtigungen.LaufbahnLesen)) mannschaft.Add(new("Laufbahn", "/Laufbahn/Index", "Dienstgrade, Funktionen, Ehrungen"));
        if (await Darf(Berechtigungen.AntraegeSehen)) mannschaft.Add(new("Anträge", "/Antraege/Index", "Aufnahme, Übertritt, Ausscheiden"));
        if (await Darf(Berechtigungen.AusbildungLesen)) mannschaft.Add(new("Personal", "/Personal/Index", "Lehrgänge, Nachweise und Fristen"));
        if (await Darf(Berechtigungen.AtemschutzLesen)) mannschaft.Add(new("Atemschutz", "/Atemschutz/Index", "Geräteträger, Abmeldungen, Nachweise bestätigen"));

        var dienstbetrieb = new List<Eintrag> { new("Dienste", "/Dienste/Index", "Übungen und Dienste, Rückmeldungen"), new("Termine", "/Termine/Index", "Kalender") };
        if (await Darf(Berechtigungen.EinsaetzeLesen)) dienstbetrieb.Add(new("Einsätze", "/Einsaetze/Index", "Einsatzberichte"));
        if (await Darf(Berechtigungen.UnfaelleLesen)) dienstbetrieb.Add(new("Dienstunfälle", "/Unfaelle/Index", "Unfallanzeige an die Unfallkasse"));
        else if (await Darf(Berechtigungen.UnfaelleAnlegen)) dienstbetrieb.Add(new("Dienstunfall erfassen", "/Unfaelle/Bearbeiten"));

        var ausruestung = new List<Eintrag>();
        var technikLesen = await Darf(Berechtigungen.TechnikLesen);
        if (technikLesen) ausruestung.Add(new("Technik", "/Technik/Index", "Fahrzeuge, Geräte, Prüffristen, Mängel"));
        else if (await Darf(Berechtigungen.MaengelMelden)) ausruestung.Add(new("Mangel melden", "/Melden/Index"));
        if (!technikLesen && await Darf(Berechtigungen.BeschaffungLesen)) ausruestung.Add(new("Beschaffung", "/Beschaffung/Index"));
        if (await Darf(Berechtigungen.KleiderkammerLesen)) ausruestung.Add(new("Kleiderkammer", "/Kleiderkammer/Index", "Persönliche Ausrüstung, Lager"));

        var verwaltung = new List<Eintrag>();
        if (await Darf(Berechtigungen.DokumenteLesen)) verwaltung.Add(new("Dokumente", "/Dokumente/Index"));
        if (await Darf(Berechtigungen.GremienLesen)) verwaltung.Add(new("Ausschuss", "/Gremien/Index", "Sitzungen und Wahlen"));
        if (await Darf(Berechtigungen.EntschaedigungLesen) || await Darf(Berechtigungen.StatistikLesen)) verwaltung.Add(new("Auswertungen", "/Auswertungen/Index", "Entschädigung, Jahresstatistik"));
        if ((await Pages.Stammdaten.IndexModel.ErlaubtAsync(autorisierung, benutzer)).Count > 0) verwaltung.Add(new("Stammdaten", "/Stammdaten/Index"));
        if (await Darf(Berechtigungen.BenutzerVerwalten))
        {
            verwaltung.Add(new("Benutzer", "/Verwaltung/Benutzer/Index"));
            verwaltung.Add(new("Rollen und Rechte", "/Verwaltung/Rechte/Index"));
        }
        if (await Darf(Berechtigungen.DatenschutzLoeschlauf)) verwaltung.Add(new("Datenschutz", "/Verwaltung/Datenschutz/Index"));

        return new List<Bereich>
            {
                new("Mannschaft", "Mitglieder, Laufbahn, Ausbildung", mannschaft),
                new("Dienstbetrieb", "Dienste, Termine, Einsätze", dienstbetrieb),
                new("Ausrüstung", "Technik und Kleiderkammer", ausruestung),
                new("Verwaltung", "Dokumente, Ausschuss, Einstellungen", verwaltung),
            }
            .Where(b => b.Eintraege.Count > 0)
            .ToList();
    }
}
