using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportDashboardReportAppService
{
	Task<TransportDashboardSummaryDto> GetDashboardSummaryAsync(Guid academicMediumId, Guid? academicYearId);
	Task<PagedResultDto<TransportDashboardAlertDto>> GetDashboardAlertsAsync(TransportDashboardAlertListRequestDto input);
	Task<List<TransportReportCatalogItemDto>> GetReportCatalogAsync(Guid academicMediumId);
	Task<TransportReportResultDto> GetReportAsync(string reportKey, TransportReportListRequestDto input);
	Task<TransportReportExportDto> ExportReportAsync(string reportKey, TransportReportListRequestDto input);
}
