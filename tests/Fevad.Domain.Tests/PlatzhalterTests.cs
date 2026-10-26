using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

/// <summary>Platzhalter der Briefvorlagen (P5: V56).</summary>
public class PlatzhalterTests
{
    [Fact]
    public void Ersetzen_laesst_Unbekanntes_stehen()
    {
        var werte = new Dictionary<string, string?> { ["Vorname"] = "Jo", ["Nachname"] = null };
        Assert.Equal("Hallo Jo , {Unbekannt}", Platzhalter.Ersetzen("Hallo {Vorname} {Nachname}, {Unbekannt}", werte));
    }

    [Fact]
    public void Unbekannte_Platzhalter_werden_gemeldet()
    {
        Assert.Equal(["Vornahme"], Platzhalter.Unbekannte("{Briefanrede} {Vornahme} {Vornahme} {PlzOrt}"));
        Assert.Empty(Platzhalter.Unbekannte(null));
    }

    [Fact]
    public void Briefanrede_nach_Geschlecht()
    {
        Assert.Equal("Sehr geehrte Frau Dr. Muster,", Platzhalter.Briefanrede(Geschlecht.Weiblich, "Dr.", "Eva", "Muster"));
        Assert.Equal("Sehr geehrter Herr Muster,", Platzhalter.Briefanrede(Geschlecht.Maennlich, null, "Max", "Muster"));
        Assert.Equal("Guten Tag Kim Muster,", Platzhalter.Briefanrede(null, null, "Kim", "Muster"));
    }
}
