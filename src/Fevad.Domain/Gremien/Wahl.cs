using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Gremien;

/// <summary>Verfahren, nach dem ein Wahlergebnis festgestellt wird.</summary>
public enum Wahlverfahren
{
    /// <summary>
    /// Kommandant, Stellvertreter: gewählt ist, wer mehr als die Hälfte der Stimmen der anwesenden Wahlberechtigten
    /// erhält; sonst Stichwahl der beiden Ersten mit einfacher Mehrheit (Satzung Adelberg § 15 Abs. 3).
    /// </summary>
    [Display(Name = "absolute Mehrheit")] AbsoluteMehrheit,

    /// <summary>Ausschuss, Kassenprüfer: die Bewerber mit den meisten Stimmen; die übrigen werden Ersatzmitglieder.</summary>
    [Display(Name = "Mehrheitswahl (meiste Stimmen)")] Mehrheitswahl,
}

/// <summary>Stand eines Wahlgangs.</summary>
public enum WahlStatus
{
    /// <summary>Stimmen werden erfasst.</summary>
    [Display(Name = "in Erfassung")] Erfassung,
    /// <summary>Das Ergebnis ist festgestellt.</summary>
    [Display(Name = "Ergebnis festgestellt")] Festgestellt,
    /// <summary>Die Gewählten sind als Funktionen eingetragen.</summary>
    [Display(Name = "in Funktionen übernommen")] Uebernommen,
}

/// <summary>
/// Ein Wahlgang für eine Funktion (z. B. Kommandant, vier Ausschussmitglieder) in einer Sitzung.
/// Das festgestellte Ergebnis wird als <see cref="FunktionZuordnung"/> mit Amtszeit übernommen.
/// </summary>
public class Wahl : Entitaet
{
    /// <summary>Schlüssel der Sitzung.</summary>
    public Guid SitzungId { get; set; }
    /// <summary>Die Sitzung.</summary>
    public Sitzung? Sitzung { get; set; }

    /// <summary>Schlüssel der zu besetzenden Funktion.</summary>
    public Guid FunktionId { get; set; }
    /// <summary>Die zu besetzende Funktion.</summary>
    public Funktion? Funktion { get; set; }

    /// <summary>1 für Kommandant, 2 für die Stichwahl.</summary>
    public int Wahlgang { get; set; } = 1;
    /// <summary>Wahlverfahren.</summary>
    public Wahlverfahren Verfahren { get; set; }
    /// <summary>Geheime Wahl mit Stimmzetteln.</summary>
    public bool Geheim { get; set; } = true;

    /// <summary>Zu besetzende Sitze, z. B. 4 im Ausschuss.</summary>
    public int Sitze { get; set; } = 1;

    /// <summary>Anwesende Wahlberechtigte; Bezug der absoluten Mehrheit.</summary>
    public int? Stimmberechtigte { get; set; }
    /// <summary>Abgegebene Stimmzettel.</summary>
    public int? AbgegebeneStimmzettel { get; set; }
    /// <summary>Ungültige Stimmzettel.</summary>
    public int? UngueltigeStimmzettel { get; set; }

    /// <summary>Gültige Stimmen gesamt; bei Mehrheitswahl mit mehreren Sitzen die Summe der Stimmen für Bewerber.</summary>
    public int? GueltigeStimmen { get; set; }

    /// <summary>Erster Tag der Amtszeit.</summary>
    public DateOnly? Amtsbeginn { get; set; }
    /// <summary>Dauer der Amtszeit in Jahren.</summary>
    public int? AmtszeitJahre { get; set; }

    /// <summary>Kommandant und Stellvertreter: Bestellung durch den Bürgermeister nach Zustimmung des Gemeinderats.</summary>
    public bool ZustimmungGemeinderatErforderlich { get; set; }
    /// <summary>Tag der Zustimmung des Gemeinderats.</summary>
    public DateOnly? ZustimmungGemeinderatAm { get; set; }

    /// <summary>Stand des Wahlgangs.</summary>
    public WahlStatus Status { get; set; }
    /// <summary>Freitext, z. B. Losentscheid.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Bewerber mit Stimmen.</summary>
    public List<WahlBewerber> Bewerber { get; set; } = [];

