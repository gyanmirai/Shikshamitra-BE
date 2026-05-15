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
[Route("api/payroll/earning-components")]
[Authorize]
public class EarningComponentController : AbpControllerBase
{
	private readonly IEarningComponentAppService _earningComponentAppService;

	public EarningComponentController(IEarningComponentAppService earningComponentAppService)
	{
		_earningComponentAppService = earningComponentAppService;
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(EarningComponentPaginatedResponseDto), 200)]
	public virtual async Task<EarningComponentPaginatedResponseDto> GetPaginatedAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var result = await _earningComponentAppService.GetFilteredListAsync(
			(page - 1) * pageSize, pageSize, search);

		return EarningComponentContractMapper.ToPaginatedResponse(result, page, pageSize);
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(EarningComponentResponseDto), 200)]
	[ProducesResponseType(404)]
	public virtual async Task<EarningComponentResponseDto> GetAsync(Guid id)
	{
		var dto = await _earningComponentAppService.GetAsync(id);
		return EarningComponentContractMapper.ToResponseDto(dto);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ComponentCreateResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public virtual async Task<ActionResult<ComponentCreateResponseDto>> CreateAsync(
		[FromBody] EarningComponentCreateRequestDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var createDto = EarningComponentContractMapper.ToCreateDto(input);
		var createdComponent = await _earningComponentAppService.CreateAsync(createDto);
		return StatusCode(201, new ComponentCreateResponseDto
		{
			Id = createdComponent.Id,
			CreatedAt = DateTime.UtcNow
		});
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual async Task<IActionResult> UpdateAsync(Guid id,
		[FromBody] EarningComponentUpdateRequestDto input)
	{
		var existingComponent = await _earningComponentAppService.GetAsync(id);
		var updateDto = EarningComponentContractMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _earningComponentAppService.UpdateAsync(id, updateDto);
		return Ok();
	}

	[HttpPost("{id}/update-impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(PayrollComponentUpdateImpactPreviewDto), 200)]
	public virtual async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		[FromBody] EarningComponentUpdateRequestDto input,
		[FromQuery] bool includeEmployees = false,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 20)
	{
		var existingComponent = await _earningComponentAppService.GetAsync(id);
		var updateDto = EarningComponentContractMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		return await _earningComponentAppService.PreviewUpdateImpactAsync(
			id,
			updateDto,
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	[HttpGet("{id}/delete-impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	[ProducesResponseType(typeof(PayrollComponentUpdateImpactPreviewDto), 200)]
	public virtual async Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		[FromQuery] bool includeEmployees = false,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 20)
	{
		return await _earningComponentAppService.PreviewDeleteImpactAsync(
			id,
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	[ProducesResponseType(204)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] string? previewHash = null)
	{
		await _earningComponentAppService.DeleteConfirmedAsync(id, previewHash ?? string.Empty);
		return NoContent();
	}
}
