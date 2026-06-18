using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportGenerator(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> copyRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<LibraryCharge, Guid> chargeRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository,
	IRepository<LibraryShelf, Guid> shelfRepository,
	IStudentRepository studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<LibraryReportOptionsDto> GetOptionsAsync()
	{
		var categories = await asyncExecuter.ToListAsync((await categoryRepository.GetQueryableAsync()).OrderBy(x => x.Name));
		var sections = await asyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync()).OrderBy(x => x.SectionName));

		return new LibraryReportOptionsDto
		{
			ReportTypes = LibraryReportCatalog.SupportedReports
				.Select(x => new LibraryReportOptionDto
				{
					Id = x.Id,
					Label = x.Id,
					Category = x.Category
				})
				.ToList(),
			ComingSoonReportTypes = LibraryReportCatalog.ComingSoonReports
				.Select(x => new LibraryReportOptionDto
				{
					Id = x.Id,
					Label = x.Id,
					Category = x.Category,
					IsComingSoon = true
				})
				.ToList(),
			Categories = categories.Select(x => new LibraryReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList(),
			Sections = sections.Select(x => new LibraryReportOptionDto { Id = x.Id.ToString(), Label = x.SectionName ?? string.Empty }).ToList(),
			CopyStatuses = LibraryReportCatalog.BuildCopyStatusOptions(),
			IssueStatuses = LibraryReportCatalog.BuildIssueStatusOptions(),
			ChargeStatuses = LibraryReportCatalog.BuildChargeStatusOptions(),
			Formats = LibraryReportCatalog.BuildFormatOptions()
		};
	}

	public async Task<LibraryReportPreviewDto> GenerateAsync(LibraryReportRequestDto input, bool exportAllRows)
	{
		var definition = LibraryReportCatalog.Resolve(input.ReportType);
		var filters = NormalizeFilters(input.Filters);
		var context = await LoadContextAsync(filters);

		var rows = definition.Id switch
		{
			LibraryReportTypes.BookMaster => BuildBookMasterRows(context, filters),
			LibraryReportTypes.StockSummary => BuildStockSummaryRows(context, filters),
			LibraryReportTypes.AvailabilityStatus => BuildAvailabilityRows(context, filters),
			LibraryReportTypes.IssueReturn => BuildIssueReturnRows(context, filters),
			LibraryReportTypes.OverdueBooks => BuildOverdueRows(context, filters),
			LibraryReportTypes.FineCollection => BuildFineRows(context, filters),
			LibraryReportTypes.LostDamagedBooks => BuildLostDamagedRows(context, filters),
			LibraryReportTypes.MemberList => BuildMemberRows(context, filters),
			LibraryReportTypes.ReservationList => BuildReservationRows(context, filters),
			LibraryReportTypes.PopularBooks => BuildPopularRows(context, filters),
			_ => []
		};

		rows = ApplySearch(rows, filters.Search);
		rows = ApplySorts(rows, input.Sorts);

		var totalCount = rows.Count;
		var page = Math.Max(input.Page, 1);
		var pageSize = exportAllRows ? Math.Max(totalCount, 1) : Math.Clamp(input.PageSize <= 0 ? 50 : input.PageSize, 1, 500);
		var pagedRows = exportAllRows
			? rows
			: rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

		return new LibraryReportPreviewDto
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
	}

	private async Task<LibraryReportContext> LoadContextAsync(LibraryReportFilterDto filters)
	{
		var bookQuery = await bookRepository.GetQueryableAsync();
		if (filters.AcademicYearId.HasValue)
		{
			bookQuery = bookQuery.Where(x => x.AcademicYearId == filters.AcademicYearId.Value);
		}

		if (filters.BookIds is { Count: > 0 })
		{
			bookQuery = bookQuery.Where(x => filters.BookIds.Contains(x.Id));
		}

		if (filters.CategoryIds is { Count: > 0 })
		{
			bookQuery = bookQuery.Where(x => filters.CategoryIds.Contains(x.CategoryId));
		}

		var books = await asyncExecuter.ToListAsync(bookQuery);
		var bookIds = books.Select(x => x.Id).ToHashSet();

		var copies = bookIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await copyRepository.GetQueryableAsync()).Where(x => bookIds.Contains(x.BookId)));
		var copyIds = copies.Select(x => x.Id).ToHashSet();
		var accessionNumbers = copies.Select(x => x.AccessionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var issues = copyIds.Count == 0 && accessionNumbers.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await issueRepository.GetQueryableAsync()).Where(x =>
				copyIds.Contains(x.BookCopyId) || accessionNumbers.Contains(x.AccessionNumber)));
		var reservations = bookIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await reservationRepository.GetQueryableAsync()).Where(x => bookIds.Contains(x.BookId)));

		var categories = await asyncExecuter.ToListAsync((await categoryRepository.GetQueryableAsync())
			.Where(x => books.Select(b => b.CategoryId).Contains(x.Id)));
		var chargeCategoriesQuery = await chargeCategoryRepository.GetQueryableAsync();
		if (filters.AcademicYearId.HasValue)
		{
			chargeCategoriesQuery = chargeCategoriesQuery.Where(x => x.AcademicYearId == filters.AcademicYearId.Value);
		}
		var chargeCategories = await asyncExecuter.ToListAsync(chargeCategoriesQuery);
		var chargeCategoryIds = chargeCategories.Select(x => x.Id).ToHashSet();
		var issueIds = issues.Select(x => x.Id).ToHashSet();

		var charges = await asyncExecuter.ToListAsync(await chargeRepository.GetQueryableAsync());
		if (filters.AcademicYearId.HasValue)
		{
			charges = charges
				.Where(x => chargeCategoryIds.Contains(x.ChargeCategoryId) ||
				            (x.BookIssueId.HasValue && issueIds.Contains(x.BookIssueId.Value)))
				.ToList();
		}

		var sections = await asyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync())
			.Where(x => copies.Select(c => c.SectionId).Contains(x.Id)));
		var shelves = await asyncExecuter.ToListAsync((await shelfRepository.GetQueryableAsync())
			.Where(x => copies.Select(c => c.ShelfId).Contains(x.Id)));

		var students = await asyncExecuter.ToListAsync((await studentRepository.GetQueryableAsync())
			.Where(x => x.AdditionalDetail != null && x.AdditionalDetail.IsLibraryMember == true));
		if (filters.AcademicYearId.HasValue)
		{
			students = students.Where(x => x.AcademicYearId == filters.AcademicYearId.Value).ToList();
		}

		var employees = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
			.Where(x => x.AdditionalDetails != null && x.AdditionalDetails.LibraryMember));

		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
		var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();
		var standards = standardIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(x => standardIds.Contains(x.Id)));
		var divisions = divisionIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(x => divisionIds.Contains(x.Id)));

		return new LibraryReportContext(
			books,
			copies,
			issues,
			reservations,
			charges,
			categories.ToDictionary(x => x.Id, x => x.Name),
			chargeCategories.ToDictionary(x => x.Id, x => x.ChargeType),
			sections.ToDictionary(x => x.Id, x => x.SectionName ?? string.Empty),
			shelves.ToDictionary(x => x.Id, x => x.ShelfName ?? string.Empty),
			BuildMemberLookup(students, employees, standards, divisions));
	}

	private static List<Dictionary<string, string?>> BuildBookMasterRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var copiesByBook = context.Copies.GroupBy(x => x.BookId).ToDictionary(x => x.Key, x => x.ToList());
		return context.Books
			.Select(book =>
			{
				var copies = copiesByBook.GetValueOrDefault(book.Id) ?? [];
				return Row(
					("bookId", book.Id.ToString()),
					("title", book.Title),
					("isbn", book.Isbn),
					("author", book.Author),
					("publisher", book.Publisher),
					("edition", book.Edition),
					("subject", book.Subject),
					("category", context.Categories.GetValueOrDefault(book.CategoryId, string.Empty)),
					("language", book.Language.ToString()),
					("price", Money(book.Price)),
					("pages", book.Pages.ToString(CultureInfo.InvariantCulture)),
					("totalCopies", copies.Count.ToString(CultureInfo.InvariantCulture)),
					("availableCopies", copies.Count(x => x.Status == BookCopyStatus.Available).ToString(CultureInfo.InvariantCulture)),
					("issuedCopies", copies.Count(x => x.Status == BookCopyStatus.Issued).ToString(CultureInfo.InvariantCulture)),
					("reservedCopies", copies.Count(x => x.Status == BookCopyStatus.Reserved).ToString(CultureInfo.InvariantCulture)),
					("lostCopies", copies.Count(x => x.Status == BookCopyStatus.Lost).ToString(CultureInfo.InvariantCulture)),
					("damagedCopies", copies.Count(x => x.Status == BookCopyStatus.Damaged).ToString(CultureInfo.InvariantCulture)));
			})
			.ToList();
	}

	private static List<Dictionary<string, string?>> BuildStockSummaryRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Books
			.GroupBy(x => x.CategoryId)
			.Select(group =>
			{
				var bookIds = group.Select(x => x.Id).ToHashSet();
				var copies = context.Copies.Where(x => bookIds.Contains(x.BookId)).ToList();
				return Row(
					("category", context.Categories.GetValueOrDefault(group.Key, "Uncategorized")),
					("bookCount", group.Count().ToString(CultureInfo.InvariantCulture)),
					("totalCopies", copies.Count.ToString(CultureInfo.InvariantCulture)),
					("availableCopies", copies.Count(x => x.Status == BookCopyStatus.Available).ToString(CultureInfo.InvariantCulture)),
					("issuedCopies", copies.Count(x => x.Status == BookCopyStatus.Issued).ToString(CultureInfo.InvariantCulture)),
					("reservedCopies", copies.Count(x => x.Status == BookCopyStatus.Reserved).ToString(CultureInfo.InvariantCulture)),
					("lostCopies", copies.Count(x => x.Status == BookCopyStatus.Lost).ToString(CultureInfo.InvariantCulture)),
					("damagedCopies", copies.Count(x => x.Status == BookCopyStatus.Damaged).ToString(CultureInfo.InvariantCulture)));
			})
			.OrderBy(x => x["category"])
			.ToList();

	private static List<Dictionary<string, string?>> BuildAvailabilityRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Copies
			.Where(copy => filters.SectionIds is not { Count: > 0 } || filters.SectionIds.Contains(copy.SectionId))
			.Where(copy => filters.CopyStatuses is not { Count: > 0 } || filters.CopyStatuses.Contains((int)copy.Status))
			.Select(copy =>
			{
				var book = context.BookById.GetValueOrDefault(copy.BookId);
				return Row(
					("accessionNumber", copy.AccessionNumber),
					("copyNumber", copy.CopyNumber.ToString(CultureInfo.InvariantCulture)),
					("title", book?.Title),
					("author", book?.Author),
					("category", book == null ? null : context.Categories.GetValueOrDefault(book.CategoryId)),
					("status", copy.Status.ToString()),
					("section", context.Sections.GetValueOrDefault(copy.SectionId)),
					("shelf", context.Shelves.GetValueOrDefault(copy.ShelfId)),
					("remarks", copy.Remarks));
			})
			.ToList();

	private static List<Dictionary<string, string?>> BuildIssueReturnRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		FilterIssues(context.Issues, filters, issue => issue.IssueDate)
			.Select(issue => BuildIssueRow(context, issue))
			.ToList();

	private static List<Dictionary<string, string?>> BuildOverdueRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var today = DateTime.UtcNow.Date;
		return FilterIssues(context.Issues, filters, issue => issue.DueDate)
			.Where(issue => issue.Status == BookIssueStatus.Issued && issue.DueDate.Date < today)
			.Select(issue =>
			{
				var row = BuildIssueRow(context, issue);
				row["overdueDays"] = Math.Max(0, (today - issue.DueDate.Date).Days).ToString(CultureInfo.InvariantCulture);
				return row;
			})
			.ToList();
	}

	private static List<Dictionary<string, string?>> BuildFineRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Charges
			.Where(charge => filters.MemberIds is not { Count: > 0 } || filters.MemberIds.Contains(charge.MemberId))
			.Where(charge => !filters.MemberRole.HasValue || charge.MemberRole == filters.MemberRole.Value)
			.Where(charge => filters.ChargeStatuses is not { Count: > 0 } || filters.ChargeStatuses.Contains((int)charge.PaymentStatus))
			.Where(charge => DateInRange(charge.ChargeDate, filters))
			.Select(charge =>
			{
				var member = context.Members.GetValueOrDefault(charge.MemberId);
				var issue = charge.BookIssueId.HasValue ? context.IssueById.GetValueOrDefault(charge.BookIssueId.Value) : null;
				var book = issue == null ? null : context.BookByAccession.GetValueOrDefault(issue.AccessionNumber);
				return Row(
					("chargeDate", Date(charge.ChargeDate)),
					("dueDate", Date(charge.DueDate)),
					("memberName", member?.Name),
					("memberIdentifier", member?.Identifier),
					("memberRole", member?.Role),
					("chargeType", context.ChargeCategories.GetValueOrDefault(charge.ChargeCategoryId)),
					("amount", Money(charge.Amount)),
					("paymentStatus", charge.PaymentStatus.ToString()),
					("paidDate", Date(charge.PaidDate)),
					("bookTitle", book?.Title),
					("accessionNumber", issue?.AccessionNumber),
					("remarks", charge.Remarks));
			})
			.ToList();

	private static List<Dictionary<string, string?>> BuildLostDamagedRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		BuildAvailabilityRows(context, new LibraryReportFilterDto
		{
			SectionIds = filters.SectionIds,
			CopyStatuses = filters.CopyStatuses is { Count: > 0 }
				? filters.CopyStatuses
				: [(int)BookCopyStatus.Lost, (int)BookCopyStatus.Damaged],
			Search = filters.Search
		})
			.Where(row => row.GetValueOrDefault("status") is nameof(BookCopyStatus.Lost) or nameof(BookCopyStatus.Damaged))
			.ToList();

	private static List<Dictionary<string, string?>> BuildMemberRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Members.Values
			.Where(member => filters.MemberIds is not { Count: > 0 } || filters.MemberIds.Contains(member.Id))
			.Where(member => !filters.MemberRole.HasValue || member.RoleValue == filters.MemberRole.Value)
			.Select(member =>
			{
				var activeIssues = context.Issues.Count(x => x.MemberId == member.Id && x.Status == BookIssueStatus.Issued);
				var overdueIssues = context.Issues.Count(x => x.MemberId == member.Id && x.Status == BookIssueStatus.Issued && x.DueDate.Date < DateTime.UtcNow.Date);
				var reservations = context.Reservations.Count(x => x.MemberId == member.Id && x.ReserveDueDate.Date >= DateTime.UtcNow.Date);
				var pendingCharges = context.Charges
					.Where(x => x.MemberId == member.Id && x.PaymentStatus == ChargePaymentStatus.Pending)
					.Sum(x => x.Amount);

				return Row(
					("memberName", member.Name),
					("memberIdentifier", member.Identifier),
					("memberRole", member.Role),
					("email", member.Email),
					("phone", member.Phone),
					("classDivision", member.ClassDivision),
					("joinDate", Date(member.JoinDate)),
					("activeIssues", activeIssues.ToString(CultureInfo.InvariantCulture)),
					("overdueIssues", overdueIssues.ToString(CultureInfo.InvariantCulture)),
					("reservationCount", reservations.ToString(CultureInfo.InvariantCulture)),
					("pendingCharges", Money(pendingCharges)));
			})
			.OrderBy(x => x["memberName"])
			.ToList();

	private static List<Dictionary<string, string?>> BuildReservationRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Reservations
			.Where(reservation => filters.MemberIds is not { Count: > 0 } || filters.MemberIds.Contains(reservation.MemberId))
			.Where(reservation => DateInRange(reservation.RequestedDate, filters))
			.Select(reservation =>
			{
				var book = context.BookById.GetValueOrDefault(reservation.BookId);
				var member = context.Members.GetValueOrDefault(reservation.MemberId);
				var status = reservation.ReserveDueDate.Date < DateTime.UtcNow.Date ? "Expired" : "Active";
				return Row(
					("requestedDate", Date(reservation.RequestedDate)),
					("reserveDueDate", Date(reservation.ReserveDueDate)),
					("title", book?.Title),
					("memberName", member?.Name),
					("memberIdentifier", member?.Identifier),
					("memberRole", member?.Role),
					("status", status));
			})
			.ToList();

	private static List<Dictionary<string, string?>> BuildPopularRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var copyBookById = context.Copies.ToDictionary(x => x.Id, x => x.BookId);
		var top = Math.Clamp(filters.Top ?? 10, 1, 100);
		return context.Books
			.Select(book =>
			{
				var issueCount = context.Issues.Count(issue => copyBookById.GetValueOrDefault(issue.BookCopyId) == book.Id);
				return Row(
					("title", book.Title),
					("author", book.Author),
					("category", context.Categories.GetValueOrDefault(book.CategoryId)),
					("issueCount", issueCount.ToString(CultureInfo.InvariantCulture)),
					("activeIssues", context.Issues.Count(issue => issue.Status == BookIssueStatus.Issued && copyBookById.GetValueOrDefault(issue.BookCopyId) == book.Id).ToString(CultureInfo.InvariantCulture)),
					("reservationCount", context.Reservations.Count(x => x.BookId == book.Id).ToString(CultureInfo.InvariantCulture)));
			})
			.OrderByDescending(x => Number(x.GetValueOrDefault("issueCount")))
			.ThenByDescending(x => Number(x.GetValueOrDefault("reservationCount")))
			.Take(top)
			.ToList();
	}

	private static Dictionary<string, string?> BuildIssueRow(LibraryReportContext context, BookIssue issue)
	{
		var book = context.BookByAccession.GetValueOrDefault(issue.AccessionNumber);
		var member = context.Members.GetValueOrDefault(issue.MemberId);
		return Row(
			("accessionNumber", issue.AccessionNumber),
			("title", book?.Title),
			("memberName", member?.Name),
			("memberIdentifier", member?.Identifier),
			("memberRole", member?.Role ?? (issue.MemberRole == 0 ? "Student" : "Staff")),
			("issueDate", Date(issue.IssueDate)),
			("dueDate", Date(issue.DueDate)),
			("returnDate", Date(issue.ReturnDate)),
			("status", issue.Status.ToString()),
			("amount", issue.Amount.HasValue ? Money(issue.Amount.Value) : null));
	}

	private static IEnumerable<BookIssue> FilterIssues(
		IEnumerable<BookIssue> issues,
		LibraryReportFilterDto filters,
		Func<BookIssue, DateTime> dateSelector) =>
		issues
			.Where(issue => filters.MemberIds is not { Count: > 0 } || filters.MemberIds.Contains(issue.MemberId))
			.Where(issue => !filters.MemberRole.HasValue || issue.MemberRole == filters.MemberRole.Value)
			.Where(issue => filters.IssueStatuses is not { Count: > 0 } || filters.IssueStatuses.Contains((int)issue.Status))
			.Where(issue => DateInRange(dateSelector(issue), filters));

	private static Dictionary<Guid, LibraryMemberReportInfo> BuildMemberLookup(
		IReadOnlyCollection<Student> students,
		IReadOnlyCollection<Employee> employees,
		IReadOnlyCollection<Standard> standards,
		IReadOnlyCollection<Division> divisions)
	{
		var standardLookup = standards.ToDictionary(x => x.Id, x => x.Name);
		var divisionLookup = divisions.ToDictionary(x => x.Id, x => x.Name);
		var members = new Dictionary<Guid, LibraryMemberReportInfo>();

		foreach (var student in students)
		{
			var standard = student.StandardId.HasValue ? standardLookup.GetValueOrDefault(student.StandardId.Value) : null;
			var division = student.DivisionId.HasValue ? divisionLookup.GetValueOrDefault(student.DivisionId.Value) : null;
			var classDivision = string.Join("-", new[] { standard, division }.Where(x => !string.IsNullOrWhiteSpace(x)));
			members[student.Id] = new LibraryMemberReportInfo(
				student.Id,
				0,
				"Student",
				string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
				student.GrNo?.ToString(CultureInfo.InvariantCulture) ?? student.Id.ToString("N")[..8],
				student.ContactDetail?.Email,
				student.ContactDetail?.PrimaryContactNo,
				classDivision,
				student.CreationTime);
		}

		foreach (var employee in employees)
		{
			members[employee.Id] = new LibraryMemberReportInfo(
				employee.Id,
				1,
				employee.EmployeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Staff",
				employee.EmployeeName,
				employee.EmployeeCode,
				employee.Email,
				employee.MobileNo,
				employee.Department,
				employee.CreationTime);
		}

		return members;
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
			ordered = ordered == null
				? descending
					? rows.OrderByDescending(row => SortValue(row, sort.Field))
					: rows.OrderBy(row => SortValue(row, sort.Field))
				: descending
					? ordered.ThenByDescending(row => SortValue(row, sort.Field))
					: ordered.ThenBy(row => SortValue(row, sort.Field));
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

	private static bool DateInRange(DateTime date, LibraryReportFilterDto filters)
	{
		var current = date.Date;
		return (!filters.FromDate.HasValue || current >= filters.FromDate.Value.Date) &&
		       (!filters.ToDate.HasValue || current <= filters.ToDate.Value.Date);
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(x => x.Key, x => x.Value);

	private static string Date(DateTime? date) => date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
	private static string Money(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
	private static decimal Number(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0;
	private static string SortValue(IReadOnlyDictionary<string, string?> row, string field) => row.GetValueOrDefault(field) ?? string.Empty;
}

internal sealed record LibraryReportContext(
	List<Book> Books,
	List<BookCopy> Copies,
	List<BookIssue> Issues,
	List<BookReservation> Reservations,
	List<LibraryCharge> Charges,
	Dictionary<Guid, string> Categories,
	Dictionary<Guid, string> ChargeCategories,
	Dictionary<Guid, string> Sections,
	Dictionary<Guid, string> Shelves,
	Dictionary<Guid, LibraryMemberReportInfo> Members)
{
	public Dictionary<Guid, Book> BookById { get; } = Books.ToDictionary(x => x.Id);
	public Dictionary<Guid, BookIssue> IssueById { get; } = Issues.ToDictionary(x => x.Id);
	public Dictionary<string, Book> BookByAccession { get; } = Copies
		.Where(x => !string.IsNullOrWhiteSpace(x.AccessionNumber))
		.GroupBy(x => x.AccessionNumber, StringComparer.OrdinalIgnoreCase)
		.ToDictionary(x => x.Key, x => Books.FirstOrDefault(book => book.Id == x.First().BookId)!, StringComparer.OrdinalIgnoreCase);
}

internal sealed record LibraryMemberReportInfo(
	Guid Id,
	int RoleValue,
	string Role,
	string Name,
	string Identifier,
	string? Email,
	string? Phone,
	string? ClassDivision,
	DateTime JoinDate);
