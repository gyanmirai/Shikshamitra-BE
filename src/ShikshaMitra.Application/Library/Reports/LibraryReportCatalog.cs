using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.Reports;

internal static class LibraryReportCatalog
{
	private static readonly List<LibraryReportDefinition> Supported =
	[
		new(LibraryReportTypes.BookMaster, "Book & Inventory", "Complete book master with copy counts",
			["title", "isbn", "author", "publisher", "edition", "subject", "category", "language", "price", "pages", "totalCopies", "availableCopies", "issuedCopies", "reservedCopies", "lostCopies", "damagedCopies", "inCustodyCopies", "inTransitCopies"]),
		new(LibraryReportTypes.StockSummary, "Book & Inventory", "Stock totals grouped by category",
			["category", "bookCount", "totalCopies", "availableCopies", "issuedCopies", "reservedCopies", "lostCopies", "damagedCopies", "inCustodyCopies", "inTransitCopies"]),
		new(LibraryReportTypes.AvailabilityStatus, "Book & Inventory", "Copy-level availability and location",
			["accessionNumber", "copyNumber", "title", "author", "category", "status", "section", "shelf", "remarks"]),
		new(LibraryReportTypes.IssueReturn, "Circulation & Usage", "Issue and return movement history",
			["accessionNumber", "title", "memberName", "memberIdentifier", "memberRole", "schoolIssueDate", "schoolDueDate", "schoolReturnDate", "dateEvidence", "status", "amount"],
			["issueDate", "dueDate", "returnDate", "rawIssueDate", "rawDueDate", "rawReturnDate", "issueDateProvenance", "dueDateProvenance", "returnDateProvenance", "academicYearId", "memberRecordId", "memberDisplaySource", "copyId", "copyLinkSource"]),
		new(LibraryReportTypes.OverdueBooks, "Circulation & Usage", "Currently overdue books",
			["accessionNumber", "title", "memberName", "memberIdentifier", "memberRole", "schoolIssueDate", "schoolDueDate", "dateEvidence", "overdueDays"],
			["issueDate", "dueDate", "rawIssueDate", "rawDueDate", "issueDateProvenance", "dueDateProvenance", "academicYearId", "memberRecordId", "memberDisplaySource", "copyId", "copyLinkSource"]),
		new(LibraryReportTypes.FineCollection, "Financial", "Library assessments and verified Finance balances",
			["schoolChargeDate", "schoolDueDate", "dateEvidence", "memberName", "chargeType", "amount", "paymentStatus",
			 "financeEvidence", "currency", "settledAmountText", "pendingVerificationAmountText", "financialOutstandingAmountText", "collectibleAmountText"],
			["chargeDate", "dueDate", "paidDate", "memberIdentifier", "memberRole", "bookTitle", "accessionNumber", "remarks",
			 "originalAmountText", "acceptedAmountText", "netWaivedAmountText", "refundedAmountText",
			 "academicYearId", "memberDisplaySource", "recordedMemberRole"]),
		new(LibraryReportTypes.LostDamagedBooks, "Circulation & Usage", "Lost and damaged copy register",
			["accessionNumber", "copyNumber", "title", "author", "category", "status", "section", "shelf", "remarks"]),
		new(LibraryReportTypes.MemberList, "Member Management", "Students and staff with Library membership",
			["memberName", "memberIdentifier", "memberRole", "email", "phone", "classDivision", "joinDate", "activeIssues", "overdueIssues", "reservationCount",
			 "financeEvidence", "currency", "collectibleAmountText"], ["pendingCharges"]),
		new(LibraryReportTypes.ReservationList, "Reservations", "Book reservation register",
			["schoolRequestedDate", "schoolRequestExpiryDate", "dateEvidence", "title", "memberName", "memberIdentifier", "memberRole", "status"],
			["requestedDate", "reserveDueDate", "rawRequestedDate", "rawReserveDueDate", "requestedDateProvenance", "requestExpiryDateProvenance", "academicYearId", "memberRecordId", "memberDisplaySource", "state"]),
		new(LibraryReportTypes.PopularBooks, "Popular Books", "Most issued and reserved books",
			["title", "author", "category", "issueCount", "activeIssues", "reservationCount"])
	];

	private static readonly List<LibraryReportDefinition> ComingSoon =
	[
		new("Acquisition Register Report", "Book & Inventory", "Requires acquisition domain", []),
		new("Vendor-wise Purchase Report", "Book & Inventory", "Requires vendor purchase domain", []),
		new("Old Books Write-off Report", "Book & Inventory", "Requires write-off workflow", []),
		new("Library Audit Report", "Administrative", "Requires stock audit domain", []),
		new("Membership Expiry Report", "Member Management", "Requires membership expiry domain", []),
		new("Blacklisted / Blocked Members Report", "Member Management", "Requires member block workflow", [])
	];

	public static IReadOnlyCollection<LibraryReportDefinition> SupportedReports => Supported;
	public static IReadOnlyCollection<LibraryReportDefinition> ComingSoonReports => ComingSoon;

