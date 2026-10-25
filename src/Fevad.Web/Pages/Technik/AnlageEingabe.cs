using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;

namespace Fevad.Web.Pages.Technik;

/// <summary>Formularteil „Anlagenbuchhaltung“ für Geräte und Fahrzeuge (V60); Anzeige über <c>_AnlageFelder</c>.</summary>
public sealed class AnlageEingabe
{
    /// <summary>Nummer der Anlagenbuchhaltung der Gemeinde.</summary>
    [StringLength(30), Display(Name = "Anlagennummer der Gemeinde")]
    public string? Anlagennummer { get; set; }

    /// <summary>Nutzungsdauer laut Anlagenbuchhaltung.</summary>
    [Range(1, 100, ErrorMessage = "Nutzungsdauer ungültig."), Display(Name = "Nutzungsdauer (Jahre, laut Kämmerei)")]
    public int? NutzungsdauerJahre { get; set; }

    /// <summary>Eigentümer.</summary>
    [Display(Name = "Eigentümer")]
    public Eigentuemer Eigentuemer { get; set; }

    /// <summary>Erhaltene Fördermittel.</summary>
    [Range(0, 9999999), Display(Name = "Fördermittel (€)")]
    public decimal? Foerdermittel { get; set; }

    /// <summary>Fördergeber.</summary>
    [StringLength(100), Display(Name = "Fördergeber")]
    public string? Foerdergeber { get; set; }

    /// <summary>Ende der Zweckbindung.</summary>
    [Display(Name = "Zweckbindung bis")]
    public DateOnly? ZweckbindungBis { get; set; }

    /// <summary>Übernimmt die gespeicherten Werte ins Formular.</summary>
    public static AnlageEingabe Aus(IAnlagegut gut) => new()
    {
        Anlagennummer = gut.Anlagennummer,
        NutzungsdauerJahre = gut.NutzungsdauerJahre,
        Eigentuemer = gut.Eigentuemer,
        Foerdermittel = gut.Foerdermittel,
        Foerdergeber = gut.Foerdergeber,
        ZweckbindungBis = gut.ZweckbindungBis,
    };

    /// <summary>Schreibt die Formularwerte zurück; leere Texte werden zu null.</summary>
    public void Uebernehmen(IAnlagegut gut)
    {
        gut.Anlagennummer = string.IsNullOrWhiteSpace(Anlagennummer) ? null : Anlagennummer.Trim();
        gut.NutzungsdauerJahre = NutzungsdauerJahre;
        gut.Eigentuemer = Eigentuemer;
        gut.Foerdermittel = Foerdermittel;
        gut.Foerdergeber = string.IsNullOrWhiteSpace(Foerdergeber) ? null : Foerdergeber.Trim();
        gut.ZweckbindungBis = ZweckbindungBis;
    }
}
