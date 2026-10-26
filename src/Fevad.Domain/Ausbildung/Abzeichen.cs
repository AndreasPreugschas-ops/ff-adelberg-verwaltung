using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Eine geprüfte Bedingung, z. B. "Vorstufe Bronze vorhanden (2025)".</summary>
/// <param name="Text">Beschreibung der Bedingung mit dem festgestellten Wert.</param>
/// <param name="Erfuellt">Die Bedingung ist am Abnahmetag erfüllt.</param>
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
    /// <param name="ziel">Das angestrebte Abzeichen mit seinen Regeln.</param>
    /// <param name="abnahmetag">Tag der Abnahme; Alter und vorhandene Nachweise werden zu diesem Tag bewertet.</param>
    /// <param name="geburtsdatum">Geburtsdatum des Mitglieds.</param>
    /// <param name="vorhandene">Qualifikationsnachweise des Mitglieds.</param>
    /// <param name="jugendSeit">Eintritt in die Jugendfeuerwehr, für die Mindestdauer; leer, wenn nie in der Jugendfeuerwehr.</param>
    /// <param name="namen">Bezeichnungen je Code für die Anzeige der Voraussetzungen; ohne Angabe erscheinen die Codes.</param>
    /// <param name="regel">
    /// Regeln der Stufe im gewählten Regelwerk (V50a); ihre gesetzten Werte ersetzen Mindestalter und Voraussetzungen des Katalogs,
    /// <see cref="AbzeichenregelwerkStufe.NachweisHoechstensMonate"/> begrenzt das Alter der Nachweise. Ohne Angabe gilt der Katalog.
    /// </param>
    /// <returns>Die geprüften Bedingungen in der Reihenfolge Alter, Vorstufe, Lehrgänge, Jugendzeit.</returns>
    public static IReadOnlyList<Voraussetzung> Pruefen(
        Qualifikation ziel,
        DateOnly abnahmetag,
        DateOnly geburtsdatum,
        IEnumerable<QualifikationNachweis> vorhandene,
        DateOnly? jugendSeit,
        IReadOnlyDictionary<string, string>? namen = null,
        AbzeichenregelwerkStufe? regel = null)
    {
        var liste = vorhandene.Where(n => n.ErworbenAm <= abnahmetag).ToList();
        var ergebnis = new List<Voraussetzung>();
        var alter = ziel.AlterNachJahrgang ? abnahmetag.Year - geburtsdatum.Year : Mitglied.Alter(geburtsdatum, abnahmetag);
        var jahrgang = ziel.AlterNachJahrgang ? " (nach Jahrgang)" : "";

        if ((regel?.MindestalterJahre ?? ziel.MindestalterJahre) is { } mindestens)
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

        // Ein Regelwerk kann verlangen, dass der Nachweis frisch ist, z. B. Erste Hilfe höchstens zwei Jahre alt.
        var hoechstensMonate = regel?.NachweisHoechstensMonate;
        var fruehestensErworben = hoechstensMonate is { } m ? abnahmetag.AddMonths(-m) : DateOnly.MinValue;
        var frische = hoechstensMonate is { } n ? $" (höchstens {n} Monate alt)" : "";
        foreach (var gruppe in Gruppen(regel?.Voraussetzungen ?? ziel.Voraussetzungen))
        {
            var text = string.Join(" oder ", gruppe.Select(c => namen?.GetValueOrDefault(c) ?? c)) + frische;
            ergebnis.Add(new(text, gruppe.Any(c => Gueltig(liste.Where(x => x.ErworbenAm >= fruehestensErworben), c, abnahmetag))));
        }

        if (ziel.MindestJugendMonate is { } monate)
        {
            var erfuellt = jugendSeit is { } seit && seit.AddMonths(monate) <= abnahmetag;
            ergebnis.Add(new($"mindestens {monate} Monate Jugendfeuerwehr", erfuellt));
        }

        return ergebnis;
    }

    /// <summary>Codes aus "TM1; AGT|SPRF": jede Gruppe muss erfüllt sein, innerhalb einer Gruppe reicht ein Code.</summary>
    /// <param name="voraussetzungen">Voraussetzungen als Text; leer ergibt keine Gruppen.</param>
    /// <returns>Die Gruppen mit Codes in Großbuchstaben.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> Gruppen(string? voraussetzungen) =>
        (voraussetzungen ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => (IReadOnlyList<string>)g.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant()).ToList())
            .Where(g => g.Count > 0)
            .ToList();

    /// <summary>Am Stichtag erworben und nicht abgelaufen.</summary>
    /// <param name="vorhandene">Nachweise mit geladener Qualifikation.</param>
    /// <param name="code">Code der gesuchten Qualifikation in Großbuchstaben.</param>
    /// <param name="stichtag">Tag, an dem der Nachweis gültig sein muss.</param>
    /// <returns>true, wenn ein passender Nachweis am Stichtag gilt.</returns>
    public static bool Gueltig(IEnumerable<QualifikationNachweis> vorhandene, string code, DateOnly stichtag) =>
        vorhandene.Any(n => n.Qualifikation?.Code == code && n.ErworbenAm <= stichtag && (n.GueltigBis is null || n.GueltigBis >= stichtag));

    /// <summary>
    /// Nur die höchste Stufe zählt (z. B. Jugendflamme III statt I und II). Liefert die Nachweise, deren
    /// Qualifikation Vorstufe einer anderen vorhandenen ist, auch über mehrere Stufen.
    /// </summary>
    /// <param name="vorhandene">Nachweise eines Mitglieds.</param>
    /// <param name="katalog">Alle Qualifikationen nach Schlüssel, mit <see cref="Qualifikation.VorstufeId"/>.</param>
    /// <returns>Die Schlüssel der abgelösten Nachweise.</returns>
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
/// <param name="Funktion">Funktion in der Gruppe, z. B. „Maschinist“.</param>
/// <param name="Erfuellt">Alle Voraussetzungen für die Funktion sind am Abnahmetag erfüllt.</param>
/// <param name="Hinweis">Was fehlt oder was vorhanden ist, für die Anzeige.</param>
public sealed record Funktionspruefung(string Funktion, bool Erfuellt, string Hinweis);

