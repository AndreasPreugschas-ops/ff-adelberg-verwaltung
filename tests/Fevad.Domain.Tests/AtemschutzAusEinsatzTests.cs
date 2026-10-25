using Fevad.Domain.Atemschutz;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class AtemschutzAusEinsatzTests
{
    private static readonly DateOnly Heute = new(2026, 10, 25);
    private static readonly Guid Traeger = Guid.NewGuid();
    private static readonly Guid Maschinist = Guid.NewGuid();
    private static readonly Guid Melder = Guid.NewGuid();

    private static Einsatz Brand() => new()
    {
        Einsatznummer = "2026-042",
        Stichwort = "B 3",
        Strasse = "Hauptstraße",
        Hausnummer = "12",
        Ort = "Adelberg",
        // 23:30 Uhr UTC ist in Adelberg schon der nächste Tag.
        Alarmiert = new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero),
        Teilnahmen =
        [
            new() { MitgliedId = Traeger, Atemschutz = true, AtemschutzMinuten = 25, PaGeraet = "PA-07", Maske = "M-12",
                Exposition = ExpositionStatus.Exponiert, Gefahrstoffe = "Brandrauch" },
            new() { MitgliedId = Maschinist, Exposition = ExpositionStatus.Exponiert, Gefahrstoffe = "Brandrauch, Asbest", Hautkontakt = true },
            new() { MitgliedId = Melder, Exposition = ExpositionStatus.NichtExponiert },
        ],
    };

    [Fact]
    public void Freigabe_erzeugt_Nachweis_fuer_Traeger_und_Exposition_fuer_Nichttraeger()
    {
        var einsatz = Brand();
        var abgleich = AtemschutzAusEinsatz.Abgleichen(einsatz, [], alsUebungAbMinuten: 20);

        Assert.Equal(2, abgleich.Neu.Count);
        var pa = Assert.Single(abgleich.Neu, n => n.MitgliedId == Traeger);
        Assert.Equal(AtemschutzNachweisArt.Einsatz, pa.Art);
        Assert.Equal(new DateOnly(2026, 10, 25), pa.Datum);
        Assert.True(pa.GiltAlsEinsatzuebung);
        Assert.Equal(("PA-07", "M-12", "Brandrauch"), (pa.PaGeraet, pa.Maske, pa.Exposition));
        Assert.Equal(einsatz.Id, pa.EinsatzId);
        Assert.Contains("2026-042", pa.Taetigkeit);
        Assert.False(pa.Bestaetigt);

        var expo = Assert.Single(abgleich.Neu, n => n.MitgliedId == Maschinist);
        Assert.Equal(AtemschutzNachweisArt.Exposition, expo.Art);
        Assert.Equal("Brandrauch, Asbest", expo.Exposition);
        Assert.True(expo.Expositionsrelevant);
        Assert.DoesNotContain(abgleich.Neu, n => n.MitgliedId == Melder);
    }

    [Fact]
    public void Kurzer_Einsatz_zaehlt_nicht_als_Einsatzuebung()
    {
        var pa = Assert.Single(AtemschutzAusEinsatz.Abgleichen(Brand(), [], alsUebungAbMinuten: 30).Neu, n => n.MitgliedId == Traeger);
        Assert.False(pa.GiltAlsEinsatzuebung);
        Assert.False(pa.Erfuellt(AtemschutzNachweisArt.Einsatzuebung));
    }

    [Fact]
    public void Erneute_Freigabe_gleicht_ab_und_setzt_Bestaetigungen_bei_Aenderung_zurueck()
    {
        var einsatz = Brand();
        var vorhanden = AtemschutzAusEinsatz.Abgleichen(einsatz, [], 0).Neu.ToList();
        foreach (var n in vorhanden)
        {
            n.Bestaetigen(Guid.NewGuid(), alsKommando: true, alsWart: false, DateTimeOffset.UtcNow);
            n.Bestaetigen(Guid.NewGuid(), alsKommando: false, alsWart: true, DateTimeOffset.UtcNow);
        }

        Assert.Empty(AtemschutzAusEinsatz.Abgleichen(einsatz, vorhanden, 0) is var gleich ? gleich.Neu.Concat(gleich.Geaendert).Concat(gleich.Entfernt) : []);

        einsatz.Teilnahmen[0].AtemschutzMinuten = 40;
        einsatz.Teilnahmen[1].Exposition = ExpositionStatus.NichtExponiert;
        var abgleich = AtemschutzAusEinsatz.Abgleichen(einsatz, vorhanden, 0);
        Assert.Empty(abgleich.Neu);
        var geaendert = Assert.Single(abgleich.Geaendert);
        Assert.Equal(40, geaendert.Minuten);
        Assert.False(geaendert.Bestaetigt);
        Assert.Equal(Maschinist, Assert.Single(abgleich.Entfernt).MitgliedId);
    }

    [Fact]
    public void Dieselbe_Person_bestaetigt_nie_beide_Felder()
    {
        var nachweis = new AtemschutzNachweis { Art = AtemschutzNachweisArt.Einsatz, Datum = Heute };
        var person = Guid.NewGuid();
        Assert.True(nachweis.Bestaetigen(person, alsKommando: true, alsWart: true, DateTimeOffset.UtcNow));
        Assert.False(nachweis.Bestaetigen(person, alsKommando: true, alsWart: true, DateTimeOffset.UtcNow));
        Assert.False(nachweis.Bestaetigt);
        Assert.True(nachweis.Bestaetigen(Guid.NewGuid(), alsKommando: false, alsWart: true, DateTimeOffset.UtcNow));
        Assert.True(nachweis.Bestaetigt);
    }

    [Fact]
    public void Selbstmeldung_ueber_Hoechstdauer_braucht_Genehmigung_und_sperrt_sofort()
    {
        var kurz = new AtemschutzSperre { Von = Heute, Bis = Heute.AddDays(13), Selbstmeldung = true };
        var lang = new AtemschutzSperre { Von = Heute, Bis = Heute.AddDays(14), Selbstmeldung = true };
        Assert.False(kurz.GenehmigungOffen(14));
        Assert.True(lang.GenehmigungOffen(14));
        Assert.True(lang.Wirksam(Heute));
        Assert.False(lang.Wirksam(Heute.AddDays(15)));

        lang.GenehmigtAm = DateTimeOffset.UtcNow;
        Assert.False(lang.GenehmigungOffen(14));
        lang.AufgehobenAm = DateTimeOffset.UtcNow;
        Assert.False(lang.Wirksam(Heute));
    }

    [Fact]
    public void Abmeldung_macht_den_Status_rot()
    {
        var vorsorge = new AtemschutzVorsorge { UntersuchtAm = Heute.AddYears(-1), Ergebnis = VorsorgeErgebnis.Geeignet, NaechsteFaellig = Heute.AddYears(2) };
        var uebungen = AtemschutzStatus.JaehrlichePflichten.Select(art => new AtemschutzNachweis { Art = art, Datum = Heute.AddMonths(-1) }).ToList();
        Assert.Equal(Fristampel.Gruen, AtemschutzStatus.Berechnen(true, [vorsorge], uebungen, Heute, _ => 12, 30).Ampel);

        var gesperrt = AtemschutzStatus.Berechnen(true, [vorsorge], uebungen, Heute, _ => 12, 30, gesperrt: true);
        Assert.Equal(Fristampel.Rot, gesperrt.Ampel);
        Assert.True(gesperrt.Gesperrt);
    }

    [Fact]
    public void Brand_verlangt_Exposition_und_Gefahrstoff()
    {
        var einsatz = Brand();
        einsatz.StatistikKategorieId = Guid.NewGuid();
        einsatz.Einsatzende = einsatz.Alarmiert.AddHours(1);
        Assert.Empty(einsatz.FreigabeHindernisse(expositionPflicht: true));

        einsatz.Teilnahmen[2].Exposition = null;
        einsatz.Teilnahmen[0].Gefahrstoffe = null;
        Assert.DoesNotContain(einsatz.FreigabeHindernisse(), h => h.Contains("Exposition"));
        var hindernisse = einsatz.FreigabeHindernisse(expositionPflicht: true);
        Assert.Contains("Bei einem Teilnehmer fehlt die Exposition.", hindernisse);
        Assert.Contains("Bei einem exponierten Teilnehmer fehlt der Gefahrstoff.", hindernisse);
    }
}
