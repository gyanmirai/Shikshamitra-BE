using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp;
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
	private static void ValidateReadInput(CashQuery input)
	{
		if (input.Page < 1 || input.PageSize is < 1 or > 100 ||
			(input.Page - 1L) * input.PageSize > int.MaxValue ||
			input.Status is < 0 or > 1 || input.Search?.Length > 200)
			throw new BusinessException(CashManagementErrorCodes.InvalidQuery);
	}

	private async Task<(IQueryable<CashCollection> Query, FinanceTableQuery Table)> ReadFilteredSourcesAsync(
		IQueryable<CashCollection> query, CashQuery input)
	{
		query = await ApplyDisplayFiltersAsync(query, input);
		var table = FinanceTableQuery.Parse(input.Filters, input.Search, input.SortBy, input.SortOrder, TableColumns);
		query = table.Apply(query, TableFields);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Reference.ToLower().Contains(search) || x.PayerName.ToLower().Contains(search));
		}
		return (query, table);
	}

	private async Task<(DateTimeOffset Start, DateTimeOffset End)?> ResolveActivityPeriodAsync(CashQuery input)
	{
		if (input.StartDate.HasValue || input.EndDate.HasValue)
		{
			if (input.StartDate is not { } start || input.EndDate is not { } end ||
				input.Month.HasValue || input.Year.HasValue ||
				start > end || start == DateOnly.MinValue || end == DateOnly.MaxValue)
				throw new BusinessException(AccountsRegisterErrorCodes.InvalidPeriod);
			var range = AccountsRegisterPeriod.Resolve(start.ToDateTime(TimeOnly.MinValue),
				end.ToDateTime(TimeOnly.MinValue), null, null);
			return (new(range.StartUtc), new(range.EndUtc));
		}
		if (!input.Month.HasValue && !input.Year.HasValue) return null;
		var year = await years.GetAsync(input.AcademicYearId);
		var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, input.Month, input.Year);
		return (new(period.StartUtc), new(period.EndUtc));
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
