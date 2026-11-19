using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Dashboard;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
    internal async Task<FinanceDashboardBalancesDto> ReadDashboardBalancesAsync(Guid yearId, bool canViewCharges,
        CancellationToken cancellationToken, DateOnly? from, DateOnly? to)
    {
        var scope = LazyServiceProvider.LazyGetRequiredService<FinanceDashboardPayerScope>();
        var payers = await scope.ReadAsync(yearId, cancellationToken);
        var yearHeads = (await feeHeadRepository.GetQueryableAsync())
            .Where(head => head.TenantId == CurrentTenant.Id && head.AcademicYearId == yearId).Select(head => head.Id);
        var dues = (await dueFeeRepository.GetQueryableAsync())
            .Where(due => due.TenantId == CurrentTenant.Id && yearHeads.Contains(due.FeeHeadId) && due.OutstandingAmount > 0);
        (DateTime StartUtc, DateTime EndUtc)? chargePeriod = null;
        if (from.HasValue && to.HasValue)
        {
            var firstDueDate = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var afterLastDueDate = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            dues = dues.Where(due => (due.ScheduleDueDate ?? due.DueDate) >= firstDueDate &&
                (due.ScheduleDueDate ?? due.DueDate) < afterLastDueDate);
            chargePeriod = AccountsRegisterPeriod.Resolve(firstDueDate, afterLastDueDate.AddDays(-1), null, null);
        }
        var schoolDate = clock.Now.ToUniversalTime().AddMinutes(330).Date;
        var overdueIds = dues.Where(DueFeeCollectionStatusResolver.OverdueOn(schoolDate)).Select(due => due.Id);
        var legacy = from due in dues
            join placement in payers.Placements on due.StudentId equals (Guid?)placement.StudentId
            where !due.FinanceFeeAccountId.HasValue
            select new { placement.ClassId, due.StudentId, due.OutstandingAmount, Overdue = overdueIds.Contains(due.Id) };
        var owned = from due in dues
            join account in payers.Accounts on due.FinanceFeeAccountId equals (Guid?)account.Id
            select new { ClassId = (Guid?)account.StandardId, account.StudentId, due.OutstandingAmount, Overdue = overdueIds.Contains(due.Id) };
        var balances = await AsyncExecuter.ToListAsync(legacy.Concat(owned)
            .GroupBy(row => new { row.ClassId, row.StudentId })
            .Select(group => new FinanceDashboardBalanceProjection.PayerBalance(group.Key.ClassId, group.Key.StudentId,
                group.Sum(row => row.OutstandingAmount), group.Sum(row => row.Overdue ? row.OutstandingAmount : 0m))),
            cancellationToken);

        FinanceDashboardBalanceProjection.ClassCharges[]? charges = null;
        if (canViewCharges)
        {
            var placements = await AsyncExecuter.ToListAsync(payers.Placements, cancellationToken);
            var pending = await BuildChargeAggregatesByStudentAsync(placements.Select(row => row.StudentId).ToList(),
                yearId, cancellationToken, chargePeriod);
            charges = placements.GroupBy(row => row.ClassId)
                .Select(group => new FinanceDashboardBalanceProjection.ClassCharges(group.Key,
                    group.Sum(row => pending[row.StudentId].PendingCharges)))
                .Where(row => row.Outstanding > 0)
                .ToArray();
        }

        var classIds = balances.Select(row => row.ClassId)
            .Concat(charges?.Select(row => row.ClassId) ?? []).OfType<Guid>().Distinct().ToArray();
        var classes = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
            .Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(classIds, row.Id) &&
                row.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == yearId)
            .Select(row => new FinanceDashboardBalanceProjection.ClassContext(row.Id, row.Name,
                row.EducationMediumStage.EducationMedium.EducationBoardId, row.EducationMediumStage.EducationMediumId,
                row.EducationMediumStage.EducationMedium.EducationBoard.Name + " / " + row.EducationMediumStage.EducationMedium.Name)),
            cancellationToken);
        return FinanceDashboardBalanceProjection.Build(balances, charges, classes);
    }
}
