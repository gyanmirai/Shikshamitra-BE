using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Dashboard;

public class LibraryDashboardOverviewDto
{
	public Guid? AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public LibraryStockTotalsDto StockTotals { get; set; } = new();
	public LibraryCirculationTotalsDto Circulation { get; set; } = new();
	public LibraryChargeTotalsDto Charges { get; set; } = new();
	public LibraryReservationTotalsDto Reservations { get; set; } = new();
	public LibraryUtilizationDto Utilization { get; set; } = new();
	public List<LibraryDashboardTrendPointDto> WeeklyCirculation { get; set; } = new();
	public List<LibraryDashboardCategoryDistributionDto> CategoryDistribution { get; set; } = new();
	public List<LibraryDashboardSectionInventoryDto> SectionInventory { get; set; } = new();
	public List<LibraryDashboardPopularBookDto> PopularBooks { get; set; } = new();
	public List<LibraryDashboardAlertDto> Alerts { get; set; } = new();
}

public class LibraryStockTotalsDto
{
	public int TotalBooks { get; set; }
	public int TotalCopies { get; set; }
	public int AvailableCopies { get; set; }
	public int IssuedCopies { get; set; }
	public int ReservedCopies { get; set; }
	public int LostCopies { get; set; }
	public int DamagedCopies { get; set; }
	public int UnderRepairCopies { get; set; }
	public int DiscardedCopies { get; set; }
}

public class LibraryCirculationTotalsDto
{
	public int ActiveIssues { get; set; }
	public int OverdueIssues { get; set; }
	public int IssuesToday { get; set; }
	public int ReturnsToday { get; set; }
	public int IssuesThisWeek { get; set; }
	public int ReturnsThisWeek { get; set; }
}

public class LibraryChargeTotalsDto
{
	public int PendingCount { get; set; }
	public decimal PendingAmount { get; set; }
	public int PaidCount { get; set; }
	public decimal PaidAmount { get; set; }
	public int WaivedCount { get; set; }
	public decimal WaivedAmount { get; set; }
}

public class LibraryReservationTotalsDto
{
	public int ActiveReservations { get; set; }
	public int ExpiringToday { get; set; }
	public int ExpiredReservations { get; set; }
}

public class LibraryUtilizationDto
{
	public decimal AvailabilityRate { get; set; }
	public decimal CirculationRate { get; set; }
	public decimal OverdueRate { get; set; }
	public decimal ChargeCollectionRate { get; set; }
}

public class LibraryDashboardTrendPointDto
{
	public DateTime Date { get; set; }
	public string Label { get; set; } = string.Empty;
	public int Issued { get; set; }
	public int Returned { get; set; }
}

public class LibraryDashboardCategoryDistributionDto
{
	public Guid? CategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public int BookCount { get; set; }
	public int CopyCount { get; set; }
}

public class LibraryDashboardSectionInventoryDto
{
	public Guid? SectionId { get; set; }
	public string SectionName { get; set; } = string.Empty;
	public int CopyCount { get; set; }
	public int AvailableCopies { get; set; }
	public int IssuedCopies { get; set; }
	public int LostDamagedCopies { get; set; }
	public decimal UtilizationRate { get; set; }
}

public class LibraryDashboardPopularBookDto
{
	public Guid BookId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Author { get; set; } = string.Empty;
	public string CategoryName { get; set; } = string.Empty;
	public int IssueCount { get; set; }
	public int ActiveIssues { get; set; }
	public int ReservationCount { get; set; }
}

public class LibraryDashboardAlertDto
{
	public string Type { get; set; } = string.Empty;
	public string Severity { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public int Count { get; set; }
	public string? Link { get; set; }
}
