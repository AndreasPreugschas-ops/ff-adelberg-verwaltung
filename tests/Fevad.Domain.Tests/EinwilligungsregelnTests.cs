using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class EinwilligungsregelnTests
{
    private static readonly DateOnly Tag = new(2026, 10, 26);

    private static Einwilligung Erklaerung(bool erteilt, int tageZurueck, EinwilligungDurch durch = EinwilligungDurch.Erziehungsberechtigter,
        bool? jugendlicher = null, DateOnly? widerrufen = null) =>
        new() { Erteilt = erteilt, Am = Tag.AddDays(-tageZurueck), Durch = durch, JugendlicherZugestimmt = jugendlicher, WiderrufenAm = widerrufen };

    [Fact]
    public void Ohne_Erklaerung_fehlt_die_Einwilligung() =>
        Assert.Equal(Einwilligungsstatus.Fehlt, Einwilligungsregeln.Status([], 12, 18, 16));

    [Fact]
    public void Unter_16_genuegt_ein_Elternteil() =>
        Assert.Equal(Einwilligungsstatus.Erteilt, Einwilligungsregeln.Status([Erklaerung(true, 3)], 12, 18, 16));

    [Fact]
    public void Ab_16_braucht_es_auch_die_Zustimmung_des_Jugendlichen()
    {
        Assert.Equal(Einwilligungsstatus.ZustimmungFehlt, Einwilligungsregeln.Status([Erklaerung(true, 3)], 16, 18, 16));
        Assert.Equal(Einwilligungsstatus.Erteilt, Einwilligungsregeln.Status([Erklaerung(true, 3, jugendlicher: true)], 16, 18, 16));
        // Volljährig wirkt die frühere Erklärung der Eltern weiter, bis sie widerrufen wird.
        Assert.Equal(Einwilligungsstatus.Erteilt, Einwilligungsregeln.Status([Erklaerung(true, 900)], 18, 18, 16));
    }

    [Fact]
    public void Die_juengste_Erklaerung_gilt_und_ein_Widerruf_wirkt_sofort()
    {
        var alt = Erklaerung(true, 30, widerrufen: Tag.AddDays(-10));
        var neu = Erklaerung(true, 5);
        Assert.Equal(Einwilligungsstatus.Erteilt, Einwilligungsregeln.Status([alt, neu], 12, 18, 16));
        Assert.Equal(Einwilligungsstatus.Widerrufen, Einwilligungsregeln.Status([alt], 12, 18, 16));
        Assert.Equal(Einwilligungsstatus.Abgelehnt, Einwilligungsregeln.Status([neu, Erklaerung(false, 1, EinwilligungDurch.Mitglied)], 30, 18, 16));
    }

    [Theory]
    [InlineData(true, 30, true)]
    [InlineData(true, 16, false)]
    [InlineData(false, 12, true)]
    public void Minderjaehrige_erteilen_nicht_selbst(bool selbst, int alter, bool darf) =>
        Assert.Equal(darf, Einwilligungsregeln.DarfErteilen(selbst, alter, 18));

    [Fact]
    public void Im_Antrag_stehen_nur_passende_Arten()
    {
        Einwilligungsart[] arten =
        [
            new() { Code = "FOTO-HP", Bezeichnung = "Homepage", Sortierung = 10, Foto = true },
            new() { Code = "WA", Bezeichnung = "WhatsApp", Sortierung = 50, NurJugend = true },
            new() { Code = "MED", Bezeichnung = "Medikamente", Sortierung = 90, NurJugend = true, ImAntrag = false },
            new() { Code = "ALT", Bezeichnung = "Alt", Sortierung = 5, Aktiv = false },
        ];
        Assert.Equal(["FOTO-HP", "WA"], Einwilligungsregeln.FuerAntrag(arten, AntragArt.JugendEintritt).Select(a => a.Code));
        Assert.Equal(["FOTO-HP"], Einwilligungsregeln.FuerAntrag(arten, AntragArt.EinsatzEintritt).Select(a => a.Code));
        Assert.Empty(Einwilligungsregeln.FuerAntrag(arten, AntragArt.Ausscheiden));
    }
}
