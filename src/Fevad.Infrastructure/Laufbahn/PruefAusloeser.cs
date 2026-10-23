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

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Sammeln(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Sammeln(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Gespeichert(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Gespeichert(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Verwerfen(nurLaufendes: true);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Verwerfen(nurLaufendes: true);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Melden();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Melden();
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Verwerfen(nurLaufendes: false);

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
