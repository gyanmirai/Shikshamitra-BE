using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportBillingAppService
{
	Task<List<TransportZoneAreaDto>> GetZoneAreasAsync(Guid academicMediumId);
	Task<TransportZoneAreaDto> CreateZoneAreaAsync(Guid academicMediumId, CreateUpdateTransportZoneAreaDto input);
	Task<TransportZoneAreaDto> UpdateZoneAreaAsync(Guid zoneAreaId, Guid academicMediumId, CreateUpdateTransportZoneAreaDto input);
	Task DeleteZoneAreaAsync(Guid zoneAreaId, Guid academicMediumId);

	Task<List<TransportOptionDto>> GetFeeFrequencyOptionsAsync();
	Task<TransportFeeConfigurationDto?> GetFeeConfigurationAsync(Guid academicMediumId);
	Task<TransportFeeConfigurationDto> CreateFeeConfigurationAsync(Guid academicMediumId, CreateUpdateTransportFeeConfigurationDto input);
	Task<TransportFeeConfigurationDto> UpdateFeeConfigurationAsync(Guid configurationId, Guid academicMediumId, CreateUpdateTransportFeeConfigurationDto input);

	Task<TransportFeeRunPreviewDto> PreviewRunAsync(Guid academicMediumId, TransportFeeRunRequestDto input);
	Task<TransportFeeRunDto> CreateRunAsync(Guid academicMediumId, TransportFeeRunRequestDto input);
	Task<PagedResultDto<TransportFeeRunDto>> GetRunsAsync(TransportFeeRunListRequestDto input);
	Task<TransportFeeRunDto> GetRunAsync(Guid runId, Guid academicMediumId);
	Task<TransportFeeFinancePostResultDto> PostRunToFinanceAsync(Guid runId, Guid academicMediumId);
	Task<PagedResultDto<TransportFeeDueDto>> GetDuesAsync(TransportFeeDueListRequestDto input);
	Task<PagedResultDto<TransportFeeDueDto>> GetMemberDuesAsync(Guid memberId, TransportMemberDueListRequestDto input);
}
