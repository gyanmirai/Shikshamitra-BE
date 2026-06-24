using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportApplicationAppService
{
	Task<PagedResultDto<TransportApplicationDto>> GetListAsync(TransportApplicationListRequestDto input);
	Task<TransportApplicationDto> GetAsync(Guid applicationId, Guid academicMediumId);
	Task<TransportApplicationDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportApplicationDto input);
	Task<TransportApplicationDto> UpdateAsync(Guid applicationId, Guid academicMediumId, CreateUpdateTransportApplicationDto input);
	Task<List<TransportSuggestedRouteDto>> GetSuggestedRoutesAsync(Guid applicationId, Guid academicMediumId);
	Task<TransportApplicationDto> AssignRouteStopAsync(Guid applicationId, Guid academicMediumId, AssignTransportApplicationRouteStopDto input);
	Task<TransportApplicationDto> ChangeStatusAsync(Guid applicationId, Guid academicMediumId, ChangeTransportApplicationStatusDto input);
}
