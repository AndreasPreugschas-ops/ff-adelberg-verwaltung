using Fevad.Domain.Atemschutz;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Atemschutz;

/// <summary>Überträgt bei der Freigabe eines Einsatzberichts Atemschutz und Exposition in die Atemschutznachweise (V11, V12).</summary>
public sealed class Atemschutzuebernahme(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Gleicht die Nachweise des Einsatzes ab; gespeichert wird mit dem Bericht. Gibt die Anzahl neuer Einträge zurück.</summary>
    public async Task<int> AbgleichenAsync(Einsatz einsatz, CancellationToken ct)
    {
        var abMinuten = await parameter.ZahlAsync(ParameterSchluessel.EinsatzAlsEinsatzuebungAbMinuten, Ortszeit.Datum(einsatz.Alarmiert), 0, ct);
        var vorhandene = await db.AtemschutzNachweise.Where(n => n.EinsatzId == einsatz.Id).ToListAsync(ct);
        var abgleich = AtemschutzAusEinsatz.Abgleichen(einsatz, vorhandene, abMinuten);
        db.AtemschutzNachweise.AddRange(abgleich.Neu);
        db.AtemschutzNachweise.RemoveRange(abgleich.Entfernt);
        return abgleich.Neu.Count;
    }
}
