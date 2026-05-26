using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Reports;

public interface IAcademicReportAppService : IApplicationService
{
    Task<AcademicReportOptionsDto> GetOptionsAsync();
    Task<AcademicReportPreviewDto> PreviewAsync(AcademicReportRequestDto input);
    Task<AcademicReportExportCreateResponseDto> CreateExportAsync(AcademicReportRequestDto input);
    Task<List<AcademicReportExportJobDto>> GetExportsAsync();
    Task<AcademicReportExportJobDto> GetExportAsync(Guid jobId);
    Task<AcademicReportDownloadDto> DownloadExportAsync(Guid jobId);
}
