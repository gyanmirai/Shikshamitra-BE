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
}

public class FinanceReportOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class FinanceReportDownloadDto
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
