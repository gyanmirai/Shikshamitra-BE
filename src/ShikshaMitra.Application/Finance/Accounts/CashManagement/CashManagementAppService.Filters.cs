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
	private async Task<IQueryable<CashCollection>> ApplyCollectionMonthAsync(IQueryable<CashCollection> query, CashQuery input)
	{
		var year = await years.GetAsync(input.AcademicYearId);
		var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, input.Month, input.Year);
		return input.Month.HasValue
			? await reads.FilterCollectionPeriodAsync(query, new DateTimeOffset(period.StartUtc), new DateTimeOffset(period.EndUtc))
			: query;
	}
	private async Task<CashSummary> SummarizeAsync(IQueryable<CashCollection> query)
	{
		var facts = await reads.SummarizeAsync(query);
		return new CashSummary
		{
			Collected = facts.Collected, PendingApproval = facts.PendingApproval, Approved = facts.Approved,
			Spent = facts.Spent, Deposited = facts.Deposited, InTransit = facts.InTransit, Remaining = facts.Remaining
		};
	}
}
