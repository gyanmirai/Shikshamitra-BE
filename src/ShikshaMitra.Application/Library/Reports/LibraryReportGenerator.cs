using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> copyRepository,
	IRepository<LibraryMember, Guid> membershipRepository,
	LibraryStoredLoanScope loanScope,
	LibraryStoredReservationScope requestScope,
	IRepository<LibraryCharge, Guid> chargeRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository,
	IRepository<LibraryShelf, Guid> shelfRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ILibraryAccessService libraryAccess,
	ICurrentTenant tenant,
	LibraryCalendarService calendar,
	IClock clock,
	LibraryCatalogueLabelReader catalogueLabels,
	ILibraryChargePositionReader chargePositionReader)
	: ITransientDependency
{
	public async Task<LibraryReportPreviewDto> GenerateAsync(
		LibraryReportRequestDto input,
		LibraryReportExecutionScope executionScope,
		bool exportAllRows)
	{
		EnsureExecutionScope(executionScope);
		var definition = LibraryReportCatalog.Resolve(input.ReportType);
		var filters = NormalizeFilters(input.Filters);
		var context = definition.Id == LibraryReportTypes.FineCollection ? null :
			(await LoadContextAsync(filters, executionScope, definition.Id)) with
			{
				ReadDay = await LibraryReadDateEvidence.ReadAsync(calendar, clock, filters.AsOfDate),
				CatalogueLabels = await catalogueLabels.ReadAsync()
			};
		var rows = context == null ? await ReadFineRowsAsync(executionScope, filters) : definition.Id switch
		{
			LibraryReportTypes.BookMaster => BuildBookMasterRows(context, filters),
			LibraryReportTypes.StockSummary => BuildStockSummaryRows(context, filters),
			LibraryReportTypes.AvailabilityStatus => BuildAvailabilityRows(context, filters),
			LibraryReportTypes.IssueReturn => BuildIssueReturnRows(context, filters),
			LibraryReportTypes.OverdueBooks => BuildOverdueRows(context, filters),
			LibraryReportTypes.LostDamagedBooks => BuildLostDamagedRows(context, filters),
			LibraryReportTypes.MemberList => await BuildMemberRowsAsync(context, filters),
			LibraryReportTypes.ReservationList => BuildReservationRows(context, filters),
			LibraryReportTypes.PopularBooks => BuildPopularRows(context, filters),
			_ => []
		};

		rows = ApplySearch(rows, filters.Search);
		rows = ApplySorts(rows, input.Sorts);

		var totalCount = rows.Count;
		var page = Math.Max(input.Page, 1);
		var pageSize = exportAllRows ? Math.Max(totalCount, 1) : Math.Clamp(input.PageSize <= 0 ? 50 : input.PageSize, 1, 500);
		var offset = ((long)page - 1) * pageSize;
		var pagedRows = exportAllRows
			? rows
			: offset >= totalCount ? [] : rows.Skip((int)offset).Take(pageSize).ToList();

		var result = new LibraryReportPreviewDto
		{
			ReportType = definition.Id,
			TotalCount = totalCount,
			Page = page,
			PageSize = pageSize,
			Columns = LibraryReportCatalog.ResolveColumns(definition, input.Columns, rows),
			Rows = pagedRows,
			NormalizedFilters = filters,
			QueryWarnings = BuildWarnings(definition, rows)
		};
		if (context != null) ApplyDateEvidence(result, context, filters);
		else if (filters.FromDate.HasValue || filters.ToDate.HasValue)
			result.QueryWarnings.Add("The date range includes assessments with confirmed school dates.");
		if (definition.Id == LibraryReportTypes.MemberList && filters.AsOfDate.HasValue)
			result.QueryWarnings.Add("The school date applies to circulation. Finance balances reflect current verified evidence.");
		return result;
	}

	private void EnsureExecutionScope(LibraryReportExecutionScope scope)
	{
		if (scope.TenantId != tenant.Id || scope.UserScope.TenantId != tenant.Id || !scope.UserScope.HasAnyAccess)
			throw new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied);
	}

	private static List<Dictionary<string, string?>> ApplySearch(List<Dictionary<string, string?>> rows, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return rows;
		}

		var normalized = search.Trim();
		return rows
			.Where(row => row.Values.Any(value => value?.Contains(normalized, StringComparison.OrdinalIgnoreCase) == true))
			.ToList();
	}

	private static List<Dictionary<string, string?>> ApplySorts(List<Dictionary<string, string?>> rows, IReadOnlyCollection<LibraryReportSortDto>? sorts)
	{
		if (sorts is not { Count: > 0 })
		{
			return rows;
		}

		IOrderedEnumerable<Dictionary<string, string?>>? ordered = null;
		foreach (var sort in sorts.Where(x => !string.IsNullOrWhiteSpace(x.Field)))
		{
			var descending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
			var comparer = LibraryReportSortComparer.For(sort.Field);
			ordered = ordered == null
				? descending
					? rows.OrderByDescending(row => SortValue(row, sort.Field), comparer)
					: rows.OrderBy(row => SortValue(row, sort.Field), comparer)
				: descending
					? ordered.ThenByDescending(row => SortValue(row, sort.Field), comparer)
					: ordered.ThenBy(row => SortValue(row, sort.Field), comparer);
		}

		return ordered?.ToList() ?? rows;
	}

	private static LibraryReportFilterDto NormalizeFilters(LibraryReportFilterDto? filters)
	{
		var normalized = filters ?? new LibraryReportFilterDto();
		normalized.Search = string.IsNullOrWhiteSpace(normalized.Search) ? null : normalized.Search.Trim();
		if (normalized.FromDate.HasValue)
		{
			normalized.FromDate = normalized.FromDate.Value.Date;
		}

		if (normalized.ToDate.HasValue)
		{
			normalized.ToDate = normalized.ToDate.Value.Date;
		}

		return normalized;
	}

	private static List<string> BuildWarnings(LibraryReportDefinition definition, IReadOnlyCollection<Dictionary<string, string?>> rows)
	{
		var warnings = new List<string>();
		if (rows.Count == 0)
		{
			warnings.Add("No Library records matched the selected filters.");
		}

		if (definition.Id is LibraryReportTypes.PopularBooks && rows.Count == 0)
		{
			warnings.Add("Popular books require issue or reservation activity.");
		}

		return warnings;
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(x => x.Key, x => x.Value);

	private static string Date(DateTime? date) => date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
	private static string Date(DateOnly? date) => date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
	private static string RawDate(DateTime? date) => date.HasValue ? date.Value.ToString("O", CultureInfo.InvariantCulture) : string.Empty;
	private static string Money(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
	private static decimal Number(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0;
	private static string SortValue(IReadOnlyDictionary<string, string?> row, string field) => row.GetValueOrDefault(field) ?? string.Empty;
}
