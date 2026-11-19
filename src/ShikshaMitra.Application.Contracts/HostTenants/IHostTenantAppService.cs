using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HostTenants;

public interface IHostTenantAppService : IApplicationService
{
	Task<PagedResultDto<HostTenantDto>> GetListAsync(GetHostTenantsInput input);
	Task<HostTenantDto> GetAsync(Guid id);
	Task<HostTenantDto> CreateAsync(CreateHostTenantInput input);
	Task<HostTenantDto> UpdateAsync(Guid id, UpdateHostTenantInput input);
	Task<HostTenantDto> ChangeStatusAsync(Guid id, ChangeHostTenantStatusInput input);
}

public interface IHostSessionAppService : IApplicationService
{
	Task<HostSessionContextDto> GetAsync();
}
