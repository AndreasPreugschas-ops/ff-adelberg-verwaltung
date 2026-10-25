using System.Buffers.Binary;
using System.Text;
using Fevad.Domain.Unfaelle;

namespace Fevad.Domain.Tests;

public class FotobereinigungTests
{
    private static byte[] Segment(byte marker, byte[] daten)
    {
        var s = new byte[4 + daten.Length];
        s[0] = 0xFF;
        s[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(2), (ushort)(daten.Length + 2));
        daten.CopyTo(s, 4);
        return s;
    }

    private static byte[] Chunk(string typ, byte[] daten)
    {
        var c = new byte[12 + daten.Length];
        BinaryPrimitives.WriteUInt32BigEndian(c, (uint)daten.Length);
        Encoding.ASCII.GetBytes(typ).CopyTo(c, 4);
        daten.CopyTo(c, 8);
        return c;
    }

    private static byte[] Verbinden(params byte[][] teile) => teile.SelectMany(t => t).ToArray();

    [Fact]
    public void Jpeg_verliert_Exif_und_Kommentar_behaelt_Jfif_und_Bilddaten()
    {
        var jfif = Segment(0xE0, Encoding.ASCII.GetBytes("JFIF\0\x01\x01"));
        var exif = Segment(0xE1, Encoding.ASCII.GetBytes("Exif\0\0GPS 48.76 9.59"));
        var kommentar = Segment(0xFE, Encoding.ASCII.GetBytes("iPhone"));
        var scan = Verbinden(Segment(0xDA, [1, 2, 3]), [0x11, 0x22, 0xFF, 0xD9]);
        var foto = Verbinden([0xFF, 0xD8], jfif, exif, kommentar, scan);

        var sauber = Fotobereinigung.Bereinigen(foto, "image/jpeg");

        Assert.Equal(Verbinden([0xFF, 0xD8], jfif, scan), sauber);
    }

    [Fact]
    public void Jpeg_behaelt_Farbprofil()
    {
        var icc = Segment(0xE2, Encoding.ASCII.GetBytes("ICC_PROFILE\0\x01\x01"));
        var foto = Verbinden([0xFF, 0xD8], icc, Segment(0xDA, [1]), [0xFF, 0xD9]);

        Assert.Equal(foto, Fotobereinigung.Bereinigen(foto, "image/jpeg"));
    }

    [Fact]
    public void Png_verliert_Text_und_Exif_Chunks()
    {
        byte[] signatur = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var ihdr = Chunk("IHDR", new byte[13]);
        var idat = Chunk("IDAT", [1, 2, 3]);
        var iend = Chunk("IEND", []);
        var foto = Verbinden(signatur, ihdr, Chunk("tEXt", Encoding.ASCII.GetBytes("Author\0Max")), Chunk("eXIf", [1, 2]), idat, iend);

        Assert.Equal(Verbinden(signatur, ihdr, idat, iend), Fotobereinigung.Bereinigen(foto, "image/png"));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public void Kaputte_Datei_ergibt_null(string typ)
    {
        Assert.Null(Fotobereinigung.Bereinigen(Encoding.ASCII.GetBytes("kein Bild, nur Text"), typ));
        Assert.Null(Fotobereinigung.Bereinigen([0xFF, 0xD8, 0xFF, 0xE1, 0x10, 0x00], typ));
    }
}
