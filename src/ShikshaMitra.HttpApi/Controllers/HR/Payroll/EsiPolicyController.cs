using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app"), RemoteService(Name = "Default"), Authorize]
[Route("api/payroll/statutory-schemes/esi/policies")]
public class EsiPolicyController(IEsiPolicyAppService service) : AbpControllerBase
{
	[HttpGet] public Task<List<EsiPolicyDto>> GetAsync() => service.GetListAsync();
	[HttpGet("options")] public Task<List<EsiPolicyDto>> GetOptionsAsync() => service.GetOptionsAsync();
	[HttpPost] public Task<EsiPolicyDto> CreateAsync(CreateEsiPolicyDto input) => service.CreateAsync(input);
	[HttpPost("{id:guid}/versions")] public Task<EsiPolicyDto> AddVersionAsync(Guid id, EsiPolicyVersionInput input) => service.AddVersionAsync(id, input);
	[HttpPost("{id:guid}/archive")] public Task ArchiveAsync(Guid id, [FromBody] ArchiveEsiPolicyDto input) => service.ArchiveAsync(id, input.ConcurrencyStamp);
}
public class ArchiveEsiPolicyDto { public string ConcurrencyStamp { get; set; } = string.Empty; }
