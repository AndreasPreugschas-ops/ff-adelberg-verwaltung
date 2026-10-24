using Fevad.Domain.Ausbildung;

namespace Fevad.Domain.Tests;

public class LehrgangsteilnahmeTests
{
    private static readonly DateOnly Heute = new(2026, 10, 24);

    [Fact]
    public void Stand_folgt_Anmeldung_Zuweisung_Zeitraum_und_Abschluss()
    {
        var t = new Lehrgangsteilnahme { AngemeldetAm = Heute.AddDays(-30) };
        Assert.Equal(LehrgangStand.Angemeldet, t.Stand(Heute));

        t.ZugewiesenAm = Heute.AddDays(-5);
        t.Von = Heute.AddDays(10);
        t.Bis = Heute.AddDays(14);
        Assert.Equal(LehrgangStand.Zugewiesen, t.Stand(Heute));
        Assert.Equal(LehrgangStand.Laeuft, t.Stand(Heute.AddDays(12)));
        Assert.Equal(LehrgangStand.ErgebnisFehlt, t.Stand(Heute.AddDays(15)));

        // Abgeschlossen ohne „bestanden“ heißt nicht bestanden.
        t.Abgeschlossen = true;
        Assert.Equal(LehrgangStand.NichtBestanden, t.Stand(Heute.AddDays(20)));
        t.Bestanden = true;
        Assert.Equal(LehrgangStand.Bestanden, t.Stand(Heute.AddDays(20)));

        // Bestanden allein ist noch kein Ergebnis.
        t.Abgeschlossen = false;
        Assert.Equal(LehrgangStand.ErgebnisFehlt, t.Stand(Heute.AddDays(20)));
    }

    [Fact]
    public void Unplausible_Daten_werden_gemeldet()
    {
        Assert.NotEmpty(Lehrgangsteilnahme.Fehler(null, null, null, null, false));
        Assert.NotEmpty(Lehrgangsteilnahme.Fehler(Heute, Heute.AddDays(-1), null, null, false));
        Assert.NotEmpty(Lehrgangsteilnahme.Fehler(Heute, null, Heute.AddDays(5), Heute.AddDays(4), false));
        Assert.NotEmpty(Lehrgangsteilnahme.Fehler(Heute, null, null, null, true));
        Assert.Empty(Lehrgangsteilnahme.Fehler(Heute, Heute, Heute.AddDays(5), Heute.AddDays(9), true));
    }
}
