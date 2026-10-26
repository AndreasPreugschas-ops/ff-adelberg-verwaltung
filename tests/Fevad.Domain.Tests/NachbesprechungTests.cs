using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class NachbesprechungTests
{
    private static readonly DateTimeOffset Alarm = new(2026, 10, 22, 17, 45, 0, TimeSpan.Zero);
    private static readonly Guid Konto = Guid.CreateVersion7();

    private static Einsatz Vollstaendig() => new()
    {
        Einsatznummer = "2026-018",
        Alarmiert = Alarm,
        Einsatzende = Alarm.AddHours(2),
        StatistikKategorieId = Guid.CreateVersion7(),
        Teilnahmen = [new EinsatzTeilnahme { MitgliedId = Guid.CreateVersion7() }],
        Status = EinsatzStatus.InBearbeitung,
    };

    [Fact]
    public void Vorlegen_und_Zurueckgeben_mit_Kommentar()
    {
        var einsatz = Vollstaendig();
        einsatz.Vorlegen(Konto, Alarm.AddDays(1));
        Assert.Equal(EinsatzStatus.Vorgelegt, einsatz.Status);
        Assert.Equal(Konto, einsatz.VorgelegtVon);

        einsatz.Zurueckgeben("  Atemschutzzeiten fehlen ");
        Assert.Equal(EinsatzStatus.InBearbeitung, einsatz.Status);
        Assert.Equal("Atemschutzzeiten fehlen", einsatz.Rueckgabe);
        Assert.Null(einsatz.VorgelegtAm);

        einsatz.Vorlegen(Konto, Alarm.AddDays(2));
        Assert.Null(einsatz.Rueckgabe);
        einsatz.Freigeben(Konto, Alarm.AddDays(3));
        Assert.Equal(EinsatzStatus.Freigegeben, einsatz.Status);
    }

    [Fact]
    public void Unvollstaendiger_Bericht_laesst_sich_nicht_vorlegen()
    {
        var einsatz = Vollstaendig();
        einsatz.Einsatzende = null;
        Assert.Throws<InvalidOperationException>(() => einsatz.Vorlegen(Konto, Alarm));
        Assert.Throws<InvalidOperationException>(() => einsatz.Zurueckgeben("nicht vorgelegt"));
    }

    [Fact]
    public void Zurueckgeben_braucht_Kommentar()
    {
        var einsatz = Vollstaendig();
        einsatz.Vorlegen(Konto, Alarm);
        Assert.ThrowsAny<ArgumentException>(() => einsatz.Zurueckgeben(" "));
    }

    [Fact]
    public void Massnahme_durchlaeuft_Vorschlag_Freigabe_Erledigung()
    {
        var m = new Massnahme { Titel = "Übung Wasserentnahme", Art = MassnahmeArt.Uebungsbedarf, ErledigenBis = new DateOnly(2026, 11, 30) };
        Assert.True(m.Offen);
        Assert.Null(m.Ampel(new DateOnly(2026, 12, 5), 14));
        Assert.Throws<InvalidOperationException>(() => m.Erledigen(new DateOnly(2026, 11, 1)));

        m.Freigeben(Konto, Alarm);
        Assert.Equal(Fristampel.Gruen, m.Ampel(new DateOnly(2026, 11, 1), 14));
        Assert.Equal(Fristampel.Gelb, m.Ampel(new DateOnly(2026, 11, 16), 14));
        Assert.Equal(Fristampel.Rot, m.Ampel(new DateOnly(2026, 12, 1), 14));

        m.Erledigen(new DateOnly(2026, 11, 20));
        Assert.False(m.Offen);
        Assert.Equal(new DateOnly(2026, 11, 20), m.ErledigtAm);
    }

    [Fact]
    public void Verwerfen_nur_bei_Vorschlag()
    {
        var m = new Massnahme { Titel = "Neue Wärmebildkamera", Art = MassnahmeArt.Beschaffung };
        m.Verwerfen(Konto, "im Haushalt 2027");
        Assert.Equal(MassnahmeStatus.Verworfen, m.Status);
        Assert.Equal("im Haushalt 2027", m.Bemerkung);
        Assert.Throws<InvalidOperationException>(() => m.Freigeben(Konto, Alarm));
    }
}
