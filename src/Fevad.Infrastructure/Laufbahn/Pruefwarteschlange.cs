using System.Threading.Channels;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>
/// Mitglieder, die nach einer Änderung neu geprüft werden sollen. Null steht für „alle“ (z. B. nach einer Regeländerung).
/// Gelesen vom <see cref="PersonalpruefungDienst"/>.
/// </summary>
public sealed class Pruefwarteschlange
{
    private readonly Channel<Guid?> kanal = Channel.CreateBounded<Guid?>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>Leseseite für den <see cref="PersonalpruefungDienst"/>.</summary>
    public ChannelReader<Guid?> Leser => kanal.Reader;

    /// <summary>Meldet Mitglieder zur erneuten Prüfung.</summary>
    /// <param name="mitglieder">Schlüssel der geänderten Mitglieder.</param>
    public void Melden(IEnumerable<Guid> mitglieder)
    {
        foreach (var id in mitglieder)
        {
            kanal.Writer.TryWrite(id);
        }
    }

    /// <summary>Meldet eine Prüfung aller Mitglieder, z. B. nach einer Regeländerung.</summary>
    public void AlleMelden() => kanal.Writer.TryWrite(null);
}
