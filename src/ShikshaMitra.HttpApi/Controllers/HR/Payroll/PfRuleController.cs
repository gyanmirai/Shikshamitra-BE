using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Mappers.HR.Payroll;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/pf-rules")]
[Authorize]
public class PfRuleController : AbpControllerBase
{
	private readonly IProvidentFundAppService _providentFundAppService;

	public PfRuleController(IProvidentFundAppService providentFundAppService)
	{
		_providentFundAppService = providentFundAppService;
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Add)]
	[ProducesResponseType(typeof(PfRuleCreateResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public virtual async Task<ActionResult<PfRuleCreateResponseDto>> CreateAsync(
		[FromBody] CreateUpdateProvidentFundDto input)
	{
		var createdPfRule = await _providentFundAppService.CreateAsync(input);
		return StatusCode(201, PfRuleContractMapper.ToCreateResponse(createdPfRule));
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View)]
	[ProducesResponseType(typeof(PfRuleResponseDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<PfRuleResponseDto> GetAsync(Guid id)
	{
		var pfRule = await _providentFundAppService.GetAsync(id);
		return PfRuleContractMapper.ToPfRuleResponse(pfRule);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View)]
	[ProducesResponseType(typeof(PaginatedPfRuleResponseDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public virtual async Task<PaginatedPfRuleResponseDto> GetListAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 10, 100);

		var input = new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		};

		var result = await _providentFundAppService.GetFilteredListAsync(
			input.SkipCount,
			input.MaxResultCount,
			search);

		return PfRuleContractMapper.ToPaginatedResponse(result, page, pageSize);
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Add)]
	[ProducesResponseType(typeof(PfRuleUpdateResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<PfRuleUpdateResponseDto> UpdateAsync(Guid id,
		[FromBody] CreateUpdateProvidentFundDto input)
	{
		var updatedPfRule = await _providentFundAppService.UpdateAsync(id, input);
		return PfRuleContractMapper.ToUpdateResponse(updatedPfRule);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Delete)]
	[ProducesResponseType(204)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id)
	{
		await _providentFundAppService.DeleteAsync(id);
		return NoContent();
	}
}
