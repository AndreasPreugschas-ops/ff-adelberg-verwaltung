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
    /// <summary>Ein Menüeintrag.</summary>
    /// <param name="Titel">Angezeigter Titel.</param>
    /// <param name="Seite">Pfad der Seite.</param>
    /// <param name="Beschreibung">Kurzbeschreibung für die Startseite.</param>
    public sealed record Eintrag(string Titel, string Seite, string? Beschreibung = null);

    /// <summary>Ein Bereich des Hauptmenüs.</summary>
    /// <param name="Titel">Titel des Bereichs.</param>
    /// <param name="Beschreibung">Kurzbeschreibung.</param>
    /// <param name="Eintraege">Einträge, die der Benutzer sehen darf.</param>
    public sealed record Bereich(string Titel, string Beschreibung, IReadOnlyList<Eintrag> Eintraege);

    /// <summary>Lädt die Bereiche mit den Einträgen, deren Policy der Benutzer erfüllt; leere Bereiche entfallen.</summary>
    /// <param name="benutzer">Der angemeldete Benutzer.</param>
    /// <returns>Die Bereiche.</returns>
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
        if (await Darf(Berechtigungen.JugendVeranstaltungenSehen)) dienstbetrieb.Add(new("Veranstaltungen Jugend", "/Jugend/Veranstaltungen", "Fahrten, Zeltlager, Anmeldungen, Zuschuss"));
        if (await Darf(Berechtigungen.JugendBearbeiten)) dienstbetrieb.Add(new("Warteliste Jugend", "/Jugend/Warteliste", "Interessenten, Wiedervorlage, Schnupperdienst"));
        if (await Darf(Berechtigungen.JugendBetreuerSehen)) dienstbetrieb.Add(new("Betreuer Jugend", "/Jugend/Betreuer", "Betreuer-Register, Führungszeugnisse"));
        if (await Darf(Berechtigungen.JugendNotfalllisteSehen)) dienstbetrieb.Add(new("Notfallliste Jugend", "/Jugend/Notfallliste", "Heimweg, Abholberechtigte, Eltern, Notfallmappe"));
        if (await Darf(Berechtigungen.EinsaetzeLesen)) dienstbetrieb.Add(new("Einsätze", "/Einsaetze/Index", "Einsatzberichte"));
        if (await Darf(Berechtigungen.UnfaelleLesen)) dienstbetrieb.Add(new("Unfälle und Verbandbuch", "/Unfaelle/Index", "Unfallanzeige, Verbandbuch, Beinaheunfälle"));
        else if (await Darf(Berechtigungen.UnfaelleAnlegen)) dienstbetrieb.Add(new("Dienstunfall erfassen", "/Unfaelle/Bearbeiten"));
        if (await Darf(Berechtigungen.SachschaedenLesen)) dienstbetrieb.Add(new("Sachschäden", "/Unfaelle/Sachschaeden", "Ersatz nach § 17 FwG"));

        var ausruestung = new List<Eintrag>();
        var technikLesen = await Darf(Berechtigungen.TechnikLesen);
        if (technikLesen) ausruestung.Add(new("Technik", "/Technik/Index", "Fahrzeuge, Geräte, Prüffristen, Mängel"));
        else if (await Darf(Berechtigungen.MaengelMelden)) ausruestung.Add(new("Mangel melden", "/Melden/Index"));
        if (await Darf(Berechtigungen.FahrtenbuchFuehren)) ausruestung.Add(new("Fahrtenbuch", "/Fahrtenbuch/Index", "Fahrt oder Betankung eintragen"));
        if (!technikLesen && await Darf(Berechtigungen.BeschaffungLesen)) ausruestung.Add(new("Beschaffung", "/Beschaffung/Index"));
        if (await Darf(Berechtigungen.KleiderkammerLesen)) ausruestung.Add(new("Kleiderkammer", "/Kleiderkammer/Index", "Persönliche Ausrüstung, Lager"));

        var verwaltung = new List<Eintrag>();
        if (await Darf(Berechtigungen.DokumenteLesen)) verwaltung.Add(new("Dokumente", "/Dokumente/Index"));
        if (await Darf(Berechtigungen.GremienLesen)) verwaltung.Add(new("Ausschuss", "/Gremien/Index", "Sitzungen und Wahlen"));
        if (await Darf(Berechtigungen.VersammlungLesen)) verwaltung.Add(new("Hauptversammlung", "/Hauptversammlung/Index", "Niederschriften und Kassenberichte"));
        if (await Darf(Berechtigungen.KasseSehen))
        {
            verwaltung.Add(new("Kassen", "/Kasse/Index", "Kameradschaftskasse und Kasse der Jugendfeuerwehr"));
            verwaltung.Add(new("Bestandsverzeichnis", "/Kasse/Bestand", "Gegenstände aus dem Sondervermögen der Kassen"));
        }
        if (await Darf(Berechtigungen.HaushaltLesen)) verwaltung.Add(new("Haushaltsanmeldung", "/Haushalt/Index", "Mittelanmeldung der Feuerwehr an die Gemeinde"));
        if (await Darf(Berechtigungen.KostenersatzLesen)) verwaltung.Add(new("Kostenersatz", "/Kostenersatz/Index", "Vorgänge nach § 34 FwG, Empfehlung und Entscheidung"));
        if (await Darf(Berechtigungen.KostenersatzForderungen) || await Darf(Berechtigungen.FinanzexportEinrichten)) verwaltung.Add(new("Finanzaustausch", "/Finanzen/Index", "Exporte an die Kämmerei, Zahlungsstatus"));
        if (await Darf(Berechtigungen.KostenersatzKalkulation)) verwaltung.Add(new("Stundensatz-Kalkulation", "/Kostenersatz/Kalkulation", "Personalstundensatz nach § 34 Abs. 5 FwG, Anlage zur Gemeinderatsvorlage"));
        if (await Darf(Berechtigungen.GemeindeVorgaenge) || await Darf(Berechtigungen.GemeindeMitgliederbescheide)) verwaltung.Add(new("Entscheidungen der Gemeinde", "/Gemeinde/Index", "Vorlagen für Gemeinderat und Bürgermeister, Bescheide an Mitglieder"));
        if (await Darf(Berechtigungen.Dienstausweise)) verwaltung.Add(new("Dienstausweise", "/Gemeinde/Dienstausweise", "Ausstellen, drucken, Rückgabe"));
        if (await Darf(Berechtigungen.EntschaedigungLesen) || await Darf(Berechtigungen.StatistikLesen) || await Darf(Berechtigungen.KennzahlenLesen)) verwaltung.Add(new("Auswertungen", "/Auswertungen/Index", "Entschädigung, Jahresstatistik, Kennzahlen"));
        if (await Darf(Berechtigungen.ChronikLesen)) verwaltung.Add(new("Chronik", "/Chronik/Index", "Ereignisse je Jahr, Jubiläen"));
        if (await Darf(Berechtigungen.Serienbriefe)) verwaltung.Add(new("Briefe und Urkunden", "/Vorlagen/Index", "Serienbriefe, Urkunden, Elternbriefe, Serienmails"));
        if ((await Pages.Stammdaten.IndexModel.ErlaubtAsync(autorisierung, benutzer)).Count > 0) verwaltung.Add(new("Stammdaten", "/Stammdaten/Index"));
        if (await Darf(Berechtigungen.BenutzerVerwalten))
        {
            verwaltung.Add(new("Benutzer", "/Verwaltung/Benutzer/Index"));
            verwaltung.Add(new("Rollen und Rechte", "/Verwaltung/Rechte/Index"));
        }
        if (await Darf(Berechtigungen.RechteFreigeben)) verwaltung.Add(new("Rechte freigeben", "/Verwaltung/Rechtefreigaben", "Zweite Person für Rechte auf Gesundheits- und Jugenddaten"));
        if (await Darf(Berechtigungen.DatenschutzLoeschlauf)) verwaltung.Add(new("Datenschutz", "/Verwaltung/Datenschutz/Index"));
        if (await Darf(Berechtigungen.DatenschutzLeseprotokoll)) verwaltung.Add(new("Leseprotokoll", "/Verwaltung/Leseprotokoll", "Wer hat Atemschutzdaten und Befreiungsgründe geöffnet"));
        if (await Darf(Berechtigungen.DatenImportieren)) verwaltung.Add(new("Altdaten importieren", "/Verwaltung/Import/Index", "CSV oder Excel, z. B. aus MP-Feuer"));

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
