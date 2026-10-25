using System.IO.Compression;
using System.Text;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Kassen;

namespace Fevad.Integration.Tests;

/// <summary>Kontoauszüge lesen: camt.053 und CSV nach Bankformat (P2: V07).</summary>
public class KontoauszugleserTests
{
    /// <summary>Kleiner Tagesauszug im Format camt.053.001.08 mit Gutschrift, Lastschrift, Sammelposten und vorgemerktem Umsatz.</summary>
    public static string Camt(string iban, DateOnly tag, decimal schlusssaldo, params string[] eintraege) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
          <BkToCstmrStmt>
            <GrpHdr><MsgId>1</MsgId><CreDtTm>{tag:yyyy-MM-dd}T18:00:00</CreDtTm></GrpHdr>
            <Stmt>
              <Id>1</Id>
              <Acct><Id><IBAN>{iban}</IBAN></Id></Acct>
              <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">1.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>{tag.AddDays(-1):yyyy-MM-dd}</Dt></Dt></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">{Math.Abs(schlusssaldo).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</Amt><CdtDbtInd>{(schlusssaldo < 0 ? "DBIT" : "CRDT")}</CdtDbtInd><Dt><Dt>{tag:yyyy-MM-dd}</Dt></Dt></Bal>
              {string.Join("\n", eintraege)}
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;

    /// <summary>Ein Umsatz im camt.053.</summary>
    public static string Eintrag(DateOnly tag, decimal betrag, string name, string zweck, string? referenz = null, string status = "BOOK") => $"""
        <Ntry>
          <Amt Ccy="EUR">{Math.Abs(betrag).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</Amt>
          <CdtDbtInd>{(betrag < 0 ? "DBIT" : "CRDT")}</CdtDbtInd>
          <Sts><Cd>{status}</Cd></Sts>
          <BookgDt><Dt>{tag:yyyy-MM-dd}</Dt></BookgDt>
          <ValDt><Dt>{tag:yyyy-MM-dd}</Dt></ValDt>
          <NtryDtls><TxDtls>
            <Refs><EndToEndId>{referenz ?? "NOTPROVIDED"}</EndToEndId></Refs>
            <RltdPties><{(betrag < 0 ? "Cdtr" : "Dbtr")}><Pty><Nm>{name}</Nm></Pty></{(betrag < 0 ? "Cdtr" : "Dbtr")}></RltdPties>
            <RmtInf><Ustrd>{zweck}</Ustrd></RmtInf>
          </TxDtls></NtryDtls>
        </Ntry>
        """;

    [Fact]
    public void Camt_liest_gebuchte_Umsaetze_Saldo_und_IBAN()
    {
        var tag = new DateOnly(2026, 10, 20);
        var xml = Camt("DE02120300000000202051", tag, 1234.56m,
            Eintrag(tag, 600m, "Gemeinde Adelberg", "Zuwendung Jugendfeuerwehr"),
            Eintrag(tag, -45.50m, "Kai Muster", "Auslagenerstattung", "PNK123-0123456789abcdef"),
            Eintrag(tag, -9.99m, "Bank", "vorgemerkt", status: "PDNG"));
        var auszug = Kontoauszugleser.Camt(Encoding.UTF8.GetBytes(xml), "auszug.xml");

        Assert.Equal("DE02120300000000202051", auszug.Iban);
        Assert.Equal(1234.56m, auszug.Endsaldo);
        Assert.Equal(2, auszug.Zeilen.Count);
        var eingang = auszug.Zeilen[0];
        Assert.Equal((600m, "Gemeinde Adelberg", "Zuwendung Jugendfeuerwehr", (string?)null), (eingang.Betrag, eingang.Gegenpartei, eingang.Verwendungszweck, eingang.Referenz));
        var ausgang = auszug.Zeilen[1];
        Assert.Equal(-45.50m, ausgang.Betrag);
        Assert.Equal("Kai Muster", ausgang.Gegenpartei);
        Assert.Equal("0123456789abcdef", Bankabgleich.SchluesselAusReferenz(ausgang.Referenz));
    }

    [Fact]
    public void Camt_aus_ZIP_und_kaputte_Dateien()
    {
        var tag = new DateOnly(2026, 10, 21);
        using var speicher = new MemoryStream();
        using (var zip = new ZipArchive(speicher, ZipArchiveMode.Create, true))
        {
            foreach (var (name, betrag) in new[] { ("1.xml", 10m), ("2.xml", -20m) })
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(Camt("DE02120300000000202051", tag, 5m, Eintrag(tag, betrag, "X", "Y")));
            }
        }

        Assert.Equal([10m, -20m], Kontoauszugleser.Camt(speicher.ToArray(), "auszuege.zip").Zeilen.Select(z => z.Betrag));
        Assert.Throws<FormatException>(() => Kontoauszugleser.Camt("<kein>xml"u8.ToArray(), "a.xml"));
        Assert.Throws<FormatException>(() => Kontoauszugleser.Camt("<Document/>"u8.ToArray(), "a.xml"));
    }

