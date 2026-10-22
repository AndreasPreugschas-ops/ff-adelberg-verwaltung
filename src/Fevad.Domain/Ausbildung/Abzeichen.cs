using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Eine geprüfte Bedingung, z. B. "Vorstufe Bronze vorhanden (2025)".</summary>
public sealed record Voraussetzung(string Text, bool Erfuellt);

/// <summary>
/// Abzeichen werden nicht beantragt, sondern am Abnahmetag erworben. Die Regeln (Vorstufe, Abstand, Alter,
/// nötige Lehrgänge) stehen am Katalogeintrag <see cref="Qualifikation"/>, damit sie ohne Programmänderung anpassbar sind.
/// </summary>
public static class Abzeichenpruefung
{
    /// <summary>
    /// Prüft die Voraussetzungen von <paramref name="ziel"/> am <paramref name="abnahmetag"/>.
    /// <paramref name="vorhandene"/> braucht geladene <see cref="QualifikationNachweis.Qualifikation"/>;
    /// <paramref name="namen"/> liefert Bezeichnungen für fehlende Codes.
    /// </summary>
    public static IReadOnlyList<Voraussetzung> Pruefen(
        Qualifikation ziel,
        DateOnly abnahmetag,
        DateOnly geburtsdatum,
        IEnumerable<QualifikationNachweis> vorhandene,
        DateOnly? jugendSeit,
        IReadOnlyDictionary<string, string>? namen = null)
    {
        var liste = vorhandene.Where(n => n.ErworbenAm <= abnahmetag).ToList();
        var ergebnis = new List<Voraussetzung>();
        var alter = ziel.AlterNachJahrgang ? abnahmetag.Year - geburtsdatum.Year : Mitglied.Alter(geburtsdatum, abnahmetag);
        var jahrgang = ziel.AlterNachJahrgang ? " (nach Jahrgang)" : "";

        if (ziel.MindestalterJahre is { } mindestens)
        {
            ergebnis.Add(new($"Mindestalter {mindestens}{jahrgang}, ist {alter}", alter >= mindestens));
        }

        if (ziel.HoechstalterJahre is { } hoechstens)
        {
            ergebnis.Add(new($"Höchstalter {hoechstens}{jahrgang}, ist {alter}", alter <= hoechstens));
        }

        if (ziel.VorstufeId is { } vorstufeId)
        {
            var name = ziel.Vorstufe?.Bezeichnung ?? "Vorstufe";
            var vorstufe = liste.Where(n => n.QualifikationId == vorstufeId).MinBy(n => n.ErworbenAm);
            if (vorstufe is null)
            {
                ergebnis.Add(new($"{name} fehlt", false));
            }
            else
            {
                var fruehestens = vorstufe.ErworbenAm.Year + (ziel.VorstufeAbstandJahre ?? 0);
                ergebnis.Add(abnahmetag.Year >= fruehestens
                    ? new($"{name} vorhanden ({vorstufe.ErworbenAm.Year})", true)
                    : new($"{name} erst {vorstufe.ErworbenAm.Year}, nächste Stufe frühestens {fruehestens}", false));
            }
        }

        foreach (var gruppe in Gruppen(ziel.Voraussetzungen))
        {
            var text = string.Join(" oder ", gruppe.Select(c => namen?.GetValueOrDefault(c) ?? c));
            ergebnis.Add(new(text, gruppe.Any(c => Gueltig(liste, c, abnahmetag))));
        }

        if (ziel.MindestJugendMonate is { } monate)
        {
            var erfuellt = jugendSeit is { } seit && seit.AddMonths(monate) <= abnahmetag;
            ergebnis.Add(new($"mindestens {monate} Monate Jugendfeuerwehr", erfuellt));
        }

        return ergebnis;
    }

    /// <summary>Codes aus "TM1; AGT|SPRF": jede Gruppe muss erfüllt sein, innerhalb einer Gruppe reicht ein Code.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Gruppen(string? voraussetzungen) =>
        (voraussetzungen ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => (IReadOnlyList<string>)g.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant()).ToList())
            .Where(g => g.Count > 0)
            .ToList();

    /// <summary>Am Stichtag erworben und nicht abgelaufen.</summary>
    public static bool Gueltig(IEnumerable<QualifikationNachweis> vorhandene, string code, DateOnly stichtag) =>
        vorhandene.Any(n => n.Qualifikation?.Code == code && n.ErworbenAm <= stichtag && (n.GueltigBis is null || n.GueltigBis >= stichtag));

    /// <summary>
    /// Nur die höchste Stufe zählt (z. B. Jugendflamme III statt I und II). Liefert die Nachweise, deren
    /// Qualifikation Vorstufe einer anderen vorhandenen ist, auch über mehrere Stufen.
    /// </summary>
    public static IReadOnlySet<Guid> Abgeloest(IEnumerable<QualifikationNachweis> vorhandene, IReadOnlyDictionary<Guid, Qualifikation> katalog)
    {
        var abgeloest = new HashSet<Guid>();
        foreach (var id in vorhandene.Select(n => n.QualifikationId).Distinct())
        {
            var besucht = new HashSet<Guid> { id };
            var stufe = katalog.GetValueOrDefault(id)?.VorstufeId;
            while (stufe is { } vorstufe && besucht.Add(vorstufe))
            {
                abgeloest.Add(vorstufe);
                stufe = katalog.GetValueOrDefault(vorstufe)?.VorstufeId;
            }
        }

        return vorhandene.Where(n => abgeloest.Contains(n.QualifikationId)).Select(n => n.Id).ToHashSet();
    }
}

