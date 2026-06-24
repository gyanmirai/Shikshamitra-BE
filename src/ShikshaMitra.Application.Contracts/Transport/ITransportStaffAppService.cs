using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportStaffAppService
{
	Task<PagedResultDto<TransportStaffDto>> GetListAsync(TransportStaffListRequestDto input);
	Task<TransportStaffDto> GetAsync(Guid staffId, Guid academicMediumId);
	Task<TransportStaffDto> CreateAsync(Guid academicMediumId, CreateUpdateTransportStaffDto input);
	Task<TransportStaffDto> UpdateAsync(Guid staffId, Guid academicMediumId, CreateUpdateTransportStaffDto input);
	Task<TransportStaffDto> ChangeStatusAsync(Guid staffId, Guid academicMediumId, ChangeTransportStaffStatusDto input);
	Task<List<TransportOptionDto>> GetOptionsAsync(TransportStaffOptionsRequestDto input);
}
