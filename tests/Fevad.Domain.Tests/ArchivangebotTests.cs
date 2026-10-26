using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class ArchivangebotTests
{
    private static readonly DateOnly Heute = new(2026, 10, 26);

    [Fact]
    public void Ohne_Anbietung_wird_nicht_geloescht()
        => Assert.False(new Archivangebot { Bezeichnung = "Akte" }.DarfLoeschen(Heute, 12));

    [Fact]
    public void Ohne_Antwort_des_Archivs_erst_nach_der_Entscheidungsfrist()
    {
        Assert.False(new Archivangebot { Bezeichnung = "Akte", AngebotenAm = Heute.AddMonths(-11) }.DarfLoeschen(Heute, 12));
        Assert.True(new Archivangebot { Bezeichnung = "Akte", AngebotenAm = Heute.AddMonths(-12) }.DarfLoeschen(Heute, 12));
    }

    [Fact]
    public void Kassation_erlaubt_die_Loeschung_sofort()
        => Assert.True(new Archivangebot { Bezeichnung = "Akte", Entscheidung = Archiventscheidung.Kassieren }.DarfLoeschen(Heute, 12));

    [Fact]
    public void Uebernahme_erst_nach_der_Uebergabe()
    {
        var angebot = new Archivangebot { Bezeichnung = "Akte", Entscheidung = Archiventscheidung.Uebernehmen, AngebotenAm = Heute.AddYears(-3) };
        Assert.False(angebot.DarfLoeschen(Heute, 12));
        angebot.UebergebenAm = Heute;
        Assert.True(angebot.DarfLoeschen(Heute, 12));
    }
}
