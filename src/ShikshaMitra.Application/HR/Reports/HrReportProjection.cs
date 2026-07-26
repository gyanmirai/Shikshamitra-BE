using System;
using System.IO;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportProjection
{
	public static HrReportTemplateDto ToTemplateDto(HrReportTemplate template)
	{
		var definition = JsonSerializer.Deserialize<HrReportRequestDto>(template.DefinitionJson, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? new HrReportRequestDto { DataSource = template.DataSource, ReportType = template.Name };

		return new HrReportTemplateDto
		{
			Id = template.Id,
			Name = template.Name,
			Description = template.Description,
			Category = template.Category,
			DataSource = template.DataSource,
			Visibility = template.Visibility,
			Status = template.Status,
			OwnerUserId = template.OwnerUserId,
			Definition = definition,
			CreationTime = template.CreationTime,
			LastModificationTime = template.LastModificationTime
		};
	}

	public static HrReportExportJobDto ToJobDto(HrReportExportJob job, DateTime now)
	{
		var format = NormalizeFormat(job.Format);
		var request = DeserializeRequest(job);
		return new HrReportExportJobDto
		{
			Id = job.Id,
			ReportType = job.ReportType,
			Format = format,
			Status = job.Status,
			RequestSnapshot = request,
			ParentJobId = request.ParentJobId,
			SourceAction = string.IsNullOrWhiteSpace(request.SourceAction) ? "manualExport" : request.SourceAction,
			TemplateId = request.TemplateId,
			TemplateName = request.TemplateName,
			FileName = job.Status == HrReportExportStatus.Completed ? BuildFileName(job.ReportType, format) : null,
			ErrorMessage = job.Status == HrReportExportStatus.Failed
				? HrReportExportErrorFormatter.NormalizePersistedMessage(job.ErrorMessage, format)
				: null,
			ProgressPercent = job.ProgressPercent,
			RowCount = job.RowCount,
			FileSizeBytes = job.FileSizeBytes,
			RequestedByUserId = job.RequestedByUserId,
			GeneratedAt = job.CompletedAt,
			IsExpired = job.ExpiresAt <= now,
			SharedUserIds = [.. job.GetSharedUserIds()],
			CreationTime = job.CreationTime,
			StartedAt = job.StartedAt,
			CompletedAt = job.CompletedAt,
			ExpiresAt = job.ExpiresAt
		};
	}

	private static HrReportRequestDto DeserializeRequest(HrReportExportJob job)
	{
		var request = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson);
		if (request != null)
		{
			return request;
		}

		return new HrReportRequestDto
		{
			ReportType = job.ReportType,
			Format = job.Format
		};
	}

	public static string NormalizeFormat(string? format) =>
		string.Equals(format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
			? HrReportFormat.Pdf
			: HrReportFormat.Excel;

	public static string BuildFileName(string reportType, string format)
	{
		var safeName = string.Join("-", reportType.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
			.Replace(' ', '-')
			.ToLowerInvariant();
		return $"{safeName}.{(format == HrReportFormat.Pdf ? "pdf" : "xlsx")}";
	}
}