    /// <summary>Ab dem zweiten Wahlgang mit mehreren Bewerbern genügt die einfache Mehrheit.</summary>
    public bool IstStichwahl => Wahlgang > 1 && Bewerber.Count > 1;

    /// <summary>Letzter Tag der Amtszeit: Amtsbeginn plus Amtszeit, minus einen Tag.</summary>
    public DateOnly? Amtsende => Amtsbeginn is { } beginn && AmtszeitJahre is { } jahre ? beginn.AddYears(jahre).AddDays(-1) : null;

    /// <summary>
    /// Stellt das Ergebnis aus den Stimmen fest und markiert die Gewählten.
    /// Gleichstand an der Grenze oder fehlende absolute Mehrheit lässt Sitze offen (Stichwahl oder Los).
    /// </summary>
    /// <returns>Gewählte, offene Sitze und Hinweise für die Wahlleitung.</returns>
    public WahlErgebnis ErgebnisFeststellen()
    {
        foreach (var b in Bewerber)
        {
            b.Gewaehlt = false;
            b.Platz = null;
        }

        var sortiert = Bewerber.OrderByDescending(b => b.Stimmen).ToList();
        for (var i = 0; i < sortiert.Count; i++)
        {
            // Gleiche Stimmenzahl, gleicher Platz.
            sortiert[i].Platz = i > 0 && sortiert[i].Stimmen == sortiert[i - 1].Stimmen ? sortiert[i - 1].Platz : i + 1;
        }

        var hinweise = new List<string>();
        var kandidaten = sortiert.Where(b => b.Stimmen > 0).ToList();

        if (Verfahren == Wahlverfahren.AbsoluteMehrheit && IstStichwahl)
        {
            // Stichwahl: einfache Mehrheit, bei Gleichstand entscheidet das Los.
            if (Bewerber.Count > 2)
            {
                hinweise.Add("Zur Stichwahl stehen nur die beiden Bewerber mit den meisten Stimmen aus dem ersten Wahlgang.");
            }
            else if (kandidaten.Count > 1 && kandidaten[0].Stimmen == kandidaten[1].Stimmen)
            {
                hinweise.Add("Stimmengleichheit in der Stichwahl: Los entscheidet.");
            }
            else if (kandidaten.Count > 0)
            {
                kandidaten[0].Gewaehlt = true;
            }
        }
        else if (Verfahren == Wahlverfahren.AbsoluteMehrheit && Stimmberechtigte is null)
        {
            // Bezug der absoluten Mehrheit sind die anwesenden Wahlberechtigten; Enthaltungen zählen damit wie Nein.
            hinweise.Add("Für die absolute Mehrheit die Zahl der anwesenden Wahlberechtigten eintragen.");
        }
        else if (Verfahren == Wahlverfahren.AbsoluteMehrheit)
        {
            var basis = Stimmberechtigte!.Value;
            var mehrheit = kandidaten.Where(b => b.Stimmen * 2 > basis).Take(Sitze).ToList();
            mehrheit.ForEach(b => b.Gewaehlt = true);
            if (mehrheit.Count < Sitze)
            {
                hinweise.Add(Bewerber.Count == 1
                    ? $"Der Bewerber hat nicht mehr als die Hälfte der Stimmen der {basis} anwesenden Wahlberechtigten erhalten: zweiter Wahlgang, wieder mit dieser Mehrheit."
                    : $"Niemand hat mehr als die Hälfte der Stimmen der {basis} anwesenden Wahlberechtigten erhalten: Stichwahl zwischen den beiden Ersten.");
            }
        }
        else
        {
            var gewaehlt = 0;
            foreach (var gruppe in kandidaten.GroupBy(b => b.Stimmen))
            {
                var mitGleicherZahl = gruppe.ToList();
                if (gewaehlt + mitGleicherZahl.Count <= Sitze)
                {
                    mitGleicherZahl.ForEach(b => b.Gewaehlt = true);
                    gewaehlt += mitGleicherZahl.Count;
                }
                else
                {
                    if (gewaehlt < Sitze)
                    {
                        hinweise.Add($"Stimmengleichheit um den letzten Sitz ({gruppe.Key} Stimmen): Los entscheidet.");
                    }

                    break;
                }
            }

            if (gewaehlt < Sitze && hinweise.Count == 0)
            {
                hinweise.Add($"Nur {gewaehlt} von {Sitze} Sitzen besetzt.");
            }
        }

        var offen = Sitze - Bewerber.Count(b => b.Gewaehlt);
        return new WahlErgebnis(Bewerber.Where(b => b.Gewaehlt).ToList(), offen, hinweise);
    }

