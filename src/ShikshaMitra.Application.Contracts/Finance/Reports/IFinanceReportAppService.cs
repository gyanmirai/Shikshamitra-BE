using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Reports;

public interface IFinanceReportAppService : IApplicationService
{
    Task<FinanceReportOptionsDto> GetOptionsAsync();
    Task<FinanceReportPreviewDto> PreviewAsync(FinanceReportRequestDto input);
    Task<FinanceReportExportCreateResponseDto> CreateExportAsync(FinanceReportRequestDto input);
    Task<FinanceReportDownloadDto> DownloadExportAsync(Guid jobId);
}
