using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class AenderungsantragTests
{
    private static readonly DateOnly Heute = new(2026, 10, 25);

    private static Mitglied Mitglied() => new()
    {
        Personalnummer = "1", Vorname = "Max", Nachname = "Muster", Strasse = "Alte Gasse 1", Plz = "73099", Ort = "Adelberg",
        Arbeitgeber = "Firma A", Arbeitsort = "Göppingen", Email = "max@beispiel.de",
    };

    [Fact]
    public void Nur_geaenderte_Felder_kommen_in_den_Antrag()
    {
        var antrag = Aenderungsantrag.Erstellen(Mitglied(), new Dictionary<AenderungFeld, string?>
        {
            [AenderungFeld.Strasse] = " Alte  Gasse 1 ", [AenderungFeld.Ort] = "Schorndorf", [AenderungFeld.Email] = "", [AenderungFeld.TelefonMobil] = null,
        }, "  ");

        Assert.NotNull(antrag);
        Assert.Equal([(AenderungFeld.Ort, "Adelberg", "Schorndorf"), (AenderungFeld.Email, "max@beispiel.de", null)],
            antrag.Felder.Select(f => (f.Feld, f.Alt, f.Neu)));
        Assert.Null(antrag.Bemerkung);
        Assert.False(antrag.BetrifftArbeitgeber);
    }

    [Fact]
    public void Ohne_Aenderung_kein_Antrag() =>
        Assert.Null(Aenderungsantrag.Erstellen(Mitglied(), new Dictionary<AenderungFeld, string?> { [AenderungFeld.Plz] = "73099" }, null));

    [Fact]
    public void Uebernahme_schreibt_Werte_und_liefert_abgeloeste_Anschriften()
    {
        var m = Mitglied();
        var antrag = Aenderungsantrag.Erstellen(m, new Dictionary<AenderungFeld, string?>
        {
            [AenderungFeld.Strasse] = "Neue Straße 5", [AenderungFeld.Arbeitgeber] = "Firma B", [AenderungFeld.NotfallkontaktName] = "Anna",
        }, null)!;
        Assert.True(antrag.BetrifftArbeitgeber);

        var verlauf = antrag.Uebernehmen(m, Heute, DateTimeOffset.UnixEpoch, "kdt");

        Assert.Equal(("Neue Straße 5", "Firma B", "Anna"), (m.Strasse, m.Arbeitgeber, m.NotfallkontaktName));
        Assert.Equal([(AnschriftArt.Privat, "Alte Gasse 1"), (AnschriftArt.Beruflich, null)], verlauf.Select(v => (v.Art, v.Strasse)));
        Assert.Equal("Firma A", verlauf[1].Arbeitgeber);
        Assert.Equal((AenderungsantragStatus.Uebernommen, "kdt"), (antrag.Status, antrag.EntschiedenVon));
        Assert.Throws<InvalidOperationException>(() => antrag.Ablehnen("zu spät", DateTimeOffset.UnixEpoch, "kdt"));
    }

    [Fact]
    public void Nur_Telefon_geaendert_ergibt_keinen_Anschriftenverlauf()
    {
        var m = Mitglied();
        var antrag = Aenderungsantrag.Erstellen(m, new Dictionary<AenderungFeld, string?> { [AenderungFeld.TelefonMobil] = "+49170" }, null)!;
        Assert.Empty(antrag.Uebernehmen(m, Heute, DateTimeOffset.UnixEpoch, null));
        Assert.Equal("+49170", m.TelefonMobil);
    }
}
