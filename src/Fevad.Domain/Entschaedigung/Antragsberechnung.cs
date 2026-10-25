using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Entschaedigung;

/// <summary>Ein berechneter Bestandteil eines Entschädigungsantrags.</summary>
/// <param name="Vorgang">Kurzname des Bestandteils, z. B. „Lehrgang“, „Verdienstausfall“, „Fahrkosten“.</param>
/// <param name="Bezeichnung">Lesbarer Text für die Abrechnung.</param>
/// <param name="Steuerart">Steuerliche Einordnung.</param>
/// <param name="Menge">Stunden, Kilometer oder 1.</param>
/// <param name="Einheit">Einheit der Menge.</param>
/// <param name="Satz">Satz je Einheit.</param>
/// <param name="Betrag">Betrag in Euro.</param>
/// <param name="Schluessel">Verwendete Parameterschlüssel, damit Beispielwerte erkannt werden.</param>
public sealed record Antragsposten(
    string Vorgang, string Bezeichnung, Steuerart Steuerart, decimal Menge, Entschaedigungseinheit Einheit, decimal Satz, decimal Betrag,
    IReadOnlyList<string> Schluessel);

/// <summary>
/// Berechnet einen Entschädigungsantrag nach der Entschädigungssatzung: Lehrgänge je Tag nach der Lehrgangsregel (Staffel) mit Aufschlag
/// für tatsächlichen Verdienstausfall (FwES Adelberg § 2 Abs. 1), über mehr als zwei Tage in tatsächlicher Höhe (§ 16 Abs. 4 FwG,
/// haushaltsführende Personen mit Stundensatz je Werktag nach § 4), dazu Fahrkosten nach LRKG (§ 2 Abs. 4) und erstattete
/// Sozialversicherungsbeiträge. Alle Sätze kommen aus den Parametern.
/// </summary>
public static class Antragsberechnung
{
    /// <summary>Aufschlag je Stunde bei tatsächlichem Verdienstausfall während eines Lehrgangs.</summary>
    public static readonly string ZuschlagSchluessel = Entschaedigungssaetze.Schluessel(nameof(DienstArt.Lehrgang), "VerdienstausfallZuschlagStunde");

    /// <summary>Stundensatz für haushaltsführende Personen bei Abrechnung in tatsächlicher Höhe (FwES § 4).</summary>
    public const string HaushaltSatzSchluessel = Entschaedigungssaetze.Praefix + "Haushaltsfuehrend.SatzStunde";

    /// <summary>Höchstens so viele Stunden je Werktag für haushaltsführende Personen (FwES § 4).</summary>
    public const string HaushaltMaxStundenSchluessel = Entschaedigungssaetze.Praefix + "Haushaltsfuehrend.MaxStundenJeWerktag";

    /// <summary>Wegstreckenentschädigung je Kilometer mit eigenem Kraftfahrzeug (LRKG).</summary>
    public const string KilometerSchluessel = Entschaedigungssaetze.Praefix + "Fahrkosten.JeKilometer";

    /// <summary>Ergebnis der Berechnung.</summary>
    /// <param name="Posten">Die Bestandteile.</param>
    /// <param name="Hinweise">Was fehlt, z. B. ein Satz.</param>
    public sealed record Ergebnis(IReadOnlyList<Antragsposten> Posten, IReadOnlyList<string> Hinweise)
    {
        /// <summary>Summe in Euro.</summary>
        public decimal Summe => Posten.Sum(p => p.Betrag);
    }

    /// <summary>Berechnet den Antrag mit den am ersten Tag gültigen Sätzen.</summary>
    /// <param name="antrag">Der Antrag mit Tagen.</param>
    /// <param name="saetze">Die Entschädigungssätze.</param>
    /// <returns>Bestandteile und Hinweise.</returns>
    public static Ergebnis Berechnen(Entschaedigungsantrag antrag, Entschaedigungssaetze saetze)
    {
        var posten = new List<Antragsposten>();
        var hinweise = new List<string>();
        if (antrag.ErsterTag is not { } stichtag)
        {
            return new Ergebnis(posten, ["Keine Tage angegeben."]);
        }

        var grenze = (int)(saetze.Zahl(Entschaedigungssaetze.TatsaechlicheHoeheUeberTage, stichtag) ?? 0);
        var tage = antrag.Tage.OrderBy(t => t.Von).ToList();

        if (antrag.InTatsaechlicherHoehe(grenze))
        {
            TatsaechlicheHoehe(antrag, saetze, stichtag, tage, posten, hinweise);
        }
        else
        {
            Durchschnittssatz(antrag, saetze, stichtag, tage, posten, hinweise);
        }

        if (antrag.SozialversicherungBetrag is > 0 and var sv)
        {
            posten.Add(new("Sozialversicherung", "Erstattete Sozialversicherungsbeiträge", Steuerart.Auslagenersatz, 1, Entschaedigungseinheit.Pauschal, sv, sv, []));
        }

        switch (antrag.Fahrtart)
        {
            case Fahrtart.Pkw when antrag.Kilometer is > 0 and var km:
                if (saetze.Zahl(KilometerSchluessel, stichtag) is { } jeKm and > 0)
                {
                    posten.Add(new("Fahrkosten", $"Fahrkosten eigenes Auto ({km:0.#} km)", Steuerart.Auslagenersatz, km, Entschaedigungseinheit.Kilometer,
                        jeKm, Runden(km * jeKm), [KilometerSchluessel]));
                }
                else
                {
                    hinweise.Add("Für die Wegstreckenentschädigung ist kein Satz je Kilometer hinterlegt.");
                }

                break;
            case Fahrtart.OeffentlicheVerkehrsmittel when antrag.Fahrkosten is > 0 and var betrag:
                posten.Add(new("Fahrkosten", "Fahrkosten Bahn oder Bus", Steuerart.Auslagenersatz, 1, Entschaedigungseinheit.Pauschal, betrag, betrag, []));
                break;
        }

        return new Ergebnis(posten, hinweise);
    }

