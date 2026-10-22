using Fevad.Web.Infrastruktur;
using Microsoft.Extensions.Configuration;

namespace Fevad.Integration.Tests;

public class StartpruefungTests
{
    private static Startpruefung.Ergebnis Pruefen(params (string Schluessel, string? Wert)[] werte) =>
        Startpruefung.Pruefen(new ConfigurationBuilder()
            .AddInMemoryCollection(werte.Select(w => new KeyValuePair<string, string?>(w.Schluessel, w.Wert)))
            .Build());

    private static readonly (string, string?)[] Vollstaendig =
    [
        ("ConnectionStrings:Datenbank", "Host=postgres;Database=fevad"),
        ("Anmeldung:OeffentlicheAdresse", "https://fevad.example.de"),
        ("Mail:SmtpServer", "smtp.example.de"),
        ("Mail:Absender", "fevad@example.de"),
    ];

    [Fact]
    public void Vollstaendige_Konfiguration_ohne_Fehler_und_Warnungen()
    {
        var ergebnis = Pruefen(Vollstaendig);
        Assert.Empty(ergebnis.Fehler);
        Assert.Empty(ergebnis.Warnungen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://fevad.example.de")]
    [InlineData("fevad.example.de")]
    public void Oeffentliche_Adresse_muss_https_sein(string? adresse)
    {
        var ergebnis = Pruefen([.. Vollstaendig.Where(w => w.Item1 != "Anmeldung:OeffentlicheAdresse"), ("Anmeldung:OeffentlicheAdresse", adresse)]);
        Assert.Contains(ergebnis.Fehler, f => f.Contains("Anmeldung:OeffentlicheAdresse"));
    }

    [Fact]
    public void Abgeschaltete_Zwei_Faktor_Pflicht_und_Startpasswort_werden_gemeldet()
    {
        var ergebnis = Pruefen([.. Vollstaendig, ("Anmeldung:ZweiFaktorPflichtAus", "true"), ("Seed:Admin:Passwort", "geheim"), ("Fe2:WebhookToken", "kurz")]);
        Assert.Empty(ergebnis.Fehler);
        Assert.Contains(ergebnis.Warnungen, w => w.Contains("ZweiFaktorPflichtAus"));
        Assert.Contains(ergebnis.Warnungen, w => w.Contains("Seed:Admin:Passwort"));
        Assert.Contains(ergebnis.Warnungen, w => w.Contains("Fe2:WebhookToken"));
    }

    [Fact]
    public void Fehlender_Mailserver_ist_nur_eine_Warnung()
    {
        var ergebnis = Pruefen(("ConnectionStrings:Datenbank", "Host=x"), ("Anmeldung:OeffentlicheAdresse", "https://fevad.example.de"));
        Assert.Empty(ergebnis.Fehler);
        Assert.Contains(ergebnis.Warnungen, w => w.Contains("Mail:SmtpServer"));
    }
}
