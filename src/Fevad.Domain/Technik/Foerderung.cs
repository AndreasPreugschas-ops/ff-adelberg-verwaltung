using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Stand eines Zuwendungsantrags nach VwV Z-Feu (V30).</summary>
public enum Foerderstatus
{
    /// <summary>Vorhaben wird vorbereitet.</summary>
    [Display(Name = "in Vorbereitung")] Vorbereitung,
    /// <summary>Vorgespräch mit dem Kreisbrandmeister geführt.</summary>
    [Display(Name = "mit KBM abgestimmt")] KbmAbgestimmt,
    /// <summary>Gemeinderat hat beschlossen, die Maßnahme steht im Haushalt.</summary>
    [Display(Name = "Gemeinderat beschlossen")] GemeinderatBeschlossen,
    /// <summary>Antrag über die Gemeinde beim Landratsamt gestellt.</summary>
    [Display(Name = "beantragt")] Beantragt,
    /// <summary>Zuwendungsbescheid liegt vor; erst jetzt darf das Vorhaben beginnen.</summary>
    [Display(Name = "bewilligt")] Bewilligt,
    /// <summary>Verwendungsnachweis eingereicht.</summary>
    [Display(Name = "Verwendungsnachweis eingereicht")] NachweisEingereicht,
    /// <summary>Zuwendung ausgezahlt; endgültig.</summary>
    [Display(Name = "ausgezahlt")] Ausgezahlt,
    /// <summary>Antrag abgelehnt oder zurückgezogen.</summary>
    [Display(Name = "abgelehnt/zurückgezogen")] Abgelehnt,
}

/// <summary>Fördergegenstand der VwV Z-Feu mit Festbetrag (Katalog, vom Kommandanten pflegbar).</summary>
public class Foerdergegenstand : Katalogeintrag
{
    /// <summary>Festbetrag der Zuwendung in Euro.</summary>
    public decimal Festbetrag { get; set; }

    /// <summary>Höherer Festbetrag bei Beschaffung über einen Rahmenvertrag, falls die VwV einen vorsieht.</summary>
    public decimal? FestbetragRahmenvertrag { get; set; }

    /// <summary>Zweckbindung in Jahren ab Indienststellung (VwV Z-Feu Nr. 6.5.4: 10 Jahre bis 4,75 t, sonst 20 Jahre).</summary>
    public int ZweckbindungJahre { get; set; }

    /// <summary>Letzter Tag, an dem der Festbetrag gilt (Laufzeit der VwV).</summary>
    public DateOnly GueltigBis { get; set; }

    /// <summary>Fundstelle in der VwV bzw. Hinweis zur Herkunft des Betrags.</summary>
    public string? Fundstelle { get; set; }
}

/// <summary>Zuwendungsantrag nach VwV Z-Feu zu einer Beschaffung, von der Abstimmung mit dem KBM bis zur Auszahlung.</summary>
public class Foerderantrag : Entitaet
{
    /// <summary>Beschaffung, zu der der Antrag gehört (höchstens ein Antrag je Beschaffung).</summary>
    public Guid BeschaffungId { get; set; }
    /// <summary>Navigation zur Beschaffung.</summary>
    public Beschaffung? Beschaffung { get; set; }

    /// <summary>Fördergegenstand aus dem Katalog.</summary>
    public Guid? FoerdergegenstandId { get; set; }
    /// <summary>Navigation zum Fördergegenstand.</summary>
    public Foerdergegenstand? Foerdergegenstand { get; set; }

    /// <summary>Beschaffung über einen Rahmenvertrag (höherer Festbetrag).</summary>
    public bool Rahmenvertrag { get; set; }

    /// <summary>Stand des Verfahrens.</summary>
    public Foerderstatus Status { get; set; }

    /// <summary>Jahr, dessen Antragsfrist (15. Februar) gilt.</summary>
    public int Antragsjahr { get; set; }

    /// <summary>Datum des Vorgesprächs mit dem Kreisbrandmeister.</summary>
    public DateOnly? KbmAbgestimmtAm { get; set; }

