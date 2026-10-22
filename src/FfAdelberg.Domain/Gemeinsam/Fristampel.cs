namespace FfAdelberg.Domain.Gemeinsam;

public enum Fristampel
{
    Gruen,
    Gelb,
    Rot,
}

public static class Frist
{
    /// <summary>Bewertet ein Ablaufdatum: rot ab Ablauf, gelb innerhalb der Warnfrist.</summary>
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
