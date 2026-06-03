using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportAppService : IApplicationService
{
	Task<HrReportSourcesDto> GetSourcesAsync();
	Task<HrReportOptionsDto> GetOptionsAsync();
	Task<HrReportTemplateListDto> GetTemplatesAsync();
	Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id);
	Task DeleteTemplateAsync(Guid id);
	Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input);
	Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input);
	Task<List<HrReportExportJobDto>> GetExportsAsync();
	Task<HrReportExportJobDto> GetExportAsync(Guid jobId);
	Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId);
}
