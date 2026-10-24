using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <param name="Menge">Stunden, Tage oder Stück.</param>
/// <param name="Satz">Euro je Einheit.</param>
public sealed record Kostenposten(KostensatzArt Art, string Bezeichnung, decimal Menge, Kosteneinheit Einheit, decimal Satz, decimal Betrag);

/// <param name="Posten">Berechnete Posten in der Reihenfolge Fahrzeuge, Personal, Ausrüstung.</param>
/// <param name="Hinweise">Was nicht berechnet werden konnte, z. B. ein Fahrzeug ohne Satz oder ohne Endzeit.</param>
public sealed record Kostenaufstellung(IReadOnlyList<Kostenposten> Posten, IReadOnlyList<string> Hinweise)
{
    public decimal Summe => Posten.Sum(p => p.Betrag);
}

/// <summary>
/// Berechnet den Kostenersatz eines Einsatzes aus den Zeiten je Fahrzeug und Person sowie der Ausrüstung.
/// Die Leistungsdauer beginnt beim Fahrzeug mit dem Ausrücken und endet mit der Einsatzbereitschaft,
/// beim Personal mit der Alarmierung bis zur Einsatzbereitschaft (§ 34 FwG, Muster Kostenersatzsatzung § 6);
/// fehlende Einzelzeiten erben die Zeiten des Einsatzes. Angefangene Einheiten werden auf
/// <paramref name="rundungMinuten"/> aufgerundet (Parameter, keine Konstante).
/// </summary>
public static class Kostenersatz
{
    public const string PersonalBezeichnung = "Einsatzkräfte";

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

        foreach (var f in einsatz.Fahrzeuge)
        {
            var name = fahrzeugname(f.FahrzeugId);
            var satz = gueltig.FirstOrDefault(s => s.Art == KostensatzArt.Fahrzeug && s.FahrzeugId == f.FahrzeugId);
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

        var personal = gueltig.FirstOrDefault(s => s.Art == KostensatzArt.Personal);
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
