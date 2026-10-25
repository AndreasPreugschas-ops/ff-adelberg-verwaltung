using System.Buffers.Binary;
using System.Text;

namespace Fevad.Domain.Unfaelle;

/// <summary>
/// Entfernt Metadaten aus hochgeladenen Fotos, ohne das Bild neu zu kodieren: bei JPEG die Segmente EXIF/XMP (APP1),
/// APP3 bis APP13, APP15 und Kommentare, bei PNG die Text-, EXIF- und Zeit-Chunks. Handyfotos tragen sonst oft
/// GPS-Ort, Gerät und Aufnahmezeit mit (Hinweis Datenschutz, 25.10.2026). Die Ausrichtung aus EXIF geht dabei verloren.
/// </summary>
public static class Fotobereinigung
{
    private static readonly byte[] PngSignatur = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly HashSet<string> PngWeg = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    /// <summary>Gibt das Bild ohne Metadaten zurück.</summary>
    /// <param name="inhalt">Bilddaten.</param>
    /// <param name="inhaltstyp">„image/jpeg“ oder „image/png“.</param>
    /// <returns>Die bereinigten Bytes oder <c>null</c>, wenn die Datei kein gültiges Bild dieses Typs ist.</returns>
    public static byte[]? Bereinigen(byte[] inhalt, string inhaltstyp) => inhaltstyp switch
    {
        "image/jpeg" => Jpeg(inhalt),
        "image/png" => Png(inhalt),
        _ => null,
    };

    private static byte[]? Jpeg(byte[] d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8)
        {
            return null;
        }

        using var aus = new MemoryStream(d.Length);
        aus.Write(d, 0, 2);
        var i = 2;
        while (i + 4 <= d.Length)
        {
            if (d[i] != 0xFF)
            {
                return null;
            }

            var marker = d[i + 1];
            if (marker == 0xFF)
            {
                // Füllbyte vor einem Marker.
                i++;
                continue;
            }

            var laenge = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(i + 2, 2));
            if (laenge < 2 || i + 2 + laenge > d.Length)
            {
                return null;
            }

            if (marker == 0xDA)
            {
                // Start of Scan: ab hier folgen die Bilddaten bis zum Ende unverändert.
                aus.Write(d, i, d.Length - i);
                return aus.ToArray();
            }

            if (!JpegSegmentWeg(marker, d.AsSpan(i + 4, laenge - 2)))
            {
                aus.Write(d, i, laenge + 2);
            }

            i += laenge + 2;
        }

        return null;
    }

    /// <summary>APP1 (EXIF, XMP), APP3–APP13, APP15 und Kommentare fallen weg; APP0 (JFIF), APP2 nur als ICC-Farbprofil und APP14 (Adobe) bleiben.</summary>
    private static bool JpegSegmentWeg(byte marker, ReadOnlySpan<byte> daten) => marker switch
    {
        0xE0 or 0xEE => false,
        0xE2 => !daten.StartsWith("ICC_PROFILE\0"u8),
        >= 0xE1 and <= 0xEF => true,
        0xFE => true,
        _ => false,
    };

    private static byte[]? Png(byte[] d)
    {
        if (d.Length < PngSignatur.Length || !d.AsSpan(0, PngSignatur.Length).SequenceEqual(PngSignatur))
        {
            return null;
        }

        using var aus = new MemoryStream(d.Length);
        aus.Write(PngSignatur);
        var i = PngSignatur.Length;
        while (i + 12 <= d.Length)
        {
            var laenge = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(i, 4));
            if (laenge > int.MaxValue - 12 || i + 12 + (int)laenge > d.Length)
            {
                return null;
            }

            var typ = Encoding.ASCII.GetString(d, i + 4, 4);
            var gesamt = 12 + (int)laenge;
            if (!PngWeg.Contains(typ))
            {
                aus.Write(d, i, gesamt);
            }

            i += gesamt;
            if (typ == "IEND")
            {
                return aus.ToArray();
            }
        }

        return null;
    }
}
