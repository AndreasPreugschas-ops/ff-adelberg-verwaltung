namespace Fevad.Domain.Kassen;

/// <summary>Rechenregeln des Kassenbuchs: Wirkung einer Buchung, Kontostand, Ist je Position, Deckung, Belegpflicht und Prüfungen.</summary>
public static class Kassenregeln
{
    /// <summary>Wirkung einer Buchung auf ein Konto: Einnahmen erhöhen, Ausgaben mindern, Umbuchungen verschieben; ein Storno kehrt um.</summary>
    /// <param name="buchung">Die Buchung.</param>
    /// <param name="kontoId">Das betrachtete Konto.</param>
    /// <returns>Betrag mit Vorzeichen, 0 wenn die Buchung das Konto nicht berührt.</returns>
    public static decimal Wirkung(Buchung buchung, Guid kontoId)
    {
        var vorzeichen = buchung.StornoVonId is null ? 1 : -1;
        var wirkung = buchung.Art switch
        {
            BuchungArt.Einnahme when buchung.KontoId == kontoId => buchung.Betrag,
            BuchungArt.Ausgabe when buchung.KontoId == kontoId => -buchung.Betrag,
            BuchungArt.Umbuchung when buchung.KontoId == kontoId => -buchung.Betrag,
            BuchungArt.Umbuchung when buchung.GegenkontoId == kontoId => buchung.Betrag,
            _ => 0m,
        };
        return vorzeichen * wirkung;
    }

    /// <summary>Kontostand am Ende eines Tages aus Anfangsbestand und allen Buchungen bis dahin.</summary>
    /// <param name="konto">Das Konto.</param>
    /// <param name="buchungen">Buchungen der Kasse; fremde Konten zählen nicht.</param>
    /// <param name="bis">Letzter Tag, der mitzählt.</param>
    /// <returns>Der Bestand in Euro.</returns>
    public static decimal Kontostand(Zahlungsmittelkonto konto, IEnumerable<Buchung> buchungen, DateOnly bis) =>
        konto.Anfangsbestand + buchungen.Where(b => b.Datum <= bis).Sum(b => Wirkung(b, konto.Id));

    /// <summary>Ist-Betrag einer Buchung für den Wirtschaftsplan: positiv auf ihrer Position, ein Storno negativ.</summary>
    /// <param name="buchung">Die Buchung.</param>
    /// <returns>Der Betrag mit Vorzeichen.</returns>
    public static decimal Ist(Buchung buchung) => buchung.StornoVonId is null ? buchung.Betrag : -buchung.Betrag;

    /// <summary>Ist je Position aus den Buchungen eines Jahres.</summary>
    /// <param name="buchungen">Buchungen des Jahres.</param>
    /// <returns>Summe je Position; Buchungen ohne Position fehlen.</returns>
    public static IReadOnlyDictionary<Guid, decimal> IstJePosition(IEnumerable<Buchung> buchungen) =>
        buchungen.Where(b => b.PlanpositionId is not null)
            .GroupBy(b => b.PlanpositionId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(Ist));

    /// <summary>
    /// Noch verfügbare Mittel einer Ausgabeposition: Ansatz minus Ist, bei deckungsfähigen Positionen zuzüglich der freien Mittel der
    /// übrigen deckungsfähigen Positionen derselben Hauptgruppe (Überschreitungen dort mindern nicht).
    /// </summary>
    /// <param name="position">Die Position.</param>
    /// <param name="positionen">Alle Positionen der Kasse.</param>
    /// <param name="ansaetze">Ansätze des Jahres je Position.</param>
    /// <param name="ist">Ist des Jahres je Position.</param>
    /// <returns>Verfügbarer Betrag, negativ bei Überschreitung.</returns>
    public static decimal Verfuegbar(
        Planposition position, IEnumerable<Planposition> positionen, IReadOnlyDictionary<Guid, decimal> ansaetze, IReadOnlyDictionary<Guid, decimal> ist)
    {
        decimal Frei(Planposition p) => ansaetze.GetValueOrDefault(p.Id) - ist.GetValueOrDefault(p.Id);

        var eigene = Frei(position);
        if (!position.Deckungsfaehig)
        {
            return eigene;
        }

        var andere = positionen.Where(p => p.Id != position.Id && p.Deckungsfaehig && p.Richtung == position.Richtung && p.Hauptgruppe == position.Hauptgruppe)
            .Sum(p => Math.Max(0, Frei(p)));
        return eigene + andere;
    }

