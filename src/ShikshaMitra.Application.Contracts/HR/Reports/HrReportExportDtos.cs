using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportCreateResponseDto
{
	public Guid JobId { get; set; }
	public string Status { get; set; } = "Queued";
}

public class HrReportExportJobDto
{
	public Guid Id { get; set; }
	public string ReportType { get; set; } = string.Empty;
	public string Format { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public HrReportRequestDto? RequestSnapshot { get; set; }
	public Guid? ParentJobId { get; set; }
	public string? SourceAction { get; set; }
	public Guid? TemplateId { get; set; }
	public string? TemplateName { get; set; }
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public int ProgressPercent { get; set; }
	public int? RowCount { get; set; }
	public long? FileSizeBytes { get; set; }
	public Guid RequestedByUserId { get; set; }
	public DateTime? GeneratedAt { get; set; }
	public bool IsExpired { get; set; }
	public List<Guid> SharedUserIds { get; set; } = new();
	public DateTime CreationTime { get; set; }
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
}

public class HrReportExportListRequestDto
{
	public DateTime? RequestedFrom { get; set; }
	public DateTime? RequestedTo { get; set; }
}

public class HrReportExportListRequestDtoValidator : AbstractValidator<HrReportExportListRequestDto>
{
	public HrReportExportListRequestDtoValidator()
	{
		RuleFor(x => x)
			.Must(input =>
				!input.RequestedFrom.HasValue ||
				!input.RequestedTo.HasValue ||
				input.RequestedTo.Value > input.RequestedFrom.Value)
			.WithMessage("Requested to must be after requested from.");
	}
}

public class HrReportShareExportRequestDto
{
	public List<Guid> UserIds { get; set; } = new();
}

public class HrReportShareExportRequestDtoValidator : AbstractValidator<HrReportShareExportRequestDto>
{
	public HrReportShareExportRequestDtoValidator()
	{
		RuleFor(x => x.UserIds)
			.NotNull()
			.Must(ids => ids.Count > 0)
			.WithMessage("At least one user must be selected.");
		RuleForEach(x => x.UserIds)
			.NotEmpty();
	}
}

public class HrReportExportActionResponseDto
{
	public HrReportExportJobDto Job { get; set; } = new();
}

public class HrReportDownloadDto
{
	public byte[] Bytes { get; set; } = Array.Empty<byte>();
	public string ContentType { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}
