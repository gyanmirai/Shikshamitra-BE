using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Dashboard.View)]
[RemoteService(false)]
public class LibraryDashboardAppService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> bookCopyRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<LibraryCharge, Guid> chargeRepository,
	IRepository<BookCategory, Guid> categoryRepository,
	IRepository<LibraryChargeCategory, Guid> chargeCategoryRepository,
	IRepository<LibrarySection, Guid> sectionRepository)
	: ApplicationService, ILibraryDashboardAppService
{
	public async Task<LibraryDashboardOverviewDto> GetOverviewAsync(Guid? academicYearId = null)
	{
		var today = DateTime.UtcNow.Date;
		var weekStart = today.AddDays(-6);

		var books = await GetBooksAsync(academicYearId);
		var bookIds = books.Select(x => x.Id).ToHashSet();

		var copies = await GetBookCopiesAsync(bookIds);
		var copyIds = copies.Select(x => x.Id).ToHashSet();
		var accessionNumbers = copies.Select(x => x.AccessionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var issues = await GetIssuesAsync(copyIds, accessionNumbers);
		var reservations = await GetReservationsAsync(bookIds);
		var charges = await GetChargesAsync(academicYearId, issues.Select(x => x.Id).ToHashSet());
		var categories = await GetCategoriesAsync(books.Select(x => x.CategoryId).Distinct());
		var sections = await GetSectionsAsync(copies.Select(x => x.SectionId).Distinct());

		var overview = new LibraryDashboardOverviewDto
		{
			AcademicYearId = academicYearId,
			GeneratedAt = DateTime.UtcNow,
			StockTotals = BuildStockTotals(books, copies),
			Circulation = BuildCirculationTotals(issues, today, weekStart),
			Charges = BuildChargeTotals(charges),
			Reservations = BuildReservationTotals(reservations, today),
			WeeklyCirculation = BuildWeeklyCirculation(issues, weekStart, today),
			CategoryDistribution = BuildCategoryDistribution(books, copies, categories),
			SectionInventory = BuildSectionInventory(copies, sections),
			PopularBooks = BuildPopularBooks(books, copies, issues, reservations, categories),
		};

		overview.Utilization = BuildUtilization(overview);
		overview.Alerts = BuildAlerts(overview, charges);
		return overview;
	}

	private async Task<List<Book>> GetBooksAsync(Guid? academicYearId)
	{
		var query = await bookRepository.GetQueryableAsync();
		if (academicYearId.HasValue)
		{
			query = query.Where(x => x.AcademicYearId == academicYearId.Value);
		}

		return await AsyncExecuter.ToListAsync(query);
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

	private async Task<List<BookIssue>> GetIssuesAsync(IReadOnlySet<Guid> copyIds, IReadOnlySet<string> accessionNumbers)
	{
		if (copyIds.Count == 0 && accessionNumbers.Count == 0)
		{
			return [];
		}

		var query = await issueRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(
			query.Where(x => copyIds.Contains(x.BookCopyId) || accessionNumbers.Contains(x.AccessionNumber)));
	}

	private async Task<List<BookReservation>> GetReservationsAsync(IReadOnlySet<Guid> bookIds)
	{
		if (bookIds.Count == 0)
		{
			return [];
		}

		var query = await reservationRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query.Where(x => bookIds.Contains(x.BookId)));
	}

	private async Task<List<LibraryCharge>> GetChargesAsync(Guid? academicYearId, IReadOnlySet<Guid> issueIds)
	{
		var query = await chargeRepository.GetQueryableAsync();
		var charges = await AsyncExecuter.ToListAsync(query);
		if (!academicYearId.HasValue)
		{
			return charges;
		}

		var categoryQuery = await chargeCategoryRepository.GetQueryableAsync();
		var chargeCategoryIds = (await AsyncExecuter.ToListAsync(
				categoryQuery.Where(x => x.AcademicYearId == academicYearId.Value)))
			.Select(x => x.Id)
			.ToHashSet();

		return charges
			.Where(x => chargeCategoryIds.Contains(x.ChargeCategoryId) ||
			            (x.BookIssueId.HasValue && issueIds.Contains(x.BookIssueId.Value)))
			.ToList();
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
			DiscardedCopies = copies.Count(x => x.Status == BookCopyStatus.Discarded)
		};

	private static LibraryCirculationTotalsDto BuildCirculationTotals(
		IReadOnlyCollection<BookIssue> issues,
		DateTime today,
		DateTime weekStart) =>
		new()
		{
			ActiveIssues = issues.Count(x => x.Status == BookIssueStatus.Issued),
			OverdueIssues = issues.Count(x => x.Status == BookIssueStatus.Issued && x.DueDate.Date < today),
			IssuesToday = issues.Count(x => x.IssueDate.Date == today),
			ReturnsToday = issues.Count(x => x.ReturnDate.HasValue && x.ReturnDate.Value.Date == today),
			IssuesThisWeek = issues.Count(x => x.IssueDate.Date >= weekStart && x.IssueDate.Date <= today),
			ReturnsThisWeek = issues.Count(x => x.ReturnDate.HasValue && x.ReturnDate.Value.Date >= weekStart && x.ReturnDate.Value.Date <= today)
		};

	private static LibraryChargeTotalsDto BuildChargeTotals(IReadOnlyCollection<LibraryCharge> charges) =>
		new()
		{
			PendingCount = charges.Count(x => x.PaymentStatus == ChargePaymentStatus.Pending),
			PendingAmount = charges.Where(x => x.PaymentStatus == ChargePaymentStatus.Pending).Sum(x => x.Amount),
			PaidCount = charges.Count(x => x.PaymentStatus == ChargePaymentStatus.Paid),
			PaidAmount = charges.Where(x => x.PaymentStatus == ChargePaymentStatus.Paid).Sum(x => x.Amount),
			WaivedCount = charges.Count(x => x.PaymentStatus == ChargePaymentStatus.Waived),
			WaivedAmount = charges.Where(x => x.PaymentStatus == ChargePaymentStatus.Waived).Sum(x => x.Amount)
		};

	private static LibraryReservationTotalsDto BuildReservationTotals(
		IReadOnlyCollection<BookReservation> reservations,
		DateTime today) =>
		new()
		{
			ActiveReservations = reservations.Count(x => x.ReserveDueDate.Date >= today),
			ExpiringToday = reservations.Count(x => x.ReserveDueDate.Date == today),
			ExpiredReservations = reservations.Count(x => x.ReserveDueDate.Date < today)
		};

	private static List<LibraryDashboardTrendPointDto> BuildWeeklyCirculation(
		IReadOnlyCollection<BookIssue> issues,
		DateTime weekStart,
		DateTime today)
	{
		var points = new List<LibraryDashboardTrendPointDto>();
		for (var date = weekStart; date <= today; date = date.AddDays(1))
		{
			var current = date.Date;
			points.Add(new LibraryDashboardTrendPointDto
			{
				Date = current,
				Label = current.ToString("ddd"),
				Issued = issues.Count(x => x.IssueDate.Date == current),
				Returned = issues.Count(x => x.ReturnDate.HasValue && x.ReturnDate.Value.Date == current)
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
		var accessionBookLookup = copies
			.GroupBy(x => x.AccessionNumber, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.First().BookId, StringComparer.OrdinalIgnoreCase);

		return books
			.Select(book =>
			{
				var issueCount = issues.Count(issue =>
					copyBookLookup.GetValueOrDefault(issue.BookCopyId) == book.Id ||
					accessionBookLookup.GetValueOrDefault(issue.AccessionNumber) == book.Id);

				return new LibraryDashboardPopularBookDto
				{
					BookId = book.Id,
					Title = book.Title,
					Author = book.Author,
					CategoryName = categories.GetValueOrDefault(book.CategoryId, string.Empty),
					IssueCount = issueCount,
					ActiveIssues = issues.Count(issue => issue.Status == BookIssueStatus.Issued &&
						(copyBookLookup.GetValueOrDefault(issue.BookCopyId) == book.Id ||
						 accessionBookLookup.GetValueOrDefault(issue.AccessionNumber) == book.Id)),
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
		IReadOnlyCollection<LibraryCharge> charges)
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

		if (overview.Charges.PendingAmount > 0)
		{
			alerts.Add(new LibraryDashboardAlertDto
			{
				Type = "pendingCharges",
				Severity = "warning",
				Title = "Pending Library charges",
				Message = $"{overview.Charges.PendingCount} charges are pending collection.",
				Count = overview.Charges.PendingCount,
				Link = "/library/charges-management"
			});
		}

		var overdueCharges = charges.Count(x =>
			x.PaymentStatus == ChargePaymentStatus.Pending &&
			x.DueDate.Date < DateTime.UtcNow.Date);
		if (overdueCharges > 0)
		{
			alerts.Add(new LibraryDashboardAlertDto
			{
				Type = "overdueCharges",
				Severity = "error",
				Title = "Overdue charge dues",
				Message = $"{overdueCharges} pending charges crossed the due date.",
				Count = overdueCharges,
				Link = "/library/charges-management"
			});
		}

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
