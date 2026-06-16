using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportAppService : IApplicationService
{
	Task<HrReportSourcesDto> GetSourcesAsync();
	Task<HrReportOptionsDto> GetOptionsAsync();
	Task<List<HrReportRecipientDto>> GetRecipientsAsync(string? filter = null, int maxResultCount = 20, string? ids = null);
	Task<HrReportTemplateListDto> GetTemplatesAsync();
	Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id);
	Task DeleteTemplateAsync(Guid id);
	Task<HrReportPreviewDto> GetPreviewAsync(HrReportRequestDto input);
	Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input);
	Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input);
	Task<List<HrReportExportJobDto>> GetExportsAsync();
	Task<HrReportExportJobDto> GetExportAsync(Guid jobId);
	Task<HrReportExportJobDto> CancelExportAsync(Guid jobId);
	Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId);
	Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId);
	Task<HrReportExportJobDto> ShareExportAsync(Guid jobId, HrReportShareExportRequestDto input);
	Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId);
	Task<HrReportScheduleListDto> GetSchedulesAsync();
	Task<HrReportScheduleDto> CreateScheduleAsync(HrReportScheduleRequestDto input);
	Task<HrReportScheduleDto> UpdateScheduleAsync(Guid id, HrReportScheduleRequestDto input);
	Task<HrReportScheduleDto> PauseScheduleAsync(Guid id);
	Task<HrReportScheduleDto> ResumeScheduleAsync(Guid id);
	Task<HrReportScheduleRunResponseDto> RunScheduleNowAsync(Guid id);
	Task DeleteScheduleAsync(Guid id);
}
