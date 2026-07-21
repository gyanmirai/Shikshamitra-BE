using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/finance/collection-policies/exception-windows")]
[Authorize]
public class CollectionPolicyExceptionWindowsController(
	ICollectionPolicyExceptionWindowAppService appService
) : AbpControllerBase
{
	[HttpPost]
	public Task<CollectionPolicyExceptionWindowDto> CreateAsync([FromBody] CreateCollectionPolicyExceptionWindowDto input) =>
		appService.CreateAsync(input);

	[HttpGet]
	public Task<List<CollectionPolicyExceptionWindowDto>> GetListAsync([FromQuery] CollectionPolicyExceptionWindowListRequestDto input) =>
		appService.GetListAsync(input);

	[HttpDelete("{id:guid}")]
	public Task DeleteAsync(Guid id) =>
		appService.DeleteAsync(id);
}
