using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Dashboard.View)]
[RemoteService(false)]
public partial class LibraryDashboardAppService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	LibraryStoredLoanScope loanScope,
	LibraryStoredReservationScope requestScope,
	IRepository<LibraryHold, Guid> holdRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<LibraryCharge, Guid> chargeRepository,
	ILibraryChargePositionReader chargePositionReader,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository,
	ILibraryAccessService libraryAccess,
	LibraryCalendarService calendar,
	IClock clock)
	: ApplicationService, ILibraryDashboardAppService
{
	public async Task<LibraryDashboardOverviewDto> GetOverviewAsync(Guid? academicYearId = null)
	{
		var positions = await chargePositionReader.GetQueryAsync(new(LibraryChargePositionReadPurpose.Dashboard, academicYearId));
		var executionScope = await libraryAccess.GetContextAsync(academicYearId);
		var day = await LibraryReadDateEvidence.ReadAsync(calendar, clock);
		var today = day.Day;
		var weekStart = today?.AddDays(-6);

		var inventoryBooks = await AsyncExecuter.ToListAsync((await bookRepository.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id));
		var inventoryCopies = await GetBookCopiesAsync(inventoryBooks.Select(x => x.Id).ToHashSet());

		var issues = await GetIssuesAsync(executionScope, academicYearId);
		var reservations = await GetReservationsAsync(executionScope, academicYearId);
		var holds = await GetCollectableHoldsAsync(reservations);
		var charges = await ReadDashboardChargesAsync(positions, today);
		var categories = await GetCategoriesAsync(inventoryBooks.Select(x => x.CategoryId).Distinct());
		var sections = await GetSectionsAsync(inventoryCopies.Select(x => x.SectionId).Distinct());

		var overview = new LibraryDashboardOverviewDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = clock.Now,
			SchoolAsOfDate = day.Day,
			SchoolDateSource = day.Source,
			SchoolDateUnavailableReason = day.UnavailableReason,
			UnknownIssueDateCount = LibraryReadDateEvidence.UnknownIssueDates(issues),
			UnknownReturnDateCount = LibraryReadDateEvidence.UnknownReturnDates(issues),
			StockTotals = BuildStockTotals(inventoryBooks, inventoryCopies),
			Circulation = BuildCirculationTotals(issues, today, weekStart),
			Charges = charges.Compatibility,
			FinancePosition = charges.Finance,
			Reservations = BuildReservationTotals(reservations, holds, today),
			WeeklyCirculation = BuildWeeklyCirculation(issues, weekStart, today),
			CategoryDistribution = BuildCategoryDistribution(inventoryBooks, inventoryCopies, categories),
			SectionInventory = BuildSectionInventory(inventoryCopies, sections),
			PopularBooks = BuildPopularBooks(inventoryBooks, inventoryCopies, issues, reservations, categories),
		};
		var unknownDue = LibraryReadDateEvidence.UnknownDueDates(issues);
		overview.Circulation.UnknownDueDateCount = unknownDue;
		overview.Circulation.OverdueAsOfDate = day.Day;
		overview.Circulation.OverdueDateSource = day.Source;
		overview.Circulation.OverdueClassificationAvailable = day.Day.HasValue && unknownDue == 0;
		overview.Circulation.OverdueUnavailableReason = LibraryReadDateEvidence.OverdueReason(day, unknownDue);

		overview.Utilization = BuildUtilization(overview);
		overview.Alerts = BuildAlerts(overview, charges.CollectibleCount, charges.OverdueCount);
		return overview;
	}

	private async Task<List<BookCopy>> GetBookCopiesAsync(IReadOnlySet<Guid> bookIds)
	{
		if (bookIds.Count == 0)
		{
			return [];
		}

		var query = await bookCopyRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => bookIds.Contains(x.BookId)));
	}

	private async Task<List<BookIssue>> GetIssuesAsync(LibraryAcademicContext context, Guid? academicYearId)
	{
		var query = await loanScope.GetReadableLoansAsync(context.UserScope);
		if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
		return await AsyncExecuter.ToListAsync(query);
	}

	private async Task<List<BookReservation>> GetReservationsAsync(LibraryAcademicContext context, Guid? academicYearId)
	{
		var query = await requestScope.GetReadableRequestsAsync(context.UserScope);
		if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
		return await AsyncExecuter.ToListAsync(query);
	}

	private async Task<Dictionary<Guid, string>> GetCategoriesAsync(IEnumerable<Guid> categoryIds)
	{
		var ids = categoryIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var query = await categoryRepository.GetQueryableAsync();
		return (await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	private async Task<Dictionary<Guid, string>> GetSectionsAsync(IEnumerable<Guid> sectionIds)
	{
		var ids = sectionIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var query = await sectionRepository.GetQueryableAsync();
		return (await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.SectionName ?? string.Empty);
	}

	private static LibraryStockTotalsDto BuildStockTotals(
		IReadOnlyCollection<Book> books,
		IReadOnlyCollection<BookCopy> copies) =>
		new()
		{
			TotalBooks = books.Count,
			TotalCopies = copies.Count,
			AvailableCopies = copies.Count(x => x.Status == BookCopyStatus.Available),
			IssuedCopies = copies.Count(x => x.Status == BookCopyStatus.Issued),
			ReservedCopies = copies.Count(x => x.Status == BookCopyStatus.Reserved),
			LostCopies = copies.Count(x => x.Status == BookCopyStatus.Lost),
			DamagedCopies = copies.Count(x => x.Status == BookCopyStatus.Damaged),
			UnderRepairCopies = copies.Count(x => x.Status == BookCopyStatus.UnderRepair),
			InCustodyCopies = copies.Count(x => x.Status == BookCopyStatus.InCustody),
			InTransitCopies = copies.Count(x => x.Status == BookCopyStatus.InTransit),
			DiscardedCopies = copies.Count(x => x.Status == BookCopyStatus.Discarded)
		};

	private static LibraryCirculationTotalsDto BuildCirculationTotals(
		IReadOnlyCollection<BookIssue> issues,
		DateOnly? today,
		DateOnly? weekStart) =>
		new()
		{
			ActiveIssues = issues.Count(x => x.Status == BookIssueStatus.Issued),
			OverdueIssues = issues.Count(x => x.Status == BookIssueStatus.Issued && today.HasValue && LibraryLoanDates.ConfirmedDueDate(x) < today),
			IssuesToday = issues.Count(x => today.HasValue && LibraryLoanDates.ConfirmedIssueDate(x) == today),
			ReturnsToday = issues.Count(x => today.HasValue && LibraryLoanDates.ConfirmedReturnDate(x) == today),
			IssuesThisWeek = issues.Count(x => today.HasValue && LibraryLoanDates.ConfirmedIssueDate(x) >= weekStart && LibraryLoanDates.ConfirmedIssueDate(x) <= today),
			ReturnsThisWeek = issues.Count(x => today.HasValue && LibraryLoanDates.ConfirmedReturnDate(x) >= weekStart && LibraryLoanDates.ConfirmedReturnDate(x) <= today)
		};

	private static LibraryReservationTotalsDto BuildReservationTotals(
		IReadOnlyCollection<BookReservation> reservations,
		IReadOnlyCollection<LibraryHold> holds,
		DateOnly? today) =>
		new()
		{
			ActiveReservations = reservations.Count(x => x.State is LibraryReservationState.Waiting or LibraryReservationState.Ready),
			ExpiringToday = reservations.Count(x => today.HasValue && RequestDeadline(x, holds) == today),
			ExpiredReservations = reservations.Count(x => x.State == LibraryReservationState.Expired)
		};

	private static List<LibraryDashboardTrendPointDto> BuildWeeklyCirculation(
		IReadOnlyCollection<BookIssue> issues,
		DateOnly? weekStart,
		DateOnly? today)
	{
		var points = new List<LibraryDashboardTrendPointDto>();
		if (!weekStart.HasValue || !today.HasValue) return points;
		for (var date = weekStart.Value; date <= today.Value; date = date.AddDays(1))
		{
			var current = date;
			points.Add(new LibraryDashboardTrendPointDto
			{
				Date = current.ToDateTime(TimeOnly.MinValue),
				Label = current.ToString("ddd"),
				Issued = issues.Count(x => LibraryLoanDates.ConfirmedIssueDate(x) == current),
				Returned = issues.Count(x => LibraryLoanDates.ConfirmedReturnDate(x) == current)
			});
		}

		return points;
	}

	private static List<LibraryDashboardCategoryDistributionDto> BuildCategoryDistribution(
		IReadOnlyCollection<Book> books,
		IReadOnlyCollection<BookCopy> copies,
		IReadOnlyDictionary<Guid, string> categories)
	{
		var copyCountByBookId = copies.GroupBy(x => x.BookId).ToDictionary(x => x.Key, x => x.Count());
		return books
			.GroupBy(x => x.CategoryId)
			.Select(group => new LibraryDashboardCategoryDistributionDto
			{
				CategoryId = group.Key,
				CategoryName = categories.GetValueOrDefault(group.Key, "Uncategorized"),
				BookCount = group.Count(),
				CopyCount = group.Sum(book => copyCountByBookId.GetValueOrDefault(book.Id))
			})
			.OrderByDescending(x => x.CopyCount)
			.ToList();
	}

	private static List<LibraryDashboardSectionInventoryDto> BuildSectionInventory(
		IReadOnlyCollection<BookCopy> copies,
		IReadOnlyDictionary<Guid, string> sections) =>
		copies
			.GroupBy(x => x.SectionId)
			.Select(group =>
			{
				var issued = group.Count(x => x.Status == BookCopyStatus.Issued);
				var total = group.Count();
				return new LibraryDashboardSectionInventoryDto
				{
					SectionId = group.Key,
					SectionName = sections.GetValueOrDefault(group.Key, "Unassigned"),
					CopyCount = total,
					AvailableCopies = group.Count(x => x.Status == BookCopyStatus.Available),
					IssuedCopies = issued,
					LostDamagedCopies = group.Count(x => x.Status is BookCopyStatus.Lost or BookCopyStatus.Damaged),
					UtilizationRate = Ratio(issued, total)
				};
			})
			.OrderByDescending(x => x.CopyCount)
			.ToList();

	private static List<LibraryDashboardPopularBookDto> BuildPopularBooks(
		IReadOnlyCollection<Book> books,
		IReadOnlyCollection<BookCopy> copies,
		IReadOnlyCollection<BookIssue> issues,
		IReadOnlyCollection<BookReservation> reservations,
		IReadOnlyDictionary<Guid, string> categories)
	{
		var copyBookLookup = copies.ToDictionary(x => x.Id, x => x.BookId);
		var accessionBookLookup = copies.GroupBy(x => x.AccessionNumber.Trim().ToUpperInvariant(), StringComparer.Ordinal)
			.Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single().BookId, StringComparer.Ordinal);
		Guid? BookId(BookIssue issue) => issue.BookCopyId != Guid.Empty
			? copyBookLookup.TryGetValue(issue.BookCopyId, out var id) ? id : null
			: accessionBookLookup.TryGetValue(issue.AccessionNumber.Trim().ToUpperInvariant(), out var legacyId) ? legacyId : null;

		return books
			.Select(book =>
			{
				var issueCount = issues.Count(issue => BookId(issue) == book.Id);

				return new LibraryDashboardPopularBookDto
				{
					BookId = book.Id,
					Title = book.Title,
					Author = book.Author,
					CategoryName = categories.GetValueOrDefault(book.CategoryId, string.Empty),
					IssueCount = issueCount,
					ActiveIssues = issues.Count(issue => issue.Status == BookIssueStatus.Issued && BookId(issue) == book.Id),
					ReservationCount = reservations.Count(x => x.BookId == book.Id)
				};
			})
			.Where(x => x.IssueCount > 0 || x.ReservationCount > 0)
			.OrderByDescending(x => x.IssueCount)
			.ThenByDescending(x => x.ReservationCount)
			.ThenBy(x => x.Title)
			.Take(10)
			.ToList();
	}

	private static LibraryUtilizationDto BuildUtilization(LibraryDashboardOverviewDto overview)
	{
		var stock = overview.StockTotals;
		var activeIssues = overview.Circulation.ActiveIssues;
		var totalCharges = overview.Charges.PendingAmount + overview.Charges.PaidAmount + overview.Charges.WaivedAmount;

		return new LibraryUtilizationDto
		{
			AvailabilityRate = Ratio(stock.AvailableCopies, stock.TotalCopies),
			CirculationRate = Ratio(activeIssues, stock.TotalCopies),
			OverdueRate = Ratio(overview.Circulation.OverdueIssues, activeIssues),
			ChargeCollectionRate = totalCharges == 0 ? 0 : Math.Round(overview.Charges.PaidAmount * 100 / totalCharges, 2)
		};
	}

	private static List<LibraryDashboardAlertDto> BuildAlerts(
		LibraryDashboardOverviewDto overview,
		int collectibleCharges,
		int overdueCharges)
	{
		var alerts = new List<LibraryDashboardAlertDto>();

		if (overview.Circulation.OverdueIssues > 0)
		{
			alerts.Add(new LibraryDashboardAlertDto
			{
				Type = "overdue",
				Severity = "error",
				Title = "Overdue books",
				Message = $"{overview.Circulation.OverdueIssues} issued books are past due.",
				Count = overview.Circulation.OverdueIssues,
				Link = "/library/issued-books"
			});
		}

		AppendFinancialAlerts(alerts, overview.FinancePosition, collectibleCharges, overdueCharges);

		var lostDamaged = overview.StockTotals.LostCopies + overview.StockTotals.DamagedCopies;
		if (lostDamaged > 0)
		{
			alerts.Add(new LibraryDashboardAlertDto
			{
				Type = "lostDamaged",
				Severity = "info",
				Title = "Lost or damaged copies",
				Message = $"{lostDamaged} copies need attention.",
				Count = lostDamaged,
				Link = "/library/book-list"
			});
		}

		return alerts;
	}

	private static decimal Ratio(decimal numerator, decimal denominator) =>
		denominator <= 0 ? 0 : Math.Round(numerator * 100 / denominator, 2);
}
