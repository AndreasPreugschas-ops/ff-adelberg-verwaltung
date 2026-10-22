using FfAdelberg.Infrastructure.Einsaetze;

namespace FfAdelberg.Integration.Tests;

public class Fe2AlarmTests
{
    [Fact]
    public void Liest_Datenformat_v2()
    {
        var alarm = Fe2Alarm.Lesen("""
            {
              "type": "ALARM",
              "timestamp": 1792691100000,
              "sender": "ILS Göppingen",
              "data": {
                "externalId": "ILS-4711",
                "keyword": "B 2",
                "keyword_description": "Brand Wohngebäude",
                "keyword_category": "Brand",
                "keyword_misc": "Personen vermisst",
                "message": ["Rauch aus Dachstuhl", " ", "Anrufer vor Ort"],
                "location": { "coordinate": [9.5961, 48.7647], "street": "Hauptstraße", "house": "12", "postalCode": "73099", "city": "Adelberg", "building": "Rathaus" },
                "vehicles": [{ "radioName": "Florian Adelberg 43" }, { "radioName": "florian adelberg 43" }, { "name": "Florian Göppingen 1/33" }]
              }
            }
            """);

        Assert.True(alarm.IstAlarm);
        Assert.Equal("ILS-4711", alarm.ExterneId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792691100000), alarm.Zeitpunkt);
        Assert.Equal("B 2", alarm.Stichwort);
        Assert.Equal("Brand Wohngebäude", alarm.StichwortBeschreibung);
        Assert.Equal("Personen vermisst", alarm.StichwortZusatz);
        Assert.Equal(["Rauch aus Dachstuhl", "Anrufer vor Ort"], alarm.Meldung);
        Assert.Equal("Hauptstraße", alarm.Strasse);
        Assert.Equal("12", alarm.Hausnummer);
        Assert.Equal("Rathaus", alarm.Objekt);
        Assert.Equal(48.7647, alarm.Latitude);
        Assert.Equal(9.5961, alarm.Longitude);
        Assert.Equal(["Florian Adelberg 43", "Florian Göppingen 1/33"], alarm.Fahrzeuge);
    }

    [Fact]
    public void Liest_flaches_Format_und_ISO_Zeit()
    {
        var alarm = Fe2Alarm.Lesen("""{"externalId":"X1","timestamp":"2026-10-22T17:45:00+02:00","keyword":"TH 1","message":"Baum auf Straße","lat":48.76,"lng":9.59}""");

        Assert.True(alarm.IstAlarm);
        Assert.Equal("X1", alarm.ExterneId);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 15, 45, 0, TimeSpan.Zero), alarm.Zeitpunkt);
        Assert.Equal(["Baum auf Straße"], alarm.Meldung);
        Assert.Equal(48.76, alarm.Latitude);
    }

    [Theory]
    [InlineData("STATUS")]
    [InlineData("CLOSE")]
    public void Andere_Typen_sind_kein_Alarm(string typ)
    {
        Assert.False(Fe2Alarm.Lesen("{\"type\":\"" + typ + "\",\"data\":{\"externalId\":\"A\"}}").IstAlarm);
    }

    [Theory]
    [InlineData("""{"data":{"externalId":"A","location":{"coordinate":[null,null]}}}""")]
    [InlineData("""{"data":{"externalId":"A","location":{"coordinate":["9.5","48.7"]}}}""")]
    [InlineData("""{"data":{"externalId":"A","location":{"lat":null,"lng":"x"}}}""")]
    public void Koordinaten_ohne_Zahl_werden_ignoriert(string json)
    {
        var alarm = Fe2Alarm.Lesen(json);
        Assert.Equal("A", alarm.ExterneId);
        Assert.Null(alarm.Latitude);
    }

    [Fact]
    public void Unsinnige_Koordinaten_werden_verworfen()
    {
        var alarm = Fe2Alarm.Lesen("""{"data":{"externalId":"A","location":{"coordinate":[0,0]}}}""");
        Assert.Null(alarm.Latitude);
        Assert.Null(alarm.Longitude);
    }
}
