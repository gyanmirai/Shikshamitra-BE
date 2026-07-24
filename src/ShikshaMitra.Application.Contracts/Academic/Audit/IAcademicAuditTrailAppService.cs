using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Audit;

public interface IAcademicAuditTrailAppService : IApplicationService
{
	Task<PagedResultDto<AcademicAuditEventDto>> GetListAsync(AcademicAuditEventListRequestDto input);

	Task<AcademicAuditEventDto> GetAsync(Guid id);

	Task<PagedResultDto<AcademicAuditEventDto>> GetEntityEventsAsync(
		string entityType,
		Guid entityId,
		AcademicAuditEventListRequestDto input);
}
