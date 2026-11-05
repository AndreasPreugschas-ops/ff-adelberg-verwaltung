using System.Data.Common;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fevad.Infrastructure.Laufbahn;

/// <summary>
/// Meldet Mitglieder zur Prüfung, wenn sich etwas Prüfrelevantes ändert: Lehrgang, Dienstgrad, Mitgliedschaft,
/// Funktion, Ehrung oder Geburtsdatum. Änderungen an Regeln (Ehrungsarten, Beförderungswege, Funktionen, Parameter)
/// lösen eine Prüfung aller aus. Gemeldet wird erst nach dem Speichern bzw. nach dem Commit der Transaktion.
/// </summary>
public sealed class PruefAusloeser(Pruefwarteschlange warteschlange) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly HashSet<Guid> beimSpeichern = [];
    private readonly HashSet<Guid> vorgemerkt = [];
    private bool alleBeimSpeichern;
    private bool alleVorgemerkt;

    /// <summary>Merkt vor dem Speichern vor, welche Mitglieder neu zu prüfen sind.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core mit dem betroffenen Kontext.</param>
    /// <param name="result">Bisheriges Ergebnis, wird unverändert weitergegeben.</param>
    /// <returns>Das unveränderte Ergebnis.</returns>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Sammeln(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>Merkt vor dem asynchronen Speichern vor, welche Mitglieder neu zu prüfen sind.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core mit dem betroffenen Kontext.</param>
    /// <param name="result">Bisheriges Ergebnis, wird unverändert weitergegeben.</param>
    /// <param name="cancellationToken">Abbruch der Anfrage.</param>
    /// <returns>Das unveränderte Ergebnis.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Sammeln(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Übernimmt nach erfolgreichem Speichern die Vormerkungen; ohne Transaktion wird sofort gemeldet.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core nach dem Speichern.</param>
    /// <param name="result">Anzahl gespeicherter Zeilen.</param>
    /// <returns>Die Anzahl gespeicherter Zeilen.</returns>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Gespeichert(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    /// <summary>Übernimmt nach erfolgreichem asynchronen Speichern die Vormerkungen; ohne Transaktion wird sofort gemeldet.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core nach dem Speichern.</param>
    /// <param name="result">Anzahl gespeicherter Zeilen.</param>
    /// <param name="cancellationToken">Abbruch der Anfrage.</param>
    /// <returns>Die Anzahl gespeicherter Zeilen.</returns>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Gespeichert(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Verwirft die Vormerkungen des fehlgeschlagenen Speicherns.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core mit dem Fehler.</param>
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Verwerfen(nurLaufendes: true);

    /// <summary>Verwirft die Vormerkungen des fehlgeschlagenen asynchronen Speicherns.</summary>
    /// <param name="eventData">Ereignisdaten von EF Core mit dem Fehler.</param>
    /// <param name="cancellationToken">Abbruch der Anfrage.</param>
    /// <returns>Erledigte Aufgabe.</returns>
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Verwerfen(nurLaufendes: true);
        return Task.CompletedTask;
    }

    /// <summary>Meldet nach dem Commit alle vorgemerkten Mitglieder an die Prüfwarteschlange.</summary>
    /// <param name="transaction">Die betroffene Datenbanktransaktion.</param>
    /// <param name="eventData">Ereignisdaten von EF Core zum Ende der Transaktion.</param>
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Melden();

    /// <summary>Meldet nach dem Commit alle vorgemerkten Mitglieder an die Prüfwarteschlange.</summary>
    /// <param name="transaction">Die betroffene Datenbanktransaktion.</param>
    /// <param name="eventData">Ereignisdaten von EF Core zum Ende der Transaktion.</param>
    /// <param name="cancellationToken">Abbruch der Anfrage.</param>
    /// <returns>Erledigte Aufgabe.</returns>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Melden();
        return Task.CompletedTask;
    }

    /// <summary>Verwirft nach einem Rollback alle Vormerkungen der Transaktion.</summary>
    /// <param name="transaction">Die betroffene Datenbanktransaktion.</param>
    /// <param name="eventData">Ereignisdaten von EF Core zum Ende der Transaktion.</param>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Verwerfen(nurLaufendes: false);

    /// <summary>Verwirft nach einem Rollback alle Vormerkungen der Transaktion.</summary>
    /// <param name="transaction">Die betroffene Datenbanktransaktion.</param>
    /// <param name="eventData">Ereignisdaten von EF Core zum Ende der Transaktion.</param>
    /// <param name="cancellationToken">Abbruch der Anfrage.</param>
    /// <returns>Erledigte Aufgabe.</returns>
    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Verwerfen(nurLaufendes: false);
        return Task.CompletedTask;
    }

    private void Sammeln(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Mitglied m:
                    beimSpeichern.Add(m.Id);
                    break;
                case Mitgliedschaft s:
                    beimSpeichern.Add(s.MitgliedId);
                    break;
                case DienstgradZuordnung d:
                    beimSpeichern.Add(d.MitgliedId);
                    break;
                case FunktionZuordnung f:
                    beimSpeichern.Add(f.MitgliedId);
                    break;
                case QualifikationNachweis q:
                    beimSpeichern.Add(q.MitgliedId);
                    break;
                case Ehrung e:
                    beimSpeichern.Add(e.MitgliedId);
                    break;
                case Antrag { MitgliedId: { } antragsteller }:
                    beimSpeichern.Add(antragsteller);
                    break;
                case Ehrungsart or Befoerderungsweg or Dienstgrad or Funktion or Qualifikation or Parameter:
                    alleBeimSpeichern = true;
                    break;
            }
        }
    }

    private void Gespeichert(DbContext? context)
    {
        vorgemerkt.UnionWith(beimSpeichern);
        alleVorgemerkt |= alleBeimSpeichern;
        beimSpeichern.Clear();
        alleBeimSpeichern = false;
        if (context?.Database.CurrentTransaction is null)
        {
            Melden();
        }
    }

    private void Melden()
    {
        if (alleVorgemerkt)
        {
            warteschlange.AlleMelden();
        }
        else if (vorgemerkt.Count > 0)
        {
            warteschlange.Melden(vorgemerkt);
        }

        vorgemerkt.Clear();
        alleVorgemerkt = false;
    }

    private void Verwerfen(bool nurLaufendes)
    {
        beimSpeichern.Clear();
        alleBeimSpeichern = false;
        if (!nurLaufendes)
        {
            vorgemerkt.Clear();
            alleVorgemerkt = false;
        }
    }
}
