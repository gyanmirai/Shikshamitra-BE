using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>
/// Internal atomic journal writer. Call from the accepted source command's unit of work,
/// after building a frozen allocation/account snapshot. Never call for an uncertain payment.
/// </summary>
[RemoteService(false)]
public class AccountsJournalPoster(
    IRepository<AccountsPostingConfiguration, Guid> configurationRepository,
    IRepository<AccountingPeriod, Guid> periodRepository,
    IRepository<AccountHead, Guid> headRepository,
    IRepository<FinancialEvent, Guid> eventRepository,
    IRepository<JournalEntry, Guid> journalRepository,
    IUnitOfWorkManager unitOfWorkManager) : ApplicationService
{
    public async Task<JournalEntry> PostAsync(Guid academicYearId, string sourceType, Guid sourceId,
        string transition, int transitionVersion, DateOnly businessDate, decimal amount,
        string snapshotJson, Guid actorId, IReadOnlyList<JournalPostingLine> lines,
        Guid? correctsJournalEntryId = null)
    {
        if (unitOfWorkManager.Current == null)
            throw new InvalidOperationException("Journal posting requires the source transaction's unit of work.");
        var now = DateTimeOffset.UtcNow;
        var financialEvent = new FinancialEvent(GuidGenerator.Create(), CurrentTenant.Id,
            academicYearId, sourceType, sourceId, transition, transitionVersion, now,
            businessDate, "INR", amount, snapshotJson);
        var existing = await eventRepository.FirstOrDefaultAsync(x => x.EventKey == financialEvent.EventKey);
        if (existing != null)
        {
            if (existing.SnapshotHash != financialEvent.SnapshotHash || existing.Amount != amount ||
                existing.BusinessDate != businessDate)
                throw new UserFriendlyException("This source transition already has a different Accounts posting.");
            return await journalRepository.FirstOrDefaultAsync(x => x.FinancialEventId == existing.Id)
                ?? throw new UserFriendlyException("The existing source event has no journal. Contact Accounts support.");
        }
        var configuration = await configurationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
        if (configuration?.IsEnabled != true || !configuration.FirstPostingDate.HasValue ||
            businessDate < configuration.FirstPostingDate.Value)
            throw new UserFriendlyException("Accounts posting is not active for this school and date.");
        var periods = await periodRepository.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
            x.StartsOn <= businessDate && x.EndsOn >= businessDate && x.IsOpen);
        if (periods.Count != 1)
            throw new UserFriendlyException("The payment date needs exactly one open financial period.");
        if (lines.Count < 2 || lines.Any(x => x.Amount <= 0 || decimal.Round(x.Amount, 2) != x.Amount) ||
            lines.Sum(x => x.IsDebit ? x.Amount : -x.Amount) != 0 ||
            lines.Sum(x => x.IsDebit ? x.Amount : 0) != amount)
            throw new UserFriendlyException("The source allocations must balance to the accepted amount in paise.");
        foreach (var id in lines.Select(x => x.AccountHeadId).Distinct())
        {
            var head = await headRepository.FindAsync(id);
            if (head == null || head.TenantId != CurrentTenant.Id ||
                head.AcademicYearId != academicYearId || !head.IsActive)
                throw new UserFriendlyException("A journal account is inactive or belongs to another school/year.");
        }
        if (correctsJournalEntryId is { } originalId)
        {
            var original = await journalRepository.FindAsync(originalId);
            if (original == null || original.TenantId != CurrentTenant.Id)
                throw new UserFriendlyException("The corrected journal must belong to this school.");
        }
        var journalId = GuidGenerator.Create();
        var journalLines = lines.Select(x => new JournalLine(GuidGenerator.Create(), journalId,
            x.AccountHeadId, x.SourceAllocationId, x.IsDebit ? x.Amount : 0,
            x.IsDebit ? 0 : x.Amount)).ToList();
        var journal = new JournalEntry(journalId, CurrentTenant.Id, financialEvent.Id,
            periods[0].Id, correctsJournalEntryId, businessDate, now, "INR",
            $"JRN-{journalId:N}", actorId, journalLines);
        await eventRepository.InsertAsync(financialEvent, autoSave: false);
        await journalRepository.InsertAsync(journal, autoSave: true);
        return journal;
    }
}

public sealed record JournalPostingLine(Guid AccountHeadId, Guid? SourceAllocationId,
    decimal Amount, bool IsDebit);