    /// <summary>Datum des Gemeinderatsbeschlusses.</summary>
    public DateOnly? GemeinderatsbeschlussAm { get; set; }

    /// <summary>Nummer oder Fundstelle des Beschlusses.</summary>
    public string? Gemeinderatsbeschluss { get; set; }

    /// <summary>Tag, an dem die Gemeinde den Antrag gestellt hat.</summary>
    public DateOnly? BeantragtAm { get; set; }

    /// <summary>Datum des Zuwendungsbescheids.</summary>
    public DateOnly? BescheidAm { get; set; }

    /// <summary>Aktenzeichen des Zuwendungsbescheids.</summary>
    public string? BescheidAktenzeichen { get; set; }

    /// <summary>Bewilligte Zuwendung in Euro.</summary>
    public decimal? Zuwendung { get; set; }

    /// <summary>Fahrzeug, das ersetzt wird; es muss vor dem Verwendungsnachweis außer Dienst sein.</summary>
    public Guid? AltfahrzeugId { get; set; }
    /// <summary>Navigation zum Altfahrzeug.</summary>
    public Fahrzeug? Altfahrzeug { get; set; }

    /// <summary>Beschafftes Fahrzeug; bekommt Förderung und Zweckbindung in die Fahrzeugakte.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Navigation zum beschafften Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Checkliste Verwendungsnachweis: Rechnung liegt vor.</summary>
    public bool RechnungLiegtVor { get; set; }

    /// <summary>Checkliste: Richtigkeitsbescheinigung der Gemeinde liegt vor.</summary>
    public bool RichtigkeitsbescheinigungLiegtVor { get; set; }

    /// <summary>Checkliste: feuerwehrtechnischer Abnahmebericht liegt vor.</summary>
    public bool AbnahmeberichtLiegtVor { get; set; }

    /// <summary>Checkliste: Erklärung „Beladung nach DIN vorhanden und verlastet“ (VwV Z-Feu Nr. 6.6.2).</summary>
    public bool BeladungserklaerungLiegtVor { get; set; }

    /// <summary>Tag, an dem der Verwendungsnachweis eingereicht wurde.</summary>
    public DateOnly? NachweisEingereichtAm { get; set; }

    /// <summary>Tag der Auszahlung.</summary>
    public DateOnly? AusgezahltAm { get; set; }

    /// <summary>Freitext, nur Sachstand.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Erwartete Zuwendung: bewilligter Betrag, sonst Festbetrag des Katalogs (mit Rahmenvertrag der höhere).</summary>
    public decimal? ErwarteterBetrag =>
        Zuwendung ?? (Foerdergegenstand is null ? null
            : Rahmenvertrag && Foerdergegenstand.FestbetragRahmenvertrag is { } rahmen ? rahmen : Foerdergegenstand.Festbetrag);

    /// <summary>Antragsfrist im Antragsjahr.</summary>
    /// <param name="monat">Monat der Frist (Parameter, 2).</param>
    /// <param name="tag">Tag der Frist (Parameter, 15).</param>
    /// <returns>Letzter Tag für den Antrag.</returns>
    public DateOnly Antragsfrist(int monat, int tag) => new(Antragsjahr, monat, Math.Clamp(tag, 1, DateTime.DaysInMonth(Antragsjahr, monat)));

    /// <summary>Vor der Bewilligung darf das Vorhaben nicht beginnen (keine Bestellung, kein Vertrag).</summary>
    public bool BeginnGesperrt => Status is < Foerderstatus.Bewilligt;

    /// <summary>Verfahren läuft noch.</summary>
    public bool Offen => Status is not (Foerderstatus.Ausgezahlt or Foerderstatus.Abgelehnt);

