using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportStopAppService
{
	Task<PagedResultDto<TransportStopDto>> GetListAsync(TransportStopListRequestDto input);
	Task<TransportStopDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportStopDto input);
	Task<TransportStopDto> UpdateAsync(Guid stopId, Guid academicMediumId, CreateUpdateTransportStopDto input);
	Task<TransportStopDto> ChangeStatusAsync(Guid stopId, Guid academicMediumId, ChangeTransportStatusDto input);
}