    /// <summary>
    /// Erzeugt die Funktionszuordnungen für die Gewählten, die die Wahl angenommen haben.
    /// Nach einem Losentscheid setzt die Wahlleitung „gewählt“ vorher von Hand.
    /// </summary>
    /// <param name="sitzungstag">Tag der Wahl; wird Wahldatum der Zuordnung.</param>
    /// <returns>Die neuen, noch nicht gespeicherten Funktionszuordnungen.</returns>
    /// <exception cref="InvalidOperationException">Amtsbeginn oder erforderliche Zustimmung des Gemeinderats fehlen.</exception>
    public IReadOnlyList<FunktionZuordnung> FunktionenErzeugen(DateOnly sitzungstag)
    {
        if (Amtsbeginn is null)
        {
            throw new InvalidOperationException("Amtsbeginn fehlt.");
        }

        if (ZustimmungGemeinderatErforderlich && ZustimmungGemeinderatAm is null)
        {
            throw new InvalidOperationException("Die Zustimmung des Gemeinderats fehlt noch.");
        }

        return Bewerber
            .Where(b => b.Gewaehlt && b.AngenommenAm is not null)
            .Select(b => new FunktionZuordnung
            {
                MitgliedId = b.MitgliedId,
                FunktionId = FunktionId,
                Art = FunktionArt.Wahl,
                GueltigAb = Amtsbeginn.Value,
                GueltigBis = Amtsende,
                Wahldatum = sitzungstag,
                Beschlussdatum = ZustimmungGemeinderatAm,
                Bemerkung = $"Gewählt mit {b.Stimmen} Stimmen" + (ZustimmungGemeinderatAm is { } z ? $", Zustimmung Gemeinderat {z:dd.MM.yyyy}" : ""),
            })
            .ToList();
    }
}

/// <summary>Festgestelltes Ergebnis eines Wahlgangs.</summary>
/// <param name="Gewaehlte">Die gewählten Bewerber.</param>
/// <param name="OffeneSitze">Nicht besetzte Sitze, z. B. bis zur Stichwahl.</param>
/// <param name="Hinweise">Hinweise für die Wahlleitung, z. B. Stichwahl oder Los.</param>
public sealed record WahlErgebnis(IReadOnlyList<WahlBewerber> Gewaehlte, int OffeneSitze, IReadOnlyList<string> Hinweise);

/// <summary>Bewerber in einem Wahlgang.</summary>
public class WahlBewerber : Entitaet
{
    /// <summary>Schlüssel der Wahl.</summary>
    public Guid WahlId { get; set; }
    /// <summary>Die Wahl.</summary>
    public Wahl? Wahl { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Erhaltene Stimmen.</summary>
    public int Stimmen { get; set; }
    /// <summary>Platz nach Stimmen; bei Gleichstand gleicher Platz.</summary>
    public int? Platz { get; set; }
    /// <summary>Gewählt; nach einem Losentscheid von Hand gesetzt.</summary>
    public bool Gewaehlt { get; set; }

    /// <summary>Gewählte müssen die Wahl annehmen; erst dann beginnt das Amt.</summary>
    public DateOnly? AngenommenAm { get; set; }

    /// <summary>Nicht gewählte Bewerber der Ausschusswahl rücken als Ersatzmitglieder nach.</summary>
    public bool Ersatzmitglied => !Gewaehlt && Stimmen > 0;
}
