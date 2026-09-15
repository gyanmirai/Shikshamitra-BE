using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Transport;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Transport;

[ApiController]
[Authorize]
[Route("api/transport")]
public class TransportDashboardReportsController(ITransportDashboardReportAppService dashboardReportAppService) : AbpController
{
	[HttpGet("dashboard/summary")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportDashboard)]
	public async Task<Result<TransportDashboardSummaryDto>> GetDashboardSummary(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid? academicYearId)
	{
		var summary = await dashboardReportAppService.GetDashboardSummaryAsync(academicMediumId, academicYearId);
		return Result.Success(summary);
	}

	[HttpGet("dashboard/alerts")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportDashboard)]
	public async Task<Result<PagedResultDto<TransportDashboardAlertDto>>> GetDashboardAlerts(
		[FromQuery] TransportDashboardAlertListRequestDto input)
	{
		var alerts = await dashboardReportAppService.GetDashboardAlertsAsync(input);
		return Result.Success(alerts);
	}

	[HttpGet("reports/catalog")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<Result<System.Collections.Generic.List<TransportReportCatalogItemDto>>> GetReportCatalog(
		[FromQuery] Guid academicMediumId)
	{
		var catalog = await dashboardReportAppService.GetReportCatalogAsync(academicMediumId);
		return Result.Success(catalog);
	}

	[HttpPost("reports/ai/plan")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<Result<TransportAiReportPlanResponseDto>> CreateAiReportPlan(
		[FromBody] TransportAiReportPlanRequestDto input)
	{
		var plan = await dashboardReportAppService.CreateAiReportPlanAsync(input);
		return Result.Success(plan);
	}

	[HttpGet("reports/{reportKey}")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<Result<TransportReportResultDto>> GetReport(
		string reportKey,
		[FromQuery] TransportReportListRequestDto input)
	{
		var report = await dashboardReportAppService.GetReportAsync(reportKey, input);
		return Result.Success(report);
	}

	[HttpGet("reports/{reportKey}/export")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportReports)]
	public async Task<IActionResult> ExportReport(
		string reportKey,
		[FromQuery] TransportReportListRequestDto input,
		[FromQuery] string? format = "csv")
	{
		if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
		{
			return BadRequest("Only CSV export is supported for transport reports.");
		}

		var export = await dashboardReportAppService.ExportReportAsync(reportKey, input);
		return File(export.Content, export.ContentType, export.FileName);
	}
}
