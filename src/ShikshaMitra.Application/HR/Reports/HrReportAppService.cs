using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View)]
public class HrReportAppService(HrReportGenerator generator, HrReportExportService exports, HrReportingAccess access) : ApplicationService, IHrReportAppService
{
	private async Task EnsureSalarySourceAsync(HrReportRequestDto input)
	{
		if (input.DataSource is not ("payroll_statement" or "payroll_register")) throw new BusinessException(HRErrorCodes.ReportRequestInvalid);
  if (input.Groups?.Count > 0 || input.Summaries?.Count > 0 || input.Filters.Conditions?.Count > 0 || input.Chart is { Type: not "none" } || input.Sorts?.Count > 1)
   throw HrReportingEngine.Invalid("Use a report template for advanced filters, summaries, charts, or multiple sorting fields.");
		await access.EnsureAsync(input.DataSource, CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired));
	}
	public async Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input)
	{
		await EnsureSalarySourceAsync(input);
		return await generator.GenerateAsync(input, CurrentUser.Id!.Value, CurrentTenant.Id, false);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public async Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input) { await EnsureSalarySourceAsync(input); return await exports.CreateExportAsync(input); }
	public Task<List<HrReportExportJobDto>> GetExportsAsync(HrReportExportListRequestDto input) => exports.GetExportsAsync(input);
	public Task<HrReportExportJobDto> GetExportAsync(Guid jobId) => exports.GetExportAsync(jobId);
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportJobDto> CancelExportAsync(Guid jobId) => exports.CancelExportAsync(jobId);
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Delete)]
	public Task DeleteFailedExportAsync(Guid jobId) => exports.DeleteFailedExportAsync(jobId);
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId) => exports.RetryExportAsync(jobId);
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId) => exports.RegenerateExportAsync(jobId);
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId) => exports.DownloadExportAsync(jobId);
}

public class HrReportExportJobArgs
{
	public Guid JobId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid UserId { get; set; }
	public HrReportRequestDto Request { get; set; } = new();
	public HrReportExportScopeSnapshot? ScopeSnapshot { get; set; }
	public Guid GenerationId { get; set; }
}
