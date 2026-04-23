using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionType;

public interface IConcessionTypeAppService : IApplicationService
{
    Task CreateAsync(Guid academicYearId, ConcessionTypeRequest concessionTypeCreateRequest);
    Task<PagedResultDto<ConcessionTypeResponse>> GetListAsync(Guid academicYearId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null);
    Task<ConcessionTypeResponse> GetAsync(Guid id);
    Task UpdateAsync(Guid id, ConcessionTypeRequest concessionTypeUpdateRequest);
    Task DeleteAsync(Guid id);
}
