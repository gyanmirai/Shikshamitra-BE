using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportAppService : IApplicationService
{
	Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input);
	Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input);
	Task<List<HrReportExportJobDto>> GetExportsAsync(HrReportExportListRequestDto input);
	Task<HrReportExportJobDto> GetExportAsync(Guid jobId);
	Task<HrReportExportJobDto> CancelExportAsync(Guid jobId);
	Task DeleteFailedExportAsync(Guid jobId);
	Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId);
	Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId);
	Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId);
}