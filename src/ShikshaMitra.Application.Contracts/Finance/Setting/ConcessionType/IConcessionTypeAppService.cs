using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos;
using ShikshaMitra.Finance.Setting.ConcessionType.Dtos;
using Volo.Abp.Application.Dtos; // For MutationResponse/MessageResponse if reused, otherwise standard DTO

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType;

public interface IConcessionTypeAppService : IApplicationService
{
    Task CreateAsync(Guid academicYearId, ConcessionTypeRequest concessionTypeCreateRequest);
    Task<PagedResultDto<ConcessionTypeResponse>> GetListAsync(Guid academicYearId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null);
    Task<ConcessionTypeResponse> GetAsync(Guid id);
    Task UpdateAsync(Guid id, ConcessionTypeRequest concessionTypeUpdateRequest);
    Task DeleteAsync(Guid id);
}
