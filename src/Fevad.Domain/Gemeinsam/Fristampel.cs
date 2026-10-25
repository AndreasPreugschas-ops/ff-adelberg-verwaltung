namespace Fevad.Domain.Gemeinsam;

/// <summary>Ampelbewertung einer Frist für Listen und die Startseite.</summary>
public enum Fristampel
{
    /// <summary>Frist gültig und außerhalb der Warnfrist.</summary>
    Gruen,
    /// <summary>Frist läuft innerhalb der Warnfrist ab.</summary>
    Gelb,
    /// <summary>Frist ist abgelaufen.</summary>
    Rot,
}

/// <summary>Hilfsfunktionen zur Bewertung von Fristen.</summary>
public static class Frist
{
    /// <summary>Bewertet ein Ablaufdatum: rot ab Ablauf, gelb innerhalb der Warnfrist.</summary>
    /// <param name="gueltigBis">Letzter gültiger Tag; leer bedeutet unbefristet und ergibt immer Grün.</param>
    /// <param name="stichtag">Tag, an dem bewertet wird (Ortszeit).</param>
    /// <param name="warnungTage">So viele Tage vor Ablauf wird die Frist gelb.</param>
    /// <returns>Rot, wenn der Stichtag nach dem Ablauf liegt, Gelb innerhalb der Warnfrist, sonst Grün.</returns>
    public static Fristampel Bewerten(DateOnly? gueltigBis, DateOnly stichtag, int warnungTage)
    {
        if (gueltigBis is null)
        {
            return Fristampel.Gruen;
        }

        if (gueltigBis.Value < stichtag)
        {
            return Fristampel.Rot;
        }

        return gueltigBis.Value <= stichtag.AddDays(warnungTage) ? Fristampel.Gelb : Fristampel.Gruen;
    }
}
