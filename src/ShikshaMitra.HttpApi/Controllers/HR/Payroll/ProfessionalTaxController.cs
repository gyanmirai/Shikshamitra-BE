using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/professional-taxes")]
[Authorize]
public class ProfessionalTaxController : AbpControllerBase
{
	private readonly IProfessionalTaxAppService _professionalTaxAppService;

	public ProfessionalTaxController(IProfessionalTaxAppService professionalTaxAppService)
	{
		_professionalTaxAppService = professionalTaxAppService;
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.View)]
	[ProducesResponseType(typeof(PagedResultDto<ProfessionalTaxDto>), 200)]
	public virtual async Task<PagedResultDto<ProfessionalTaxDto>> GetListAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 10, 100);

		return await _professionalTaxAppService.GetListAsync(new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		});
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.View)]
	[ProducesResponseType(typeof(ProfessionalTaxDto), 200)]
	[ProducesResponseType(404)]
	public virtual async Task<ProfessionalTaxDto> GetAsync(Guid id)
	{
		return await _professionalTaxAppService.GetAsync(id);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.Add)]
	[ProducesResponseType(typeof(ProfessionalTaxDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public virtual async Task<ActionResult<ProfessionalTaxDto>> CreateAsync(
		[FromBody] CreateUpdateProfessionalTaxDto input)
	{
		var created = await _professionalTaxAppService.CreateAsync(input);
		return StatusCode(201, created);
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.Add)]
	[ProducesResponseType(typeof(ProfessionalTaxDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<ProfessionalTaxDto> UpdateAsync(
		Guid id,
		[FromBody] CreateUpdateProfessionalTaxDto input)
	{
		return await _professionalTaxAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsProfessionalTax.Delete)]
	[ProducesResponseType(204)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id)
	{
		await _professionalTaxAppService.DeleteAsync(id);
		return NoContent();
	}
}
