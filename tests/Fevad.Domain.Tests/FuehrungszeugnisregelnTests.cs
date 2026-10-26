using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class FuehrungszeugnisregelnTests
{
    private static readonly DateOnly Tag = new(2026, 10, 26);

    private static Jugendbetreuer Betreuer(DateOnly? eingesehen, bool? ohneEintrag = true, bool noetig = true) =>
        new() { Vorname = "A", Nachname = "B", Aufgabe = "Betreuer", Von = Tag.AddYears(-6), EingesehenAm = eingesehen, OhneEintrag = ohneEintrag, ZeugnisNoetig = noetig };

    [Theory]
    [InlineData(-100, Zeugnisstatus.Gueltig)]
    [InlineData(-1800, Zeugnisstatus.Bald)]
    [InlineData(-1900, Zeugnisstatus.Faellig)]
    public void Wiedervorlage_nach_fuenf_Jahren(int tage, Zeugnisstatus erwartet) =>
        Assert.Equal(erwartet, Fuehrungszeugnisregeln.Status(Betreuer(Tag.AddDays(tage)), Tag, 5, 60));

    [Fact]
    public void Fehlend_Eintrag_und_nicht_noetig()
    {
        Assert.Equal(Zeugnisstatus.Fehlt, Fuehrungszeugnisregeln.Status(Betreuer(null, null), Tag, 5, 60));
        Assert.Equal(Zeugnisstatus.Eintrag, Fuehrungszeugnisregeln.Status(Betreuer(Tag.AddDays(-5), false), Tag, 5, 60));
        Assert.Equal(Zeugnisstatus.NichtNoetig, Fuehrungszeugnisregeln.Status(Betreuer(null, null, noetig: false), Tag, 5, 60));
        Assert.False(Fuehrungszeugnisregeln.Einsetzbar(Zeugnisstatus.Eintrag));
        Assert.True(Fuehrungszeugnisregeln.Einsetzbar(Zeugnisstatus.Bald));
    }

    [Fact]
    public void Zeugnis_hoechstens_drei_Monate_alt()
    {
        Assert.False(Fuehrungszeugnisregeln.ZuAlt(Tag.AddMonths(-3), Tag, 3));
        Assert.True(Fuehrungszeugnisregeln.ZuAlt(Tag.AddMonths(-3).AddDays(-1), Tag, 3));
    }
}
