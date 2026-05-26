using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportRequestDto
{
    [Required]
    public string ReportType { get; set; } = string.Empty;

    public string Format { get; set; } = AcademicReportFormat.Excel;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public AcademicReportFilterDto Filters { get; set; } = new();
}

public class AcademicReportFilterDto
{
    public Guid? AcademicYearId { get; set; }
    public List<Guid>? ClassIds { get; set; }
    public List<Guid>? SubjectIds { get; set; }

    /// <summary>
    /// Upcoming = 1, In Progress = 2, Completed = 3
    /// </summary>
    public List<int>? Statuses { get; set; }

    public bool? RevisionRequired { get; set; }
}

public class AcademicReportPreviewDto
{
    public string ReportType { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AcademicReportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public AcademicReportFilterDto NormalizedFilters { get; set; } = new();
}

public class AcademicReportColumnDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class AcademicReportExportCreateResponseDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "Queued";
}

public class AcademicReportExportJobDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class AcademicReportOptionsDto
{
    public List<AcademicReportOptionDto> ReportTypes { get; set; } = new();
    public List<AcademicReportOptionDto> Classes { get; set; } = new();
    public List<AcademicReportOptionDto> Subjects { get; set; } = new();
    public List<AcademicReportOptionDto> Statuses { get; set; } = new();
}

public class AcademicReportOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class AcademicReportDownloadDto
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public static class AcademicReportFormat
{
    public const string Pdf = "pdf";
    public const string Excel = "excel";
}

public static class AcademicReportTypes
{
    public const string ChapterProgressReport = "Chapter Progress Report";
    public const string SubjectWiseChapterCoverage = "Subject-wise Chapter Coverage";
    public const string ChapterDescriptionSummary = "Chapter Description Summary";
    public const string TimelineReport = "Timeline Report";
    public const string ClassWiseChapterAssignment = "Class-wise Chapter Assignment Report";
    public const string CurriculumCompletionReport = "Curriculum Completion Report";
    public const string ChapterRevisionTracker = "Chapter Revision Tracker";
}
