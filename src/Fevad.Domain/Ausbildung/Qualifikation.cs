using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

public enum QualifikationArt
{
    Lehrgang,
    Fahrerlaubnis,
    Sonstiges,
}

public enum AusbildungsEbene
{
    Standort,
    Kreis,
    Land,
}

public class Qualifikation : Katalogeintrag
{
    public QualifikationArt Art { get; set; }
    public AusbildungsEbene? Ebene { get; set; }

    /// <summary>Leer bedeutet unbefristet.</summary>
    public int? GueltigkeitMonate { get; set; }

    /// <summary>Kürzel für alamos osFunctions (z. B. AGT, MA). Nur gültige Nachweise werden übertragen.</summary>
    public string? Fe2Funktion { get; set; }
}

public class QualifikationNachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid QualifikationId { get; set; }
    public Qualifikation? Qualifikation { get; set; }

    public DateOnly ErworbenAm { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public string? Lehrgangsort { get; set; }
    public decimal? Stunden { get; set; }
    public Guid? DokumentId { get; set; }

    public Fristampel Status(DateOnly stichtag, int warnungTage) =>
        Frist.Bewerten(GueltigBis, stichtag, warnungTage);

    /// <summary>Ablaufdatum aus dem Katalog vorschlagen; ein manuell gesetztes Datum bleibt erhalten.</summary>
    public void GueltigkeitVorbelegen(Qualifikation qualifikation)
    {
        if (GueltigBis is null && qualifikation.GueltigkeitMonate is { } monate)
        {
            GueltigBis = ErworbenAm.AddMonths(monate);
        }
    }
}
