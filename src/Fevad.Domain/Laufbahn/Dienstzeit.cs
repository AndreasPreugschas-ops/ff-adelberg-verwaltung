using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Anrechnungsregeln der VwV Feuerwehr-Ehrenzeichen Nr. 4 (Fassung 27.02.2025); die Werte kommen aus Parametern.</summary>
/// <param name="JugendAnrechnenAbAlter">Jugendfeuerwehr zählt ab diesem Lebensjahr (VwV: 14).</param>
/// <param name="AltersabteilungAnrechnenBisAlter">
/// Bestätigte Zeit in der Altersabteilung (Konzeption 65plus) zählt bis zu diesem Lebensjahr (VwV: 70); 0 heißt gar nicht.
/// Unbestätigte Zeit zählt nie.
/// </param>
/// <param name="KindererziehungMonate">Höchstens so viele Monate je Befreiung wegen Kindererziehung (VwV: 1 Jahr je Kind).</param>
/// <param name="LeistungsspangeAbEintritt">
/// Bei Eintritt in die Jugendfeuerwehr ab diesem Tag zählt die Jugendzeit nur mit Leistungsspange (VwV Nr. 6: 01.01.2018;
/// die VwV sagt „soll“, die FF Adelberg behandelt es als Pflicht). Null: keine Pflicht.
/// </param>
public sealed record Dienstzeitregeln(
    int JugendAnrechnenAbAlter,
    int AltersabteilungAnrechnenBisAlter,
    int KindererziehungMonate = 12,
    DateOnly? LeistungsspangeAbEintritt = null);

/// <summary>Angerechnete Einsatzdienstzeit und was dabei nicht angerechnet wurde (zum Anzeigen).</summary>
public sealed record Anrechnung(Dienstzeit Zeit, IReadOnlyList<string> NichtAngerechnet);

/// <summary>Zusammengefasste, überlappungsfreie Zeiträume. <c>Bis = null</c> heißt: läuft noch.</summary>
public sealed class Dienstzeit
{
    public IReadOnlyList<(DateOnly Von, DateOnly? Bis)> Zeitraeume { get; }

    public Dienstzeit(IEnumerable<(DateOnly Von, DateOnly? Bis)> zeitraeume) => Zeitraeume = Zusammenfassen(zeitraeume);

    /// <summary>Vollendete Diensttage vor dem Stichtag; der Stichtag selbst läuft noch.</summary>
    public int TageBis(DateOnly stichtag)
    {
        var gestern = stichtag.AddDays(-1);
        return Zeitraeume
            .Where(z => z.Von <= gestern)
            .Sum(z => (Min(z.Bis ?? gestern, gestern).DayNumber - z.Von.DayNumber) + 1);
    }

    /// <summary>
    /// Vollendete Dienstjahre am Stichtag. Gerechnet wird wie beim Lebensalter ab einem gedachten Beginn: dem Beginn des
    /// letzten Zeitraums, vorverlegt um die Tage der früheren Zeiträume. Ohne Unterbrechung ist das genau der Jahrestag.
    /// </summary>
    public int VolleJahreBis(DateOnly stichtag)
    {
        var summe = 0;
        var jahre = 0;
        foreach (var (von, bis) in Zeitraeume.Where(z => z.Von < stichtag))
        {
            var ende = bis is null || bis.Value >= stichtag ? stichtag : bis.Value.AddDays(1);
            jahre = Mitglieder.Mitglied.Alter(von.AddDays(-summe), ende);
            summe += ende.DayNumber - von.DayNumber;
        }

        return jahre;
    }

    /// <summary>
    /// Tag, an dem <paramref name="jahre"/> Dienstjahre vollendet sind (bei Eintritt am 01.01.2000 und 15 Jahren der
    /// 01.01.2015), wenn laufende Zeiträume weiterlaufen. Null, wenn das mit den bekannten Zeiträumen nicht mehr
    /// erreicht wird. Unterbrechungen verschieben den Tag um ihre Länge.
    /// </summary>
    public DateOnly? ErreichtAm(int jahre)
    {
        var summe = 0;
        foreach (var (von, bis) in Zeitraeume)
        {
            var am = von.AddDays(-summe).AddYears(jahre);
            if (bis is null || am <= bis.Value.AddDays(1))
            {
                return am;
            }

            summe += bis.Value.DayNumber - von.DayNumber + 1;
        }

        return null;
    }

