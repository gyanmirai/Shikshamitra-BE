using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Transport;

public interface ITransportMemberAppService
{
	Task<PagedResultDto<TransportMemberCandidateDto>> GetCandidatesAsync(TransportMemberCandidateListRequestDto input);
	Task<PagedResultDto<TransportMemberDto>> GetListAsync(TransportMemberListRequestDto input);
	Task<TransportMemberDto> GetAsync(Guid memberId, Guid academicMediumId);
	Task<PagedResultDto<TransportMemberRouteAssignmentDto>> GetAssignmentsAsync(Guid memberId, TransportMemberAssignmentListRequestDto input);
	Task<TransportMemberDto> ChangeStatusAsync(Guid memberId, Guid academicMediumId, ChangeTransportMemberStatusDto input);
}
