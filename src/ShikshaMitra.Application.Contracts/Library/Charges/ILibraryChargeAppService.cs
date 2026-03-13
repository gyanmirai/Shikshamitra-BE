using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Charges;

public interface ILibraryChargeAppService : IApplicationService
{
    Task<GenericCreatedResponse> CreateAsync(CreateChargeRequest input);
    Task<PaginatedResponse<ChargeListItemDto>> GetListAsync(int page = 1, int pageSize = 20);
    Task<ChargeDetailDto> GetAsync(Guid id);
    Task<GenericUpdatedResponse> UpdateAsync(Guid id, UpdateChargeRequest input);
    Task DeleteAsync(Guid id);
}