/// <summary>Kann ein Mitglied am Abnahmetag eine Funktion in der Gruppe übernehmen?</summary>
public sealed record Funktionspruefung(string Funktion, bool Erfuellt, string Hinweis);

/// <summary>
/// Voraussetzungen am Abnahmetag des Leistungsabzeichens BW (Richtlinie Neufassung 2026):
/// Maschinist mit gültiger Fahrerlaubnis, Atemschutzgeräteträger mit gültiger Eignung und
/// Belastungsübung nicht älter als <c>belastungsuebungMonate</c>, Gruppenführer mit Lehrgang.
/// </summary>
public static class Leistungsabzeichen
{
    public static IReadOnlyList<Funktionspruefung> Funktionen(
        DateOnly abnahmetag,
        IReadOnlyList<QualifikationNachweis> vorhandene,
        IReadOnlyList<AtemschutzVorsorge>? vorsorgen,
        IReadOnlyList<AtemschutzNachweis>? atemschutz,
        int belastungsuebungMonate)
    {
        var ergebnis = new List<Funktionspruefung>();

        var fahrerlaubnisse = vorhandene
            .Where(n => n.Qualifikation?.Art == QualifikationArt.Fahrerlaubnis && n.ErworbenAm <= abnahmetag && (n.GueltigBis is null || n.GueltigBis >= abnahmetag))
            .Select(n => n.Qualifikation!.Kurzbezeichnung ?? n.Qualifikation.Code)
            .Distinct()
            .ToList();
        var maschinist = Abzeichenpruefung.Gueltig(vorhandene, Qualifikation.CodeMaschinist, abnahmetag);
        ergebnis.Add(new("Maschinist", maschinist && fahrerlaubnisse.Count > 0,
            !maschinist ? "Lehrgang fehlt"
            : fahrerlaubnisse.Count == 0 ? "keine gültige Fahrerlaubnis"
            : "Fahrerlaubnis " + string.Join(", ", fahrerlaubnisse)));

        // Ohne Atemschutz-Leserecht werden keine Vorsorgedaten übergeben; dann bleibt die Spalte offen.
        if (vorsorgen is not null && atemschutz is not null)
        {
            var agt = Abzeichenpruefung.Gueltig(vorhandene, Qualifikation.CodeAtemschutz, abnahmetag);
            var eignung = vorsorgen
                .Where(v => v.UntersuchtAm <= abnahmetag)
                .MaxBy(v => v.UntersuchtAm);
            var eignungGueltig = eignung is not null && eignung.Ergebnis != VorsorgeErgebnis.NichtGeeignet && eignung.NaechsteFaellig >= abnahmetag;
            var belastung = atemschutz
                .Where(n => n.Art == AtemschutzNachweisArt.Belastungsuebung && n.Datum <= abnahmetag)
                .MaxBy(n => n.Datum);
            var belastungGueltig = belastung is not null && belastung.Datum >= abnahmetag.AddMonths(-belastungsuebungMonate);
            ergebnis.Add(new("Atemschutz", agt && eignungGueltig && belastungGueltig,
                !agt ? "Lehrgang fehlt"
                : !eignungGueltig ? "Eignung am Abnahmetag nicht gültig"
                : !belastungGueltig ? $"Belastungsübung älter als {belastungsuebungMonate} Monate"
                : "Eignung und Belastungsübung gültig"));
        }

        var gf = Abzeichenpruefung.Gueltig(vorhandene, Qualifikation.CodeGruppenfuehrer, abnahmetag);
        ergebnis.Add(new("Gruppenführer", gf, gf ? "Lehrgang vorhanden" : "Lehrgang fehlt"));
        return ergebnis;
    }
}

/// <summary>
/// Leistungsspange DJF: Bei Eintritt in die Jugendfeuerwehr ab dem Stichtag zählt die Jugendzeit für das
/// Feuerwehr-Ehrenzeichen nur mit Spange. Die VwV sagt "soll", in FEVAD ist sie Pflicht (Andi, 22.10.2026).
/// </summary>
public static class Leistungsspange
{
    public sealed record Regeln(DateOnly PflichtAbEintritt, int HinweisAbAlter, int UebertrittAlter, int WarnungTage);

    /// <summary>Hinweis für ein Mitglied der Jugendfeuerwehr ohne Spange, fällig zum Übertritt; sonst null.</summary>
    public static Fristeintrag? Hinweis(Guid mitgliedId, string mitglied, DateOnly geburtsdatum, DateOnly jugendSeit, bool hatSpange, DateOnly stichtag, Regeln regeln)
    {
        if (hatSpange || jugendSeit < regeln.PflichtAbEintritt || Mitglied.Alter(geburtsdatum, stichtag) < regeln.HinweisAbAlter)
        {
            return null;
        }

        // Ab dem Hinweisalter gelb, im letzten Zeitraum vor dem Übertritt rot.
        var faellig = geburtsdatum.AddYears(regeln.UebertrittAlter);
        var ampel = faellig <= stichtag.AddDays(regeln.WarnungTage) ? Fristampel.Rot : Fristampel.Gelb;
        return new Fristeintrag(mitgliedId, mitglied, Fristbereich.Jugend, "Leistungsspange DJF fehlt", faellig, ampel);
    }
}