    /// <summary>
    /// Ausgaben über der Grenze sind Sache des Feuerwehrausschusses (Andi, 25.10.2026). FEVAD sperrt nichts, sondern weist darauf hin
    /// und bietet den Verweis auf den Beschluss an.
    /// </summary>
    /// <param name="art">Art der Buchung.</param>
    /// <param name="betrag">Betrag in Euro.</param>
    /// <param name="grenze">Grenze aus dem Parameter; 0 schaltet den Hinweis ab.</param>
    /// <returns>true, wenn der Hinweis erscheinen soll.</returns>
    public static bool BeschlussHinweis(BuchungArt art, decimal betrag, int grenze) =>
        art == BuchungArt.Ausgabe && grenze > 0 && betrag > grenze;

    /// <summary>Die Buchung hat einen Beleg: mindestens eine Datei oder einen Eigenbeleg mit Begründung.</summary>
    /// <param name="belegart">Art des Belegs.</param>
    /// <param name="begruendung">Begründung des Eigenbelegs.</param>
    /// <param name="dateien">Zahl der hochgeladenen Dateien.</param>
    /// <returns>true, wenn sich die Buchung festschreiben lässt.</returns>
    public static bool BelegVollstaendig(Belegart belegart, string? begruendung, int dateien) =>
        belegart == Belegart.Eigenbeleg ? !string.IsNullOrWhiteSpace(begruendung) : dateien > 0;

    /// <summary>Prüft eine Buchung vor dem Speichern.</summary>
    /// <param name="buchung">Die Buchung mit gesetzten Schlüsseln.</param>
    /// <param name="konten">Konten der Kasse.</param>
    /// <param name="positionen">Positionen der Kasse.</param>
    /// <returns>Fehlermeldungen; leer, wenn alles passt.</returns>
    public static IReadOnlyList<string> Pruefen(Buchung buchung, IEnumerable<Zahlungsmittelkonto> konten, IEnumerable<Planposition> positionen)
    {
        var fehler = new List<string>();
        var kontenListe = konten.ToList();
        if (buchung.Betrag <= 0)
        {
            fehler.Add("Der Betrag muss größer als 0 sein.");
        }

        var konto = kontenListe.FirstOrDefault(k => k.Id == buchung.KontoId);
        if (konto is null)
        {
            fehler.Add("Das Konto gehört nicht zu dieser Kasse.");
        }
        else if (buchung.Datum < konto.Eroeffnet)
        {
            fehler.Add($"Das Konto „{konto.Bezeichnung}“ wird erst ab {konto.Eroeffnet:dd.MM.yyyy} in FEVAD geführt.");
        }

        var position = buchung.PlanpositionId is { } p ? positionen.FirstOrDefault(x => x.Id == p) : null;
        if (buchung.PlanpositionId is not null && position is null)
        {
            fehler.Add("Die Position gehört nicht zu dieser Kasse.");
        }

        if (buchung.Art == BuchungArt.Umbuchung)
        {
            var gegen = kontenListe.FirstOrDefault(k => k.Id == buchung.GegenkontoId);
            if (gegen is null || gegen.Id == buchung.KontoId)
            {
                fehler.Add("Eine Umbuchung braucht ein anderes Konto derselben Kasse als Ziel.");
            }
            else if (buchung.Datum < gegen.Eroeffnet)
            {
                fehler.Add($"Das Konto „{gegen.Bezeichnung}“ wird erst ab {gegen.Eroeffnet:dd.MM.yyyy} in FEVAD geführt.");
            }
        }
        else
        {
            if (position is null && buchung.PlanpositionId is null)
            {
                fehler.Add("Bitte die Position des Wirtschaftsplans wählen.");
            }
            else if (position is not null && position.Richtung != (buchung.Art == BuchungArt.Einnahme ? Planrichtung.Einnahme : Planrichtung.Ausgabe))
            {
                fehler.Add($"Position {position.Nummer} ist eine {(position.Richtung == Planrichtung.Einnahme ? "Einnahme" : "Ausgabe")}.");
            }
        }

        if (buchung.Belegart == Belegart.Eigenbeleg && string.IsNullOrWhiteSpace(buchung.EigenbelegBegruendung))
        {
            fehler.Add("Ein Eigenbeleg braucht eine Begründung.");
        }

        return fehler;
    }

