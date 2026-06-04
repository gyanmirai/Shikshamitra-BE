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

	public static HrReportExportJobDto ToJobDto(HrReportExportJob job)
	{
		var format = NormalizeFormat(job.Format);
		return new HrReportExportJobDto
		{
			Id = job.Id,
			ReportType = job.ReportType,
			Format = format,
			Status = job.Status,
			FileName = job.Status == HrReportExportStatus.Completed ? BuildFileName(job.ReportType, format) : null,
			ErrorMessage = job.ErrorMessage,
			CreationTime = job.CreationTime,
			StartedAt = job.StartedAt,
			CompletedAt = job.CompletedAt,
			ExpiresAt = job.ExpiresAt
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
