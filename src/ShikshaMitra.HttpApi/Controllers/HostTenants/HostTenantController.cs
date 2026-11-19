using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HostTenants;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.HostTenants;

[Authorize]
[Route("api/host/tenants")]
public class HostTenantController(IHostTenantAppService service) : AbpController
{
	[HttpGet]
	public Task<PagedResultDto<HostTenantDto>> GetListAsync([FromQuery] GetHostTenantsInput input) => service.GetListAsync(input);

	[HttpGet("{id:guid}")]
	public Task<HostTenantDto> GetAsync(Guid id) => service.GetAsync(id);

	[HttpPost, DisableAuditing]
	public Task<HostTenantDto> CreateAsync([FromBody] CreateHostTenantInput input) => service.CreateAsync(input);

	[HttpPut("{id:guid}")]
	public Task<HostTenantDto> UpdateAsync(Guid id, [FromBody] UpdateHostTenantInput input) => service.UpdateAsync(id, input);

	[HttpPut("{id:guid}/status")]
	public Task<HostTenantDto> ChangeStatusAsync(Guid id, [FromBody] ChangeHostTenantStatusInput input) => service.ChangeStatusAsync(id, input);
}

[Authorize]
[Route("api/host/session-context")]
public class HostSessionController(IHostSessionAppService service) : AbpController
{
	[HttpGet]
	public Task<HostSessionContextDto> GetAsync() => service.GetAsync();
}