    /// <summary>Stornobuchung zu einer Buchung: gleiche Art, gleicher Betrag, gleiche Konten und Position, Wirkung umgekehrt.</summary>
    /// <param name="original">Die zu stornierende Buchung.</param>
    /// <param name="datum">Tag des Stornos.</param>
    /// <param name="grund">Grund des Stornos.</param>
    /// <returns>Die neue Buchung ohne Belegnummer.</returns>
    public static Buchung Storno(Buchung original, DateOnly datum, string grund) => new()
    {
        KasseId = original.KasseId,
        Datum = datum,
        Jahr = datum.Year,
        Art = original.Art,
        Betrag = original.Betrag,
        KontoId = original.KontoId,
        GegenkontoId = original.GegenkontoId,
        PlanpositionId = original.PlanpositionId,
        Partner = original.Partner,
        Text = $"Storno {original.Kennung}: {grund}",
        Belegart = Belegart.Eigenbeleg,
        EigenbelegBegruendung = $"Storno von Beleg {original.Kennung}: {grund}",
        StornoVonId = original.Id,
    };

    /// <summary>Gliederung der Kameradschaftskasse nach dem Muster der LFV-Handreichung „Kameradschaftskassen“ (2. Auflage 2026).</summary>
    public static IReadOnlyList<(string Nummer, string Bezeichnung, Planrichtung Richtung, bool Deckungsfaehig, string? Hinweis)> MusterKameradschaftskasse =>
    [
        ("0", "Entnahme aus Rücklagen", Planrichtung.Einnahme, false, "Als Umbuchung vom Rücklagenkonto buchen."),
        ("1.1", "Zuwendung der Gemeinde", Planrichtung.Einnahme, false, "Entschädigungssatzung § 5 Abs. 1."),
        ("1.2", "Zuwendungen Dritter und Spenden", Planrichtung.Einnahme, false, "Die Kasse darf keine Zuwendungsbestätigung ausstellen."),
        ("2", "Erträge aus Veranstaltungen", Planrichtung.Einnahme, false, null),
        ("3", "Sonstige Einnahmen", Planrichtung.Einnahme, false, "z. B. Zinsen, Erstattungen, Brandsicherheitswachen."),
        ("5", "Ausgaben für Veranstaltungen", Planrichtung.Ausgabe, false, null),
        ("6.1", "Kameradschaftspflege", Planrichtung.Ausgabe, true, "Versammlungen, Hauptübung, Jahresausflug."),
        ("6.2", "Ehrungen, Geschenke, Jubiläen", Planrichtung.Ausgabe, true, null),
        ("7", "Anschaffungen", Planrichtung.Ausgabe, false, "Fahrzeuge, Geräte und Schutzausrüstung zahlt die Gemeinde (§ 3 FwG), nicht die Kasse."),
        ("8", "Sonstige Ausgaben", Planrichtung.Ausgabe, false, "z. B. Kontoführung, Porto."),
        ("9", "Zuführung an Rücklagen", Planrichtung.Ausgabe, false, "Als Umbuchung auf das Rücklagenkonto buchen."),
    ];

    /// <summary>Gliederung der Kasse der Jugendfeuerwehr, angelehnt an das LFV-Muster.</summary>
    public static IReadOnlyList<(string Nummer, string Bezeichnung, Planrichtung Richtung, bool Deckungsfaehig, string? Hinweis)> MusterJugendkasse =>
    [
        ("0", "Entnahme aus Rücklagen", Planrichtung.Einnahme, false, "Als Umbuchung vom Rücklagenkonto buchen."),
        ("1.1", "Zuwendung der Gemeinde (Jugendförderung)", Planrichtung.Einnahme, false, "Entschädigungssatzung § 5 Abs. 2."),
        ("1.2", "Zuschüsse Kreisjugendring und Jugendpflege", Planrichtung.Einnahme, false, null),
        ("1.3", "Spenden", Planrichtung.Einnahme, false, "Die Kasse darf keine Zuwendungsbestätigung ausstellen."),
        ("2", "Teilnehmerbeiträge Fahrten und Zeltlager", Planrichtung.Einnahme, false, null),
        ("3", "Sonstige Einnahmen", Planrichtung.Einnahme, false, null),
        ("5", "Zeltlager und Fahrten", Planrichtung.Ausgabe, true, null),
        ("6.1", "Jugendarbeit und Gruppenstunden", Planrichtung.Ausgabe, true, null),
        ("6.2", "Wettbewerbe und Abzeichen", Planrichtung.Ausgabe, true, null),
        ("7", "Anschaffungen", Planrichtung.Ausgabe, false, "Fahrzeuge, Geräte und Schutzausrüstung zahlt die Gemeinde (§ 3 FwG), nicht die Kasse."),
        ("8", "Sonstige Ausgaben", Planrichtung.Ausgabe, false, null),
        ("9", "Zuführung an Rücklagen", Planrichtung.Ausgabe, false, "Als Umbuchung auf das Rücklagenkonto buchen."),
    ];
}
