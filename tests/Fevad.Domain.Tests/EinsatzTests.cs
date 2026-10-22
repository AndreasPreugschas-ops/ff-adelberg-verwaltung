using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class EinsatzTests
{
    private static readonly DateTimeOffset Alarm = new(2026, 10, 22, 17, 45, 0, TimeSpan.Zero);

    private static Einsatz Vollstaendig() => new()
    {
        Einsatznummer = "2026-017",
        Alarmiert = Alarm,
        Einsatzende = Alarm.AddHours(2),
        StatistikKategorieId = Guid.CreateVersion7(),
        Teilnahmen = [new EinsatzTeilnahme { MitgliedId = Guid.CreateVersion7() }],
    };

    [Fact]
    public void Vollstaendiger_Bericht_kann_freigegeben_werden()
    {
        var einsatz = Vollstaendig();
        var benutzer = Guid.CreateVersion7();

        einsatz.Freigeben(benutzer, Alarm.AddDays(1));

        Assert.Equal(EinsatzStatus.Freigegeben, einsatz.Status);
        Assert.Equal(benutzer, einsatz.FreigegebenVon);
    }

    [Fact]
    public void Ohne_Statistik_Kategorie_keine_Freigabe()
    {
        var einsatz = Vollstaendig();
        einsatz.StatistikKategorieId = null;

        Assert.Contains("Statistik-Kategorie fehlt.", einsatz.FreigabeHindernisse());
        Assert.Throws<InvalidOperationException>(() => einsatz.Freigeben(Guid.CreateVersion7(), Alarm));
        Assert.Equal(EinsatzStatus.Entwurf, einsatz.Status);
    }

    [Fact]
    public void Einsatzende_vor_Alarm_ist_ein_Hindernis()
    {
        var einsatz = Vollstaendig();
        einsatz.Einsatzende = Alarm.AddMinutes(-5);

        Assert.Contains("Einsatzende liegt vor der Alarmierung.", einsatz.FreigabeHindernisse());
    }

    [Fact]
    public void Freigabe_zuruecknehmen_oeffnet_den_Bericht_wieder()
    {
        var einsatz = Vollstaendig();
        einsatz.Freigeben(Guid.CreateVersion7(), Alarm.AddDays(1));

        einsatz.FreigabeZuruecknehmen();

        Assert.Equal(EinsatzStatus.InBearbeitung, einsatz.Status);
        Assert.Null(einsatz.FreigegebenAm);
        Assert.Null(einsatz.FreigegebenVon);
        Assert.Throws<InvalidOperationException>(einsatz.FreigabeZuruecknehmen);
    }

    [Fact]
    public void Erstes_Speichern_nimmt_Entwurf_in_Bearbeitung()
    {
        var einsatz = Vollstaendig();
        einsatz.InBearbeitungNehmen();
        Assert.Equal(EinsatzStatus.InBearbeitung, einsatz.Status);

        einsatz.Freigeben(Guid.CreateVersion7(), Alarm);
        einsatz.InBearbeitungNehmen();
        Assert.Equal(EinsatzStatus.Freigegeben, einsatz.Status);
    }

    [Fact]
    public void Alarmrohdaten_werden_nach_sechs_Monaten_zur_Loeschung_markiert()
    {
        var import = AlarmImport.Empfangen(AlarmQuelle.Fe2Webhook, "T-1", "{}", Alarm, 6);
        Assert.Equal(new DateOnly(2027, 4, 22), import.LoeschenAb);
    }

    [Fact]
    public void Anwesenheit_zaehlt_ganze_Dienstdauer_wenn_nichts_angegeben()
    {
        var dienst = new Dienst { Titel = "Übung", Beginn = Alarm, Ende = Alarm.AddHours(2) };
        var anwesend = new Anwesenheit { Status = AnwesenheitStatus.Anwesend };
        var spaeter = new Anwesenheit { Status = AnwesenheitStatus.Anwesend, Von = Alarm.AddMinutes(30) };
        var entschuldigt = new Anwesenheit { Status = AnwesenheitStatus.Entschuldigt };

        Assert.Equal(TimeSpan.FromHours(2), anwesend.Stunden(dienst));
        Assert.Equal(TimeSpan.FromMinutes(90), spaeter.Stunden(dienst));
        Assert.Equal(TimeSpan.Zero, entschuldigt.Stunden(dienst));
    }
}
