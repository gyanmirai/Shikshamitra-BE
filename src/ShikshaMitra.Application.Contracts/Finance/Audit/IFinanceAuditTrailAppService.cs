using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Audit;

public interface IFinanceAuditTrailAppService : IApplicationService
{
	Task<PagedResultDto<FinanceAuditEventDto>> GetListAsync(FinanceAuditEventListRequestDto input);
	Task<PagedResultDto<FinanceAuditEventDto>> GetStudentEventsAsync(Guid studentId, FinanceAuditEventListRequestDto input);
	Task<PagedResultDto<FinanceAuditEventDto>> GetEntityEventsAsync(string entityType, Guid entityId, FinanceAuditEventListRequestDto input);
	Task<FinanceAuditCorrelationDto> GetByCorrelationAsync(string correlationId, PagedAndSortedResultRequestDto input);
	Task<FinanceAuditEventDto> GetAsync(Guid id);
	Task<ListResultDto<FinanceAuditStudentLookupDto>> LookupStudentsForAuditAsync(
		FinanceAuditStudentLookupRequestDto input);
}
