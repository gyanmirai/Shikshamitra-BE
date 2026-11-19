using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportRequestDto
{
    [Required]
    public string ReportType { get; set; } = string.Empty;

    public string Format { get; set; } = "excel";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public FinanceReportFilterDto Filters { get; set; } = new();
}

public class FinanceReportFilterDto
{
    public List<Guid>? AcademicYearIds { get; set; }
    public List<int>? Months { get; set; }
    public Guid? BatchId { get; set; }
    public List<Guid>? ReceiptBookIds { get; set; }
    public List<Guid>? FeeHeadIds { get; set; }
    public Guid? BoardId { get; set; }
    public Guid? MediumId { get; set; }
    public List<Guid>? OrganizationIds { get; set; }
    public List<Guid>? ClassIds { get; set; }
    public List<Guid>? DivisionIds { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? StudentId { get; set; }
    public string? PayerSearch { get; set; }
    public string? GroupBy { get; set; }
    public string? Installment { get; set; }
    public string? AgeingBucket { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Collector { get; set; }
    public string? Bank { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? ReceiptKind { get; set; }
    public string? DateBasis { get; set; }
}

public class FinanceReportPreviewDto
{
    public string ReportType { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<FinanceReportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public FinanceReportFilterDto NormalizedFilters { get; set; } = new();
}

public class FinanceReportColumnDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Kind { get; set; }
}

public class FinanceReportExportCreateResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "Queued";
}

public class FinanceReportOptionsDto
{
    public List<FinanceReportOptionDto> ReportTypes { get; set; } = new();
    public List<FinanceReportOptionDto> AcademicYears { get; set; } = new();
    public List<FinanceReportScopeOptionDto> Scope { get; set; } = new();
    public List<FinanceReportOptionDto> FeeHeads { get; set; } = new();
    public List<FinanceReportOptionDto> ReceiptBooks { get; set; } = new();
    public List<FinanceReportOptionDto> PaymentMethods { get; set; } = new();
}

public class FinanceReportScopeOptionDto
{
    public string AcademicYearId { get; set; } = string.Empty;
    public FinanceReportOptionDto Board { get; set; } = new();
    public FinanceReportOptionDto Medium { get; set; } = new();
    public FinanceReportOptionDto Stage { get; set; } = new();
    public FinanceReportOptionDto Class { get; set; } = new();
    public List<FinanceReportOptionDto> Divisions { get; set; } = new();
}

public class FinanceReportExportDto
{
    public Guid JobId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class FinanceReportOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? AcademicYearId { get; set; }
    public bool YearWideOnly { get; set; }
}

public class FinanceReportDownloadDto
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
