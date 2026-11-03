using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType;

public interface IConcessionTypeAppService : IApplicationService
{
    Task CreateAsync(Guid academicYearId, ConcessionTypeRequest concessionTypeCreateRequest);
	Task<PagedResultDto<ConcessionTypeResponse>> GetListAsync(Guid academicYearId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null, bool includeInactive = false, bool? isActive = null, string? filters = null);
    Task<ConcessionTypeResponse> GetAsync(Guid id);
	Task<PagedResultDto<ConcessionTypeVersionHistoryItemDto>> GetVersionHistoryAsync(Guid id, int skipCount = 0, int maxResultCount = 50);
	Task<ConcessionTypeDocumentDefaultsResponse> GetDocumentDefaultsAsync();
    Task UpdateAsync(Guid id, ConcessionTypeRequest concessionTypeUpdateRequest);
	Task<ConcessionTypeResponse> UpdateStatusAsync(Guid id, ConcessionTypeStatusUpdateRequest input);
    Task DeleteAsync(Guid id);
}
