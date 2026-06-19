using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportRequestDto
{
	[Required]
	public string ReportType { get; set; } = string.Empty;

	public string Format { get; set; } = LibraryReportFormat.Excel;
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
	public List<string>? Columns { get; set; }
	public List<LibraryReportSortDto>? Sorts { get; set; }
	public LibraryReportFilterDto Filters { get; set; } = new();
}

public class LibraryReportFilterDto
{
	public Guid? AcademicYearId { get; set; }
	public List<Guid>? BookIds { get; set; }
	public List<Guid>? CategoryIds { get; set; }
	public List<Guid>? MemberIds { get; set; }
	public List<Guid>? SectionIds { get; set; }
	public List<int>? CopyStatuses { get; set; }
	public List<int>? IssueStatuses { get; set; }
	public List<int>? ChargeStatuses { get; set; }
	public int? MemberRole { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public string? Search { get; set; }
	public int? Top { get; set; }
}

public class LibraryReportSortDto
{
	public string Field { get; set; } = string.Empty;
	public string Direction { get; set; } = "asc";
}

public class LibraryReportPreviewDto
{
	public string ReportType { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public List<LibraryReportColumnDto> Columns { get; set; } = new();
	public List<Dictionary<string, string?>> Rows { get; set; } = new();
	public LibraryReportFilterDto NormalizedFilters { get; set; } = new();
	public List<string> QueryWarnings { get; set; } = new();
}

public class LibraryReportColumnDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}

public class LibraryReportExportCreateResponseDto
{
	public Guid JobId { get; set; }
	public string Status { get; set; } = "Queued";
}

public class LibraryReportExportJobDto
{
	public Guid Id { get; set; }
	public string ReportType { get; set; } = string.Empty;
	public string Format { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public LibraryReportRequestDto? RequestSnapshot { get; set; }
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public int ProgressPercent { get; set; }
	public int? RowCount { get; set; }
	public long? FileSizeBytes { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
}

public class LibraryReportOptionsDto
{
	public List<LibraryReportOptionDto> ReportTypes { get; set; } = new();
	public List<LibraryReportOptionDto> ComingSoonReportTypes { get; set; } = new();
	public List<LibraryReportOptionDto> Categories { get; set; } = new();
	public List<LibraryReportOptionDto> Sections { get; set; } = new();
	public List<LibraryReportOptionDto> CopyStatuses { get; set; } = new();
	public List<LibraryReportOptionDto> IssueStatuses { get; set; } = new();
	public List<LibraryReportOptionDto> ChargeStatuses { get; set; } = new();
	public List<LibraryReportOptionDto> Formats { get; set; } = new();
}

public class LibraryReportOptionDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string? Category { get; set; }
	public bool IsComingSoon { get; set; }
}

public class LibraryReportDownloadDto
{
	public byte[] Bytes { get; set; } = Array.Empty<byte>();
	public string ContentType { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}

public static class LibraryReportFormat
{
	public const string Pdf = "pdf";
	public const string Excel = "excel";
}

public static class LibraryReportTypes
{
	public const string BookMaster = "Book Master Report";
	public const string StockSummary = "Library Stock Summary";
	public const string AvailabilityStatus = "Book Availability Status Report";
	public const string IssueReturn = "Issue Return Report";
	public const string OverdueBooks = "Overdue Books Report";
	public const string FineCollection = "Fine Collection Report";
	public const string LostDamagedBooks = "Lost Damaged Books Report";
	public const string MemberList = "Library Member List";
	public const string ReservationList = "Book Reservation Report";
	public const string PopularBooks = "Popular Books Report";
}
