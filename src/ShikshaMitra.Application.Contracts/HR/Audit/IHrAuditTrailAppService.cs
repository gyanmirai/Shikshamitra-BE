using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Audit;

public interface IHrAuditTrailAppService : IApplicationService
{
	Task<PagedResultDto<HrAuditEventDto>> GetListAsync(HrAuditEventListRequestDto input);
	Task<PagedResultDto<HrAuditEventDto>> GetEntityEventsAsync(string entityType, Guid entityId, HrAuditEventListRequestDto input);
}