	public static LibraryReportDefinition Resolve(string reportType)
	{
		var definition = Supported.FirstOrDefault(x => string.Equals(x.Id, reportType, StringComparison.OrdinalIgnoreCase));
		if (definition == null)
		{
			throw new Volo.Abp.BusinessException(
				code: LibraryErrorCodes.ReportTypeNotSupported,
				message: $"Library report type '{reportType}' is not supported in this phase.");
		}

		return definition;
	}

	public static bool IsSupported(string reportType) =>
		Supported.Any(x => string.Equals(x.Id, reportType, StringComparison.OrdinalIgnoreCase));

	public static List<LibraryReportColumnDto> ResolveColumns(
		LibraryReportDefinition definition,
		IReadOnlyCollection<string>? requestedColumns,
		IReadOnlyCollection<Dictionary<string, string?>> rows)
	{
		var requested = requestedColumns is { Count: > 0 }
			? requestedColumns
			: definition.DefaultColumns;

		return requested
			.Where(id => rows.Count == 0 || rows.First().ContainsKey(id) || definition.SupportedColumns.Contains(id, StringComparer.OrdinalIgnoreCase))
			.Select(id => new LibraryReportColumnDto
			{
				Id = id,
				Label = ColumnLabel(definition.Id, id)
			})
			.ToList();
	}

	public static List<LibraryReportOptionDto> BuildFormatOptions() =>
	[
		new() { Id = LibraryReportFormat.Excel, Label = "Excel" },
		new() { Id = LibraryReportFormat.Pdf, Label = "PDF" }
	];

	public static List<LibraryReportOptionDto> BuildCopyStatusOptions() =>
		Enum.GetValues<BookCopyStatus>()
			.Select(x => new LibraryReportOptionDto { Id = ((int)x).ToString(), Label = CopyStatusLabel(x) })
			.ToList();

	internal static string CopyStatusLabel(BookCopyStatus status) => status switch
	{
		BookCopyStatus.InCustody => "Classroom custody",
		BookCopyStatus.InTransit => "In transit",
		_ => status.ToString()
	};

	public static List<LibraryReportOptionDto> BuildIssueStatusOptions() =>
		Enum.GetValues<BookIssueStatus>()
			.Select(x => new LibraryReportOptionDto { Id = ((int)x).ToString(), Label = x.ToString() })
			.ToList();

	public static List<LibraryReportOptionDto> BuildChargeStatusOptions() =>
		Enum.GetValues<ChargePaymentStatus>()
			.Select(x => new LibraryReportOptionDto { Id = ((int)x).ToString(), Label = x.ToString() })
			.ToList();

	private static string Humanize(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return string.Empty;
		}

		var chars = id.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString());
		var value = string.Concat(chars);
		return char.ToUpperInvariant(value[0]) + value[1..];
	}

	private static string ColumnLabel(string reportType, string id) => (reportType, id) switch
	{
		(LibraryReportTypes.FineCollection, "amount") => "Original assessment amount",
		(LibraryReportTypes.FineCollection, "paymentStatus") => "Recorded local status",
		(LibraryReportTypes.FineCollection, "paidDate") => "Recorded local paid date",
		(LibraryReportTypes.MemberList, "pendingCharges") => "Recorded pending assessments",
		(LibraryReportTypes.MemberList, "collectibleAmountText") => "Verified collectible",
		(_, "chargeDate") => "Legacy charge date component",
		(_, "schoolChargeDate") => "Assessed on (school date)",
		(_, "financeEvidence") => "Finance evidence",
		(_, "originalAmountText") => "Verified original amount",
		(_, "acceptedAmountText") => "Accepted amount",
		(_, "settledAmountText") => "Settled",
		(_, "pendingVerificationAmountText") => "Pending verification",
		(_, "netWaivedAmountText") => "Net waived",
		(_, "financialOutstandingAmountText") => "Financial outstanding",
		(_, "collectibleAmountText") => "Collectible",
		(_, "refundedAmountText") => "Refunded",
		(_, "issueDate") => "Legacy issue date component",
		(_, "dueDate") => "Legacy due date component",
		(_, "returnDate") => "Legacy return date component",
		(_, "requestedDate") => "Legacy requested date component",
		(_, "reserveDueDate") => "Legacy expiry date component",
		(_, "schoolIssueDate") => "Issued on (school date)",
		(_, "schoolDueDate") => "Due on (school date)",
		(_, "schoolReturnDate") => "Returned on (school date)",
		(_, "schoolRequestedDate") => "Requested on (school date)",
		(_, "schoolRequestExpiryDate") => "Request expiry (school date)",
		(_, "joinDate") => "Profile created on",
		(_, "dateEvidence") => "Date evidence",
		_ => Humanize(id)
	};
}

internal sealed record LibraryReportDefinition(
	string Id,
	string Category,
	string Description,
	List<string> DefaultColumns,
	List<string>? CompatibilityColumns = null)
{
	public IReadOnlyCollection<string> SupportedColumns => CompatibilityColumns == null ? DefaultColumns :
		DefaultColumns.Concat(CompatibilityColumns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
