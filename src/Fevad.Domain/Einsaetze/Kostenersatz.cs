using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <param name="Menge">Stunden, Tage oder Stück.</param>
/// <param name="Satz">Euro je Einheit.</param>
/// <param name="Art">Art des Satzes.</param>
/// <param name="Bezeichnung">Bezeichnung des Postens, z. B. das Fahrzeug.</param>
/// <param name="Betrag">Menge mal Satz in Euro.</param>
/// <param name="Einheit">Einheit des Satzes.</param>
public sealed record Kostenposten(KostensatzArt Art, string Bezeichnung, decimal Menge, Kosteneinheit Einheit, decimal Satz, decimal Betrag);

/// <param name="Posten">Berechnete Posten in der Reihenfolge Fahrzeuge, Personal, Ausrüstung.</param>
/// <param name="Hinweise">Was nicht berechnet werden konnte, z. B. ein Fahrzeug ohne Satz oder ohne Endzeit.</param>
public sealed record Kostenaufstellung(IReadOnlyList<Kostenposten> Posten, IReadOnlyList<string> Hinweise)
{
    /// <summary>Summe aller Posten in Euro.</summary>
    public decimal Summe => Posten.Sum(p => p.Betrag);
}

/// <summary>
/// Berechnet den Kostenersatz eines Einsatzes aus den Zeiten je Fahrzeug und Person sowie der Ausrüstung.
/// Die Leistungsdauer beginnt beim Fahrzeug mit dem Ausrücken und endet mit der Einsatzbereitschaft,
/// beim Personal mit der Alarmierung bis zur Einsatzbereitschaft (§ 34 FwG, Muster Kostenersatzsatzung § 6);
/// fehlende Einzelzeiten erben die Zeiten des Einsatzes. Angefangene Einheiten werden auf
/// <c>rundungMinuten</c> aufgerundet (Parameter, keine Konstante).
/// </summary>
public static class Kostenersatz
{
    /// <summary>Bezeichnung des Personalpostens.</summary>
    public const string PersonalBezeichnung = "Einsatzkräfte";

