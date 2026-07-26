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
	HrReportExportService exportService,
	HrReportScheduleService scheduleService,
	HrReportRecipientLookupService recipientLookupService) : ApplicationService, IHrReportAppService
{
	public Task<HrReportSourcesDto> GetSourcesAsync() =>
		reportGenerator.GetSourcesAsync();

	public async Task<HrReportOptionsDto> GetOptionsAsync()
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
		return await reportGenerator.GetOptionsAsync(userId, CurrentTenant.Id);
	}

	public Task<List<HrReportRecipientDto>> GetRecipientsAsync(string? filter = null, int maxResultCount = 20, string? ids = null) =>
		recipientLookupService.GetRecipientsAsync(filter, maxResultCount, ids);

	public Task<HrReportTemplateListDto> GetTemplatesAsync() =>
		templateService.GetTemplatesAsync();

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates)]
	public Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input) =>
		templateService.CreateTemplateAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates)]
	public Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input) =>
		templateService.UpdateTemplateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates)]
	public Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id) =>
		templateService.DuplicateTemplateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Delete)]
	public Task DeleteTemplateAsync(Guid id) =>
		templateService.DeleteTemplateAsync(id);

	public Task<HrReportPreviewDto> GetPreviewAsync(HrReportRequestDto input) =>
		PreviewAsync(input);

	public async Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
		return await reportGenerator.GenerateAsync(input, userId, CurrentTenant.Id, exportAllRows: false);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input) =>
		exportService.CreateExportAsync(input);

	public Task<List<HrReportExportJobDto>> GetExportsAsync(HrReportExportListRequestDto input) =>
		exportService.GetExportsAsync(input);

	public Task<HrReportExportJobDto> GetExportAsync(Guid jobId) =>
		exportService.GetExportAsync(jobId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportJobDto> CancelExportAsync(Guid jobId) =>
		exportService.CancelExportAsync(jobId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Delete)]
	public Task DeleteFailedExportAsync(Guid jobId) =>
		exportService.DeleteFailedExportAsync(jobId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId) =>
		exportService.RetryExportAsync(jobId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Export)]
	public Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId) =>
		exportService.RegenerateExportAsync(jobId);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Share)]
	public Task<HrReportExportJobDto> ShareExportAsync(Guid jobId, HrReportShareExportRequestDto input) =>
		exportService.ShareExportAsync(jobId, input);

	public Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId) =>
		exportService.DownloadExportAsync(jobId);

	public Task<HrReportScheduleListDto> GetSchedulesAsync() =>
		scheduleService.GetSchedulesAsync();

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task<HrReportScheduleDto> CreateScheduleAsync(HrReportScheduleRequestDto input) =>
		scheduleService.CreateScheduleAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task<HrReportScheduleDto> UpdateScheduleAsync(Guid id, HrReportScheduleRequestDto input) =>
		scheduleService.UpdateScheduleAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task<HrReportScheduleDto> PauseScheduleAsync(Guid id) =>
		scheduleService.PauseScheduleAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task<HrReportScheduleDto> ResumeScheduleAsync(Guid id) =>
		scheduleService.ResumeScheduleAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task<HrReportScheduleRunResponseDto> RunScheduleNowAsync(Guid id) =>
		scheduleService.RunScheduleNowAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Schedule)]
	public Task DeleteScheduleAsync(Guid id) =>
		scheduleService.DeleteScheduleAsync(id);
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
