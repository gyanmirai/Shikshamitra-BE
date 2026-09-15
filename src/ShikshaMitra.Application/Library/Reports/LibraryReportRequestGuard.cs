using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportRequestGuard : ITransientDependency
{
	private const int MaxListItems = 100;

	public void ValidateAndNormalize(LibraryReportRequestDto request)
	{
		if (request == null)
		{
			throw Invalid("Request", "A report request is required.");
		}

		var definition = LibraryReportCatalog.Resolve(request.ReportType?.Trim() ?? string.Empty);
		request.ReportType = definition.Id;
		request.Format = NormalizeFormat(request.Format);
		request.Page = Math.Max(request.Page, 1);
		request.PageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 500);
		request.Filters ??= new LibraryReportFilterDto();
		NormalizeFilters(request.Filters);
		ValidateColumnsAndSorts(request, definition);
		EnsureOnlySupportedFilters(request.ReportType, request.Filters);
	}

	private static void NormalizeFilters(LibraryReportFilterDto filters)
	{
		filters.BookIds = NormalizeIds(filters.BookIds, "BookIds");
		filters.CategoryIds = NormalizeIds(filters.CategoryIds, "CategoryIds");
		filters.MemberIds = NormalizeIds(filters.MemberIds, "MemberIds");
		filters.SectionIds = NormalizeIds(filters.SectionIds, "SectionIds");
		filters.CopyStatuses = NormalizeIntegers(filters.CopyStatuses, "CopyStatuses");
		filters.IssueStatuses = NormalizeIntegers(filters.IssueStatuses, "IssueStatuses");
		filters.ChargeStatuses = NormalizeIntegers(filters.ChargeStatuses, "ChargeStatuses");
		filters.Search = string.IsNullOrWhiteSpace(filters.Search) ? null : filters.Search.Trim();

		if (filters.Search?.Length > 200)
		{
			throw Invalid("Search", "Search text cannot exceed 200 characters.");
		}

		if (filters.FromDate.HasValue) filters.FromDate = filters.FromDate.Value.Date;
		if (filters.ToDate.HasValue) filters.ToDate = filters.ToDate.Value.Date;
		if (filters.FromDate > filters.ToDate)
		{
			throw Invalid("DateRange", "From date cannot be after to date.");
		}

		if (filters.Top.HasValue && filters.Top is < 1 or > 100)
		{
			throw Invalid("Top", "Top must be between 1 and 100.");
		}

		if (filters.MemberRole.HasValue && filters.MemberRole is not 0 and not 1)
		{
			throw Invalid("MemberRole", "Member role must be Student or Staff.");
		}

		EnsureEnumValues<BookCopyStatus>(filters.CopyStatuses, "CopyStatuses");
		EnsureEnumValues<BookIssueStatus>(filters.IssueStatuses, "IssueStatuses");
		EnsureEnumValues<ChargePaymentStatus>(filters.ChargeStatuses, "ChargeStatuses");
	}

	private static void ValidateColumnsAndSorts(LibraryReportRequestDto request, LibraryReportDefinition definition)
	{
		if (request.Columns is { Count: > 0 })
		{
			request.Columns = request.Columns
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			if (request.Columns.Count > 30 || request.Columns.Any(column => !definition.DefaultColumns.Contains(column, StringComparer.OrdinalIgnoreCase)))
			{
				throw Invalid("Columns", "One or more columns are not supported by this report.");
			}
		}

		if (request.Sorts is not { Count: > 0 }) return;
		if (request.Sorts.Count > 5)
		{
			throw Invalid("Sorts", "At most five sort fields are supported.");
		}

		foreach (var sort in request.Sorts)
		{
			sort.Field = sort.Field?.Trim() ?? string.Empty;
			sort.Direction = sort.Direction?.Trim().ToLowerInvariant() ?? "asc";
			if (!definition.DefaultColumns.Contains(sort.Field, StringComparer.OrdinalIgnoreCase) || sort.Direction is not "asc" and not "desc")
			{
				throw Invalid("Sorts", "One or more sort fields or directions are invalid.");
			}
		}
	}

	private static void EnsureOnlySupportedFilters(string reportType, LibraryReportFilterDto filters)
	{
		var allowed = reportType switch
		{
			LibraryReportTypes.BookMaster => Set("year", "books", "categories", "search"),
			LibraryReportTypes.StockSummary => Set("year", "books", "categories", "search"),
			LibraryReportTypes.AvailabilityStatus => Set("year", "books", "categories", "sections", "copyStatuses", "search"),
			LibraryReportTypes.IssueReturn => Set("year", "books", "categories", "members", "issueStatuses", "memberRole", "dates", "search"),
			LibraryReportTypes.OverdueBooks => Set("year", "books", "categories", "members", "issueStatuses", "memberRole", "dates", "search"),
			LibraryReportTypes.FineCollection => Set("year", "members", "chargeStatuses", "memberRole", "dates", "search"),
			LibraryReportTypes.LostDamagedBooks => Set("year", "books", "categories", "sections", "copyStatuses", "search"),
			LibraryReportTypes.MemberList => Set("year", "members", "memberRole", "search"),
			LibraryReportTypes.ReservationList => Set("year", "books", "categories", "members", "memberRole", "dates", "search"),
			LibraryReportTypes.PopularBooks => Set("year", "books", "categories", "top", "search"),
			_ => []
		};
		var used = new Dictionary<string, bool>
		{
			["year"] = filters.AcademicYearId.HasValue,
			["books"] = filters.BookIds?.Count > 0,
			["categories"] = filters.CategoryIds?.Count > 0,
			["members"] = filters.MemberIds?.Count > 0,
			["sections"] = filters.SectionIds?.Count > 0,
			["copyStatuses"] = filters.CopyStatuses?.Count > 0,
			["issueStatuses"] = filters.IssueStatuses?.Count > 0,
			["chargeStatuses"] = filters.ChargeStatuses?.Count > 0,
			["memberRole"] = filters.MemberRole.HasValue,
			["dates"] = filters.FromDate.HasValue || filters.ToDate.HasValue,
			["search"] = filters.Search != null,
			["top"] = filters.Top.HasValue
		};
		if (used.Any(item => item.Value && !allowed.Contains(item.Key)))
		{
			throw Invalid("Filters", "One or more filters are not supported by this report.");
		}
	}

	private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

	private static List<Guid>? NormalizeIds(List<Guid>? values, string field)
	{
		if (values == null) return null;
		if (values.Count > MaxListItems || values.Any(value => value == Guid.Empty))
		{
			throw Invalid(field, $"{field} contains too many or invalid identifiers.");
		}
		var normalized = values.Distinct().ToList();
		return normalized.Count == 0 ? null : normalized;
	}

	private static List<int>? NormalizeIntegers(List<int>? values, string field)
	{
		if (values == null) return null;
		if (values.Count > MaxListItems) throw Invalid(field, $"{field} contains too many values.");
		var normalized = values.Distinct().ToList();
		return normalized.Count == 0 ? null : normalized;
	}

	private static void EnsureEnumValues<TEnum>(IEnumerable<int>? values, string field) where TEnum : struct, Enum
	{
		if (values?.Any(value => !Enum.IsDefined(typeof(TEnum), value)) == true)
		{
			throw Invalid(field, $"{field} contains an unsupported status.");
		}
	}

	private static string NormalizeFormat(string? format) => format?.Trim().ToLowerInvariant() switch
	{
		LibraryReportFormat.Pdf => LibraryReportFormat.Pdf,
		LibraryReportFormat.Excel => LibraryReportFormat.Excel,
		_ => throw Invalid("Format", "Format must be PDF or Excel.")
	};

	private static BusinessException Invalid(string field, string message) =>
		new BusinessException(LibraryErrorCodes.ReportRequestInvalid, message).WithData("Field", field);
}
