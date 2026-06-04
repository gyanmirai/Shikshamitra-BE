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
public class HrReportAppService(
	HrReportGenerator reportGenerator,
	HrReportTemplateService templateService,
	HrReportExportService exportService) : ApplicationService, IHrReportAppService
{
	public Task<HrReportSourcesDto> GetSourcesAsync() =>
		reportGenerator.GetSourcesAsync();

	public async Task<HrReportOptionsDto> GetOptionsAsync()
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
		return await reportGenerator.GetOptionsAsync(userId, CurrentTenant.Id);
	}

	public Task<HrReportTemplateListDto> GetTemplatesAsync() =>
		templateService.GetTemplatesAsync();

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Add)]
	public Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input) =>
		templateService.CreateTemplateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Add)]
	public Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input) =>
		templateService.UpdateTemplateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Add)]
	public Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id) =>
		templateService.DuplicateTemplateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Delete)]
	public Task DeleteTemplateAsync(Guid id) =>
		templateService.DeleteTemplateAsync(id);

	public async Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
		return await reportGenerator.GenerateAsync(input, userId, CurrentTenant.Id, exportAllRows: false);
	}

	public Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input) =>
		exportService.CreateExportAsync(input);

	public Task<List<HrReportExportJobDto>> GetExportsAsync() =>
		exportService.GetExportsAsync();

	public Task<HrReportExportJobDto> GetExportAsync(Guid jobId) =>
		exportService.GetExportAsync(jobId);

	public Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId) =>
		exportService.DownloadExportAsync(jobId);
}

public class HrReportExportJobArgs
{
	public Guid JobId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid UserId { get; set; }
	public HrReportRequestDto Request { get; set; } = new();
}
