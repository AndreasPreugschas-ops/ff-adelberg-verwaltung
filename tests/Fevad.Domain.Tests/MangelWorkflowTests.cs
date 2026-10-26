using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

public class MangelWorkflowTests
{
    private static Geraet Neu(string nummer, Guid? fahrzeugId, string? lagerort) =>
        new() { Inventarnummer = nummer, Bezeichnung = "Motorsäge", FahrzeugId = fahrzeugId, Lagerort = lagerort };

    [Fact]
    public void Ersatz_kommt_ins_Fach_und_beim_Wiederverladen_tauscht_alles_zurueck()
    {
        var hlf = Guid.NewGuid();
        var defekt = Neu("MS-1", hlf, "G3");
        var ersatz = Neu("MS-9", null, "Magazin");
        var mangel = new Mangel { GeraetId = defekt.Id, Beschreibung = "Kettenbremse", Schwere = MangelSchwere.AusserBetrieb };

        mangel.ErsatzEinsetzen(defekt, ersatz, "Werkstatt");

        Assert.True(mangel.ErsatzEingesetzt);
        Assert.Equal((hlf, "G3"), (ersatz.FahrzeugId!.Value, ersatz.Lagerort));
        Assert.Equal(((Guid?)null, "Werkstatt"), (defekt.FahrzeugId, defekt.Lagerort));
        Assert.Throws<InvalidOperationException>(() => mangel.ErsatzEinsetzen(defekt, Neu("MS-8", null, null), "Werkstatt"));

        mangel.WiederVerladen(defekt, ersatz, new DateOnly(2026, 10, 26));

        Assert.False(mangel.ErsatzEingesetzt);
        Assert.Equal((hlf, "G3"), (defekt.FahrzeugId!.Value, defekt.Lagerort));
        Assert.Equal(((Guid?)null, "Magazin"), (ersatz.FahrzeugId, ersatz.Lagerort));
        Assert.Equal(new DateOnly(2026, 10, 26), mangel.WiederVerladenAm);
    }

    [Fact]
    public void Ersatz_nur_fuer_das_Geraet_des_Mangels()
    {
        var defekt = Neu("MS-1", Guid.NewGuid(), "G3");
        var mangel = new Mangel { GeraetId = Guid.NewGuid(), Beschreibung = "x" };

        Assert.Throws<InvalidOperationException>(() => mangel.ErsatzEinsetzen(defekt, Neu("MS-9", null, null), "Werkstatt"));
        Assert.Throws<InvalidOperationException>(() => mangel.WiederVerladen(defekt, null, new DateOnly(2026, 10, 26)));
    }

    [Theory]
    [InlineData(true, MangelSchwere.AusserBetrieb, MangelStatus.Offen, true)]
    [InlineData(true, MangelSchwere.Einschraenkung, MangelStatus.Offen, false)]
    [InlineData(true, MangelSchwere.AusserBetrieb, MangelStatus.Behoben, false)]
    [InlineData(false, MangelSchwere.AusserBetrieb, MangelStatus.Offen, false)]
    public void Einsatzrelevant_ist_nur_ein_offener_Fahrzeugausfall(bool fahrzeug, MangelSchwere schwere, MangelStatus status, bool erwartet)
    {
        var mangel = new Mangel
        {
            FahrzeugId = fahrzeug ? Guid.NewGuid() : null,
            GeraetId = fahrzeug ? null : Guid.NewGuid(),
            Beschreibung = "x",
            Schwere = schwere,
            Status = status,
        };

        Assert.Equal(erwartet, mangel.Einsatzrelevant);
    }
}
