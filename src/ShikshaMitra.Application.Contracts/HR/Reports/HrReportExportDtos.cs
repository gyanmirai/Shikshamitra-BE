using System;

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
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
}

public class HrReportDownloadDto
{
	public byte[] Bytes { get; set; } = Array.Empty<byte>();
	public string ContentType { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}
