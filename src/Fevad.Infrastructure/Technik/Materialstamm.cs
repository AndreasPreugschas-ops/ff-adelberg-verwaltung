using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>Gemeinsame Regeln für Materialstamm und Inventurgruppe, damit Technik und Kleiderkammer gleich zuordnen.</summary>
public static class Materialstamm
{
    /// <summary>Passenden Materialstamm suchen oder im Kontext neu anlegen (gespeichert wird mit dem Gerät).</summary>
    public static async Task<Material> FindenOderAnlegenAsync(
        AppDbContext db, string bezeichnung, Guid geraeteartId, string? hersteller, string? typ, bool inventurpflichtig, CancellationToken ct)
    {
        bezeichnung = bezeichnung.Trim();
        hersteller = Leer(hersteller);
        typ = Leer(typ);

        var material = db.Materialien.Local.FirstOrDefault(m => Passt(m, bezeichnung, geraeteartId, hersteller, typ))
            ?? await db.Materialien.FirstOrDefaultAsync(m => m.Bezeichnung == bezeichnung && m.GeraeteartId == geraeteartId
                && m.Hersteller == hersteller && m.Typ == typ, ct);
        if (material is null)
        {
            material = new Material
            {
                Bezeichnung = bezeichnung,
                GeraeteartId = geraeteartId,
                Hersteller = hersteller,
                Typ = typ,
                Inventurpflichtig = inventurpflichtig,
            };
            db.Materialien.Add(material);
        }

        return material;
    }

    /// <summary>Gerät einem Materialstamm zuordnen; die Geräteart und damit die Prüfpflicht kommen vom Material.</summary>
    public static void Zuordnen(Geraet geraet, Material material)
    {
        geraet.MaterialId = material.Id;
        geraet.Material = material;
        geraet.GeraeteartId = material.GeraeteartId;
    }

    /// <summary>Inventurgruppe des Fahrzeugs; wird bei Bedarf angelegt.</summary>
    public static async Task<Inventurgruppe> FahrzeuggruppeAsync(AppDbContext db, Guid fahrzeugId, CancellationToken ct)
    {
        var gruppe = db.Inventurgruppen.Local.FirstOrDefault(g => g.FahrzeugId == fahrzeugId)
            ?? await db.Inventurgruppen.FirstOrDefaultAsync(g => g.FahrzeugId == fahrzeugId, ct);
        if (gruppe is null)
        {
            var name = await db.Fahrzeuge.Where(f => f.Id == fahrzeugId).Select(f => f.Funkrufname).SingleAsync(ct);
            gruppe = new Inventurgruppe { Bezeichnung = name, Art = InventurgruppenArt.Fahrzeug, FahrzeugId = fahrzeugId };
            db.Inventurgruppen.Add(gruppe);
        }

        return gruppe;
    }

    private static bool Passt(Material m, string bezeichnung, Guid geraeteartId, string? hersteller, string? typ) =>
        m.Bezeichnung == bezeichnung && m.GeraeteartId == geraeteartId && m.Hersteller == hersteller && m.Typ == typ;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
