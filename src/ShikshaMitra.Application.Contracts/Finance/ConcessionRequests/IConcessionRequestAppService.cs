using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
    Task<ConcessionRequestDto> GetAsync(Guid id);
    Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input);
    Task<ConcessionRequestDto> CreateAsync(CreateUpdateConcessionRequestDto input);
    Task<ConcessionRequestDto> UpdateAsync(Guid id, CreateUpdateConcessionRequestDto input);
    Task DeleteAsync(Guid id);
}