    [Fact]
    public void Csv_mit_Kopfzeilen_Soll_Haben_Kennzeichen_und_deutschen_Betraegen()
    {
        var csv = """
            Umsatzanzeige;Konto DE02...
            Zeitraum;01.10.2026 - 20.10.2026
            Buchungstag;Valuta;Auftraggeber/Zahlungsempfänger;Verwendungszweck;Betrag;Soll/Haben
            20.10.2026;20.10.2026;Gemeinde Adelberg;"Zuwendung; Jugend";600,00;H
            21.10.2026;21.10.2026;Getränke Maier;Rechnung 4711;1.234,50;S
            ;;;Summe;1.834,50;
            """;
        var format = new Bankformat
        {
            Name = "VR", KopfzeilenUeberspringen = 2, SpalteBuchungstag = "Buchungstag", SpalteValuta = "Valuta", SpalteBetrag = "Betrag",
            SpalteSollHaben = "Soll/Haben", SpalteGegenpartei = "Auftraggeber/Zahlungsempfänger", SpalteVerwendungszweck = "Verwendungszweck",
        };
        var zeilen = Kontoauszugleser.Csv(Encoding.UTF8.GetBytes(csv), format).Zeilen;
        Assert.Equal(2, zeilen.Count);
        Assert.Equal((new DateOnly(2026, 10, 20), 600m, "Zuwendung; Jugend"), (zeilen[0].Buchungstag, zeilen[0].Betrag, zeilen[0].Verwendungszweck));
        Assert.Equal(-1234.50m, zeilen[1].Betrag);

        format.SpalteBetrag = "Umsatz";
        var fehler = Assert.Throws<FormatException>(() => Kontoauszugleser.Csv(Encoding.UTF8.GetBytes(csv), format));
        Assert.Contains("„Umsatz“ nicht gefunden", fehler.Message);
    }

    [Fact]
    public void Csv_mit_getrennten_Soll_und_Haben_Spalten_und_Dezimalpunkt()
    {
        var csv = "Date,Debit,Credit,Payee,Memo\n2026-10-20,,600.00,Gemeinde,Zuwendung\n2026-10-21,19.99,,Shop,Material\n";
        var format = new Bankformat
        {
            Name = "Englisch", SpalteBuchungstag = "Date", SpalteSoll = "Debit", SpalteHaben = "Credit", SpalteGegenpartei = "Payee",
            SpalteVerwendungszweck = "Memo", Datumsformat = "yyyy-MM-dd", Dezimalkomma = false,
        };
        Assert.Equal([600m, -19.99m], Kontoauszugleser.Csv(Encoding.UTF8.GetBytes(csv), format).Zeilen.Select(z => z.Betrag));
    }

    [Fact]
    public void Fingerabdruck_unterscheidet_gleiche_Umsaetze_am_selben_Tag_und_ignoriert_Zeilenumbrueche()
    {
        var konto = Guid.NewGuid();
        var a = new Auszugszeile(new(2026, 10, 20), null, -5m, "Bäcker", "Brezeln\nÜbung", null);
        var b = a with { Verwendungszweck = "Brezeln Übung" };
        Assert.Equal(Bankabgleich.Fingerabdruck(konto, a, 1), Bankabgleich.Fingerabdruck(konto, b, 1));
        Assert.NotEqual(Bankabgleich.Fingerabdruck(konto, a, 1), Bankabgleich.Fingerabdruck(konto, a, 2));
        Assert.Null(Bankabgleich.SchluesselAusReferenz("RE-2026-17"));
    }
}