    private static List<(DateOnly Von, DateOnly? Bis)> Zusammenfassen(IEnumerable<(DateOnly Von, DateOnly? Bis)> zeitraeume)
    {
        var ergebnis = new List<(DateOnly Von, DateOnly? Bis)>();
        foreach (var z in zeitraeume.Where(z => z.Bis is null || z.Bis >= z.Von).OrderBy(z => z.Von))
        {
            if (ergebnis.Count > 0)
            {
                var letzte = ergebnis[^1];

                // Doppelmitgliedschaften zählen nur einfach; lückenlos aneinander anschließende Abschnitte verschmelzen.
                if (letzte.Bis is null || z.Von.DayNumber <= letzte.Bis.Value.DayNumber + 1)
                {
                    ergebnis[^1] = (letzte.Von, letzte.Bis is null || z.Bis is null ? null : Max(letzte.Bis.Value, z.Bis.Value));
                    continue;
                }
            }

            ergebnis.Add(z);
        }

        return ergebnis;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

public static class Dienstzeitrechnung
{
    public static Dienstzeit Einsatzdienst(DateOnly geburtsdatum, IEnumerable<Abschnitt> abschnitte, Dienstzeitregeln regeln, bool leistungsspange = true) =>
        Anrechnen(geburtsdatum, abschnitte, regeln, leistungsspange).Zeit;

    /// <summary>
    /// Anrechenbare Zeit im Einsatzdienst für das Feuerwehr-Ehrenzeichen (VwV Nr. 4): Einsatzabteilung; Jugendfeuerwehr
    /// ab dem Anrechnungsalter (bei Eintritt ab dem Stichtag nur mit Leistungsspange); Altersabteilung und Musikabteilung
    /// nur mit Bestätigung; Vordienstzeiten anderer Feuerwehren; Wehr- und Freiwilligendienst nur, wenn vorher schon
    /// Feuerwehrdienst war. Befreiungen zählen nicht, außer Krankheit, Mutterschutz und Kindererziehung bis zur Grenze.
    /// </summary>
    public static Anrechnung Anrechnen(DateOnly geburtsdatum, IEnumerable<Abschnitt> abschnitte, Dienstzeitregeln regeln, bool leistungsspange)
    {
        var liste = abschnitte.OrderBy(a => a.Von).ToList();
        var zeitraeume = new List<(DateOnly Von, DateOnly? Bis)>();
        var nicht = new List<string>();

        // Erster Eintritt in irgendeine Jugendfeuerwehr, auch als Vordienstzeit in einer anderen Wehr erfasst.
        var jugendEintritt = liste.Where(a => a.AbteilungCode == AbteilungCode.Jugend).Select(a => (DateOnly?)a.Von).Min();
        var ohneSpange = !leistungsspange && regeln.LeistungsspangeAbEintritt is { } pflichtAb && jugendEintritt >= pflichtAb;

        foreach (var a in liste.Where(a => a.Vordienstart is not (Vordienstart.Wehrdienst or Vordienstart.Freiwilligendienst)))
        {
            if (a.Status is not (MitgliedStatus.Aktiv or MitgliedStatus.Probezeit or MitgliedStatus.Befreit))
            {
                continue;
            }

            // Erst die Regeln der Abteilung, dann bei einer Befreiung zusätzlich deren Grenze: Keine hebt die andere auf.
            var zeitraum = Abteilungszeitraum(geburtsdatum, a, regeln, ohneSpange, nicht);
            if (zeitraum is not null && a.Status == MitgliedStatus.Befreit)
            {
                zeitraum = Befreiung(a, zeitraum.Value, regeln, nicht);
            }

            if (zeitraum is { } z)
            {
                zeitraeume.Add(z);
            }
        }

        // Wehr- und Freiwilligendienst zählen nur, wenn die Person vorher schon Feuerwehrdienst geleistet hat.
        var feuerwehrBeginn = zeitraeume.Count == 0 ? (DateOnly?)null : zeitraeume.Min(z => z.Von);
        foreach (var a in liste.Where(a => a.Vordienstart is Vordienstart.Wehrdienst or Vordienstart.Freiwilligendienst))
        {
            if (feuerwehrBeginn < a.Von)
            {
                zeitraeume.Add((a.Von, a.Bis));
            }
            else
            {
                nicht.Add($"{(a.Vordienstart == Vordienstart.Wehrdienst ? "Wehr- oder Zivildienst" : "Freiwilligendienst")} ab {a.Von:dd.MM.yyyy}: nicht angerechnet, weil vorher kein Feuerwehrdienst war.");
            }
        }

        return new Anrechnung(new Dienstzeit(zeitraeume), nicht);
    }

    /// <summary>Der Teil eines Abschnitts, den seine Abteilung zum Einsatzdienst macht; null, wenn nichts zählt.</summary>
    private static (DateOnly Von, DateOnly? Bis)? Abteilungszeitraum(
        DateOnly geburtsdatum, Abschnitt a, Dienstzeitregeln regeln, bool ohneSpange, List<string> nicht)
    {
        switch (a.AbteilungCode)
        {
            case AbteilungCode.Einsatz:
                return (a.Von, a.Bis);
            case AbteilungCode.Jugend when ohneSpange:
                nicht.Add($"Jugendfeuerwehr ab {a.Von:dd.MM.yyyy}: ohne Leistungsspange nicht angerechnet (Eintritt ab {regeln.LeistungsspangeAbEintritt:dd.MM.yyyy}).");
                return null;
            case AbteilungCode.Jugend:
                var ab = Laufbahnhinweise.Geburtstag(geburtsdatum, regeln.JugendAnrechnenAbAlter);
                return Begrenzen(a.Von > ab ? a.Von : ab, a.Bis);
            case AbteilungCode.Alters when a.Bestaetigt && regeln.AltersabteilungAnrechnenBisAlter > 0:
                var bis = Laufbahnhinweise.Geburtstag(geburtsdatum, regeln.AltersabteilungAnrechnenBisAlter).AddDays(-1);
                return Begrenzen(a.Von, a.Bis is null || a.Bis > bis ? bis : a.Bis);
            case AbteilungCode.Alters:
                nicht.Add($"Altersabteilung ab {a.Von:dd.MM.yyyy}: nicht angerechnet, weil keine Aufgaben nach Konzeption 65plus bestätigt sind.");
                return null;
            case AbteilungCode.Musik when a.Bestaetigt:
                return (a.Von, a.Bis);
            case AbteilungCode.Musik:
                nicht.Add($"Musikabteilung ab {a.Von:dd.MM.yyyy}: nicht angerechnet, weil Grundausbildung und Übungsdienst nicht bestätigt sind.");
                return null;
            default:
                return null;
        }
    }

    /// <summary>Was von einer Befreiung zählt: Krankheit und Mutterschutz ganz, Kindererziehung bis zur Grenze, sonst nichts.</summary>
    private static (DateOnly Von, DateOnly? Bis)? Befreiung(Abschnitt a, (DateOnly Von, DateOnly? Bis) zeitraum, Dienstzeitregeln regeln, List<string> nicht)
    {
        switch (a.Befreiungsgrund)
        {
            case Befreiungsgrund.Krankheit or Befreiungsgrund.Mutterschutz:
                return zeitraum;
            case Befreiungsgrund.Kindererziehung when regeln.KindererziehungMonate > 0:
                var grenze = a.Von.AddMonths(regeln.KindererziehungMonate).AddDays(-1);
                if (a.Bis is null || a.Bis > grenze)
                {
                    nicht.Add($"Kindererziehung ab {a.Von:dd.MM.yyyy}: nur {regeln.KindererziehungMonate} Monate angerechnet.");
                }

                return Begrenzen(zeitraum.Von, zeitraum.Bis is null || zeitraum.Bis > grenze ? grenze : zeitraum.Bis);
            default:
                nicht.Add($"Befreiung ab {a.Von:dd.MM.yyyy}: nicht angerechnet.");
                return null;
        }
    }

    private static (DateOnly Von, DateOnly? Bis)? Begrenzen(DateOnly von, DateOnly? bis) => bis < von ? null : (von, bis);

    /// <summary>Zugehörigkeit zur eigenen Feuerwehr in allen Abteilungen, für Vereinsjubiläen. Vordienstzeiten zählen nicht.</summary>
    public static Dienstzeit Zugehoerigkeit(IEnumerable<Abschnitt> abschnitte) =>
        new(abschnitte.Where(a => !a.Vordienstzeit).Select(a => (a.Von, a.Bis)));

    /// <summary>Zeit in der Jugendfeuerwehr (eigene Wehr), für die Traditionsnadel der Jugendfeuerwehr.</summary>
    public static Dienstzeit Jugendfeuerwehr(IEnumerable<Abschnitt> abschnitte) =>
        new(abschnitte.Where(a => !a.Vordienstzeit && a.AbteilungCode == AbteilungCode.Jugend).Select(a => (a.Von, a.Bis)));
}