/// <summary>
/// Voraussetzungen am Abnahmetag des Leistungsabzeichens BW (Richtlinie Neufassung 2026):
/// Maschinist mit gültiger Fahrerlaubnis, Atemschutzgeräteträger mit gültiger Eignung und
/// Belastungsübung nicht älter als <c>belastungsuebungMonate</c>, Gruppenführer mit Lehrgang.
/// </summary>
public static class Leistungsabzeichen
{
    /// <summary>Prüft für Maschinist, Atemschutz und Gruppenführer, ob das Mitglied die Funktion am Abnahmetag übernehmen kann.</summary>
    /// <param name="abnahmetag">Tag der Abnahme.</param>
    /// <param name="vorhandene">Qualifikationsnachweise mit geladener Qualifikation.</param>
    /// <param name="vorsorgen">Atemschutz-Vorsorgen; null ohne Atemschutz-Leserecht, dann entfällt die Atemschutz-Prüfung.</param>
    /// <param name="atemschutz">Atemschutznachweise; null ohne Atemschutz-Leserecht.</param>
    /// <param name="belastungsuebungMonate">So alt darf die letzte Belastungsübung höchstens sein.</param>
    /// <returns>Je geprüfte Funktion ein Ergebnis.</returns>
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
    /// <summary>Regeln aus den Parametern für den Hinweis auf eine fehlende Leistungsspange.</summary>
    /// <param name="PflichtAbEintritt">Ab diesem Eintritt in die Jugendfeuerwehr zählt die Jugendzeit nur mit Spange.</param>
    /// <param name="HinweisAbAlter">Ab diesem Alter erscheint der Hinweis.</param>
    /// <param name="UebertrittAlter">Alter des Übertritts in die Einsatzabteilung; dann ist die Spange fällig.</param>
    /// <param name="WarnungTage">So viele Tage vor dem Übertritt wird der Hinweis rot.</param>
    public sealed record Regeln(DateOnly PflichtAbEintritt, int HinweisAbAlter, int UebertrittAlter, int WarnungTage);

    /// <summary>Hinweis für ein Mitglied der Jugendfeuerwehr ohne Spange, fällig zum Übertritt; sonst null.</summary>
    /// <param name="mitgliedId">Schlüssel des Mitglieds.</param>
    /// <param name="mitglied">Anzeigename des Mitglieds.</param>
    /// <param name="geburtsdatum">Geburtsdatum.</param>
    /// <param name="jugendSeit">Eintritt in die Jugendfeuerwehr.</param>
    /// <param name="hatSpange">Die Leistungsspange ist schon eingetragen.</param>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <param name="regeln">Regeln aus den Parametern.</param>
    /// <returns>Der Fristeintrag oder null, wenn kein Hinweis nötig ist.</returns>
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
