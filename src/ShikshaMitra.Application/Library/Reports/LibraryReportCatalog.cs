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
			["title", "isbn", "author", "publisher", "edition", "subject", "category", "language", "price", "pages", "totalCopies", "availableCopies", "issuedCopies", "reservedCopies", "lostCopies", "damagedCopies"]),
		new(LibraryReportTypes.StockSummary, "Book & Inventory", "Stock totals grouped by category",
			["category", "bookCount", "totalCopies", "availableCopies", "issuedCopies", "reservedCopies", "lostCopies", "damagedCopies"]),
		new(LibraryReportTypes.AvailabilityStatus, "Book & Inventory", "Copy-level availability and location",
			["accessionNumber", "copyNumber", "title", "author", "category", "status", "section", "shelf", "remarks"]),
		new(LibraryReportTypes.IssueReturn, "Circulation & Usage", "Issue and return movement history",
			["accessionNumber", "title", "memberName", "memberIdentifier", "memberRole", "issueDate", "dueDate", "returnDate", "status", "amount"]),
		new(LibraryReportTypes.OverdueBooks, "Circulation & Usage", "Currently overdue books",
			["accessionNumber", "title", "memberName", "memberIdentifier", "memberRole", "issueDate", "dueDate", "overdueDays"]),
		new(LibraryReportTypes.FineCollection, "Financial", "Library charges and collection status",
			["chargeDate", "dueDate", "memberName", "memberIdentifier", "memberRole", "chargeType", "amount", "paymentStatus", "paidDate", "bookTitle", "accessionNumber", "remarks"]),
		new(LibraryReportTypes.LostDamagedBooks, "Circulation & Usage", "Lost and damaged copy register",
			["accessionNumber", "copyNumber", "title", "author", "category", "status", "section", "shelf", "remarks"]),
		new(LibraryReportTypes.MemberList, "Member Management", "Students and staff with Library membership",
			["memberName", "memberIdentifier", "memberRole", "email", "phone", "classDivision", "joinDate", "activeIssues", "overdueIssues", "reservationCount", "pendingCharges"]),
		new(LibraryReportTypes.ReservationList, "Reservations", "Book reservation register",
			["requestedDate", "reserveDueDate", "title", "memberName", "memberIdentifier", "memberRole", "status"]),
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
			.Where(id => rows.Count == 0 || rows.First().ContainsKey(id) || definition.DefaultColumns.Contains(id, StringComparer.OrdinalIgnoreCase))
			.Select(id => new LibraryReportColumnDto
			{
				Id = id,
				Label = Humanize(id)
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
			.Select(x => new LibraryReportOptionDto { Id = ((int)x).ToString(), Label = x.ToString() })
			.ToList();

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
}

internal sealed record LibraryReportDefinition(
	string Id,
	string Category,
	string Description,
	List<string> DefaultColumns);