    private static void Durchschnittssatz(
        Entschaedigungsantrag antrag, Entschaedigungssaetze saetze, DateOnly stichtag, List<AntragTag> tage, List<Antragsposten> posten, List<string> hinweise)
    {
        if (antrag.Anlass != AntragAnlass.Lehrgang)
        {
            hinweise.Add("Einsätze bis zur Grenze rechnet der Einsatzbericht ab.");
            return;
        }

        var vorgang = nameof(DienstArt.Lehrgang);
        if (saetze.Regel(vorgang, stichtag) is not { Verguetet: true } regel)
        {
            hinweise.Add("Für Lehrgänge ist kein Satz hinterlegt.");
            return;
        }

        var zuschlag = saetze.Zahl(ZuschlagSchluessel, stichtag);
        // Bei haushaltsführenden Personen gilt die Zeitversäumnis als Verdienstausfall (FwES § 4).
        var mitVerdienstausfall = antrag.Erwerbslage == Erwerbslage.Haushaltsfuehrend
            || (antrag.Verdienstausfall && antrag.Erwerbslage != Erwerbslage.OhneVerdienst);
        var schluessel = new List<string> { Entschaedigungssaetze.Schluessel(vorgang, regel.NachStaffel ? "StaffelErsterBetrag" : regel.NachTagen ? "SatzTag" : "SatzStunde") };
        foreach (var tag in tage)
        {
            var datum = Ortszeit.Datum(tag.Von);
            var betrag = regel.Berechnen(tag.Bis - tag.Von, Ortszeit.Kalendertage(tag.Von, tag.Bis));
            posten.Add(new(vorgang, $"{antrag.Bezeichnung}, {datum:dd.MM.yyyy}", Steuerart.Aufwandsentschaedigung, betrag.Menge, betrag.Einheit,
                betrag.Satz, betrag.Betrag, schluessel));

            if (mitVerdienstausfall && zuschlag is > 0 && betrag.Einheit is Entschaedigungseinheit.Stunde or Entschaedigungseinheit.Staffel)
            {
                posten.Add(new("Verdienstausfall", $"Aufschlag Verdienstausfall {datum:dd.MM.yyyy}", Steuerart.Verdienstausfall, betrag.Menge,
                    Entschaedigungseinheit.Stunde, zuschlag.Value, Runden(betrag.Menge * zuschlag.Value), [ZuschlagSchluessel]));
            }
        }

        if (mitVerdienstausfall && zuschlag is not > 0)
        {
            hinweise.Add("Für den Verdienstausfall bei Lehrgängen ist kein Aufschlag hinterlegt.");
        }
    }

    private static void TatsaechlicheHoehe(
        Entschaedigungsantrag antrag, Entschaedigungssaetze saetze, DateOnly stichtag, List<AntragTag> tage, List<Antragsposten> posten, List<string> hinweise)
    {
        if (antrag.Erwerbslage == Erwerbslage.Haushaltsfuehrend)
        {
            var satz = saetze.Zahl(HaushaltSatzSchluessel, stichtag);
            var max = saetze.Zahl(HaushaltMaxStundenSchluessel, stichtag) ?? 24;
            if (satz is not > 0)
            {
                hinweise.Add("Für haushaltsführende Personen ist kein Stundensatz hinterlegt.");
            }
            else
            {
                // Je Werktag (Montag bis Samstag) höchstens die Höchststunden.
                var stunden = tage
                    .GroupBy(t => Ortszeit.Datum(t.Von))
                    .Where(g => g.Key.DayOfWeek != DayOfWeek.Sunday)
                    .Sum(g => Math.Min(max, Math.Round((decimal)g.Sum(t => (t.Bis - t.Von).TotalHours), 2)));
                if (stunden > 0)
                {
                    posten.Add(new("Verdienstausfall", "Zeitversäumnis haushaltsführend", Steuerart.Verdienstausfall, stunden, Entschaedigungseinheit.Stunde,
                        satz.Value, Runden(stunden * satz.Value), [HaushaltSatzSchluessel, HaushaltMaxStundenSchluessel]));
                }
            }
        }
        else if (antrag.VerdienstausfallBetrag is > 0 and var verdienst)
        {
            posten.Add(new("Verdienstausfall", "Verdienstausfall in tatsächlicher Höhe", Steuerart.Verdienstausfall, 1, Entschaedigungseinheit.Pauschal,
                verdienst, verdienst, []));
        }

        if (antrag.AuslagenBetrag is > 0 and var auslagen)
        {
            posten.Add(new("Auslagen", "Notwendige Auslagen in tatsächlicher Höhe", Steuerart.Auslagenersatz, 1, Entschaedigungseinheit.Pauschal,
                auslagen, auslagen, []));
        }

        if (posten.Count == 0 && antrag.SozialversicherungBetrag is not > 0)
        {
            hinweise.Add("In tatsächlicher Höhe abzurechnen, aber weder Verdienstausfall noch Auslagen angegeben.");
        }
    }

    private static decimal Runden(decimal betrag) => Math.Round(betrag, 2, MidpointRounding.AwayFromZero);
}