    /// <summary>Berechnet die Kostenaufstellung mit den am Einsatztag gültigen Sätzen.</summary>
    /// <param name="einsatz">Der Einsatz mit Fahrzeugen, Beteiligten und Ausrüstung.</param>
    /// <param name="saetze">Alle Kostensätze.</param>
    /// <param name="rundungMinuten">Auf diese Minuten werden angefangene Zeiten aufgerundet.</param>
    /// <param name="fahrzeugname">Liefert den Anzeigenamen eines Fahrzeugs.</param>
    /// <returns>Posten und Hinweise.</returns>
    public static Kostenaufstellung Berechnen(
        Einsatz einsatz,
        IReadOnlyCollection<Kostensatz> saetze,
        int rundungMinuten,
        Func<Guid, string> fahrzeugname)
    {
        var datum = Ortszeit.Datum(einsatz.Alarmiert);
        var gueltig = saetze.Where(s => s.IstGueltigAm(datum)).ToList();
        var posten = new List<Kostenposten>();
        var hinweise = new List<string>();
        var einsatzende = einsatz.EinsatzbereitschaftHergestellt ?? einsatz.Einsatzende;
        var platzhalter = false;
        Kostenposten Posten(Kostensatz satz, decimal menge, string bezeichnung)
        {
            platzhalter |= satz.IstPlatzhalter;
            return Kostenersatz.Posten(satz, menge, bezeichnung);
        }

        foreach (var f in einsatz.Fahrzeuge)
        {
            var name = fahrzeugname(f.FahrzeugId);
            var satz = gueltig.Where(s => s.Art == KostensatzArt.Fahrzeug && s.FahrzeugId == f.FahrzeugId).MaxBy(s => s.GueltigAb);
            if (satz is null)
            {
                hinweise.Add($"{name}: kein Kostensatz am {datum:dd.MM.yyyy} hinterlegt.");
                continue;
            }

            var von = f.Ausgerueckt ?? einsatz.Ausgerueckt ?? einsatz.Alarmiert;
            var bis = f.Einsatzbereit ?? f.Zurueck ?? einsatzende;
            if (bis is null || bis <= von)
            {
                hinweise.Add($"{name}: keine Endzeit, nicht berechnet.");
                continue;
            }

            posten.Add(Posten(satz, Menge(satz.Einheit, von, bis.Value, rundungMinuten), name));
        }

        // Gelten mehrere Sätze, zählt der jüngste (neue Fassung der Satzung ohne Ende der alten).
        var personal = gueltig.Where(s => s.Art == KostensatzArt.Personal).MaxBy(s => s.GueltigAb);
        if (einsatz.Teilnahmen.Count > 0)
        {
            if (personal is null)
            {
                hinweise.Add($"Personal: kein Kostensatz am {datum:dd.MM.yyyy} hinterlegt.");
            }
            else
            {
                decimal stunden = 0;
                var ohneEnde = 0;
                foreach (var t in einsatz.Teilnahmen)
                {
                    var von = t.Von ?? einsatz.Alarmiert;
                    var bis = t.Bis ?? einsatzende;
                    if (bis is null || bis <= von)
                    {
                        ohneEnde++;
                        continue;
                    }

                    stunden += Menge(personal.Einheit, von, bis.Value, rundungMinuten);
                }

                if (ohneEnde > 0)
                {
                    hinweise.Add($"Personal: {ohneEnde} Teilnahme(n) ohne Endzeit, nicht berechnet.");
                }

                var berechnet = einsatz.Teilnahmen.Count - ohneEnde;
                if (berechnet > 0)
                {
                    posten.Add(Posten(personal, stunden, $"{PersonalBezeichnung} ({berechnet} {(berechnet == 1 ? "Person" : "Personen")})"));
                }
            }
        }

        foreach (var a in einsatz.Ausruestung)
        {
            if (a.Kostensatz is null)
            {
                hinweise.Add("Ausrüstung ohne Kostensatz übersprungen.");
                continue;
            }

            posten.Add(Posten(a.Kostensatz, a.Kostensatz.Einheit == Kosteneinheit.Pauschale ? 1 : a.Menge, a.Kostensatz.Bezeichnung));
        }

        if (platzhalter)
        {
            hinweise.Insert(0, "Vorläufige Sätze (Platzhalter, z. B. aus dem Satzungsentwurf): Die Aufstellung ist nur ein Testwert und kein Bescheid.");
        }

        return new Kostenaufstellung(posten, hinweise);
    }

    /// <summary>Stunden bzw. Kalendertage der Leistungsdauer; Stunden auf angefangene Einheiten aufgerundet.</summary>
    public static decimal Menge(Kosteneinheit einheit, DateTimeOffset von, DateTimeOffset bis, int rundungMinuten)
    {
        var dauer = bis - von;
        return einheit switch
        {
            Kosteneinheit.Tag => Ortszeit.Kalendertage(von, bis),
            Kosteneinheit.Stunde => Stunden(dauer, rundungMinuten),
            _ => 1,
        };
    }

    /// <summary>Rechnet eine Dauer in Stunden um, aufgerundet auf angefangene Rundungseinheiten.</summary>
    /// <param name="dauer">Dauer der Leistung.</param>
    /// <param name="rundungMinuten">Rundungseinheit in Minuten, mindestens 1.</param>
    /// <returns>Stunden; 0 bei leerer oder negativer Dauer.</returns>
    public static decimal Stunden(TimeSpan dauer, int rundungMinuten)
    {
        if (dauer <= TimeSpan.Zero)
        {
            return 0;
        }

        var einheit = Math.Max(1, rundungMinuten);
        var minuten = (decimal)Math.Ceiling(dauer.TotalMinutes / einheit) * einheit;
        return Math.Round(minuten / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private static Kostenposten Posten(Kostensatz satz, decimal menge, string bezeichnung) =>
        new(satz.Art, bezeichnung, menge, satz.Einheit, satz.Betrag, Math.Round(menge * satz.Betrag, 2, MidpointRounding.AwayFromZero));
}
