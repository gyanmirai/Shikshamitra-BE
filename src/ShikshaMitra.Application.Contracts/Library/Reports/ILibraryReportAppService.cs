using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Reports;

public interface ILibraryReportAppService : IApplicationService
{
	Task<LibraryReportOptionsDto> GetOptionsAsync();
	Task<LibraryAiReportPlanResponseDto> CreateAiPlanAsync(LibraryAiReportPlanRequestDto input);
	Task<LibraryReportPreviewDto> PreviewAsync(LibraryReportRequestDto input);
	Task<LibraryReportExportCreateResponseDto> CreateExportAsync(LibraryReportRequestDto input);
	Task<List<LibraryReportExportJobDto>> GetExportsAsync();
	Task<LibraryReportExportJobDto> GetExportAsync(Guid jobId);
	Task<LibraryReportDownloadDto> DownloadExportAsync(Guid jobId);
}
