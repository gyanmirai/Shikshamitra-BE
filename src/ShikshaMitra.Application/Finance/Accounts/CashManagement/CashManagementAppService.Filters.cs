using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task<IQueryable<CashCollection>> ApplyDisplayFiltersAsync(IQueryable<CashCollection> query, CashQuery input)
	{
		var year = await years.GetAsync(input.AcademicYearId);
		var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, input.Month, input.Year);
		if (input.Month.HasValue)
		{
			var start = new DateTimeOffset(period.StartUtc);
			var end = new DateTimeOffset(period.EndUtc);
			// Keep instant comparison exact on every provider, including SQLite,
			// which cannot translate DateTimeOffset inequalities. Only authorized
			// source IDs/timestamps are materialized; counts/paging remain in SQL.
			var dates = await AsyncExecuter.ToListAsync(query.Select(x => new { x.Id, x.CollectedAt }));
			var ids = dates.Where(x => x.CollectedAt >= start && x.CollectedAt < end).Select(x => x.Id).ToArray();
			query = query.Where(x => Enumerable.Contains(ids, x.Id));
		}
		if (input.BoardId.HasValue || input.MediumId.HasValue)
		{
			var accounts = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetQueryableAsync())
				.Where(x => (!input.BoardId.HasValue || x.BoardId == input.BoardId) && (!input.MediumId.HasValue || x.EducationMediumId == input.MediumId)).Select(x => x.Id);
			var students = (await LazyServiceProvider.LazyGetRequiredService<IRepository<Student, Guid>>().GetQueryableAsync())
				.Where(x => (!input.BoardId.HasValue || x.AcademicBoardId == input.BoardId) && (!input.MediumId.HasValue || x.AcademicMediumId == input.MediumId)).Select(x => x.Id);
			query = query.Where(x => x.FinanceFeeAccountId.HasValue ? accounts.Contains(x.FinanceFeeAccountId.Value) : x.StudentId.HasValue && students.Contains(x.StudentId.Value));
		}
		return query;
	}
	private async Task<CashSummary> SummarizeAsync(IQueryable<CashCollection> query)
	{
		var rows = await AsyncExecuter.ToListAsync(query.Select(x => new { x.Amount, x.Status, x.SpentAmount, x.DepositedAmount, x.InTransitAmount }));
		return new CashSummary { Collected = rows.Sum(x => x.Amount),
			PendingApproval = rows.Where(x => x.Status == 0).Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount),
			Approved = rows.Where(x => x.Status == 1).Sum(x => x.Amount), Spent = rows.Sum(x => x.SpentAmount),
			Deposited = rows.Sum(x => x.DepositedAmount), InTransit = rows.Sum(x => x.InTransitAmount),
			Remaining = rows.Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount) };
	}
}