    /// <summary>Zulässige nächste Schritte; Abgelehnt ist bis zur Bewilligung möglich.</summary>
    /// <param name="status">Aktueller Stand.</param>
    /// <returns>Nächste Stände.</returns>
    public static IReadOnlyList<Foerderstatus> Folgestatus(Foerderstatus status) => status switch
    {
        Foerderstatus.Vorbereitung => [Foerderstatus.KbmAbgestimmt, Foerderstatus.Abgelehnt],
        Foerderstatus.KbmAbgestimmt => [Foerderstatus.GemeinderatBeschlossen, Foerderstatus.Abgelehnt],
        Foerderstatus.GemeinderatBeschlossen => [Foerderstatus.Beantragt, Foerderstatus.Abgelehnt],
        Foerderstatus.Beantragt => [Foerderstatus.Bewilligt, Foerderstatus.Abgelehnt],
        Foerderstatus.Bewilligt => [Foerderstatus.NachweisEingereicht],
        Foerderstatus.NachweisEingereicht => [Foerderstatus.Ausgezahlt],
        Foerderstatus.Abgelehnt => [Foerderstatus.Vorbereitung],
        _ => [],
    };

    /// <summary>Checkliste des Verwendungsnachweises mit Stand.</summary>
    /// <param name="altfahrzeugAusserDienst">Ob das Altfahrzeug außer Dienst gestellt ist; null, wenn keins ersetzt wird.</param>
    /// <returns>Punkte mit erfüllt ja/nein.</returns>
    public IReadOnlyList<(string Punkt, bool Erfuellt)> Checkliste(bool? altfahrzeugAusserDienst)
    {
        var liste = new List<(string, bool)>
        {
            ("Rechnung", RechnungLiegtVor),
            ("Richtigkeitsbescheinigung der Gemeinde", RichtigkeitsbescheinigungLiegtVor),
            ("Feuerwehrtechnischer Abnahmebericht", AbnahmeberichtLiegtVor),
            ("Erklärung „Beladung nach DIN vorhanden und verlastet“", BeladungserklaerungLiegtVor),
        };
        if (altfahrzeugAusserDienst is { } ausserDienst)
        {
            liste.Add(("Altfahrzeug außer Dienst gestellt", ausserDienst));
        }

        return liste;
    }

    /// <summary>Was für den nächsten Schritt noch fehlt.</summary>
    /// <param name="ziel">Gewünschter Stand.</param>
    /// <param name="altfahrzeugAusserDienst">Ob das Altfahrzeug außer Dienst ist; null ohne Altfahrzeug.</param>
    /// <returns>Fehlende Angaben; leer, wenn der Schritt geht.</returns>
    public IReadOnlyList<string> Fehlt(Foerderstatus ziel, bool? altfahrzeugAusserDienst)
    {
        var fehlt = new List<string>();
        switch (ziel)
        {
            case Foerderstatus.KbmAbgestimmt when KbmAbgestimmtAm is null:
                fehlt.Add("Datum des Vorgesprächs mit dem KBM");
                break;
            case Foerderstatus.GemeinderatBeschlossen when GemeinderatsbeschlussAm is null:
                fehlt.Add("Datum des Gemeinderatsbeschlusses");
                break;
            case Foerderstatus.Beantragt:
                if (BeantragtAm is null)
                {
                    fehlt.Add("Antragsdatum");
                }

                if (FoerdergegenstandId is null)
                {
                    fehlt.Add("Fördergegenstand");
                }

                break;
            case Foerderstatus.Bewilligt:
                if (BescheidAm is null)
                {
                    fehlt.Add("Datum des Zuwendungsbescheids");
                }

                if (Zuwendung is null)
                {
                    fehlt.Add("bewilligte Zuwendung");
                }

                break;
            case Foerderstatus.NachweisEingereicht:
                fehlt.AddRange(Checkliste(altfahrzeugAusserDienst).Where(p => !p.Erfuellt).Select(p => p.Punkt));
                if (NachweisEingereichtAm is null)
                {
                    fehlt.Add("Datum der Einreichung");
                }

                break;
            case Foerderstatus.Ausgezahlt when AusgezahltAm is null:
                fehlt.Add("Datum der Auszahlung");
                break;
        }

        return fehlt;
    }
}
