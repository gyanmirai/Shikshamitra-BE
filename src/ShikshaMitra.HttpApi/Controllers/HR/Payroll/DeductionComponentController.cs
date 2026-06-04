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
[Route("api/payroll/deduction-components")]
[Authorize]
public class DeductionComponentController : AbpControllerBase
{
	private readonly IDeductionComponentAppService _deductionComponentAppService;

	public DeductionComponentController(IDeductionComponentAppService deductionComponentAppService)
	{
		_deductionComponentAppService = deductionComponentAppService;
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(DeductionComponentPaginatedResponseDto), 200)]
	public virtual async Task<DeductionComponentPaginatedResponseDto> GetPaginatedAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var result = await _deductionComponentAppService.GetFilteredListAsync(
			(page - 1) * pageSize, pageSize, search);

		return DeductionComponentContractMapper.ToPaginatedResponse(result, page, pageSize);
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(DeductionComponentResponseDto), 200)]
	[ProducesResponseType(404)]
	public virtual async Task<DeductionComponentResponseDto> GetAsync(Guid id)
	{
		var dto = await _deductionComponentAppService.GetAsync(id);
		return DeductionComponentContractMapper.ToResponseDto(dto);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ComponentCreateResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public virtual async Task<ActionResult<ComponentCreateResponseDto>> CreateAsync(
		[FromBody] DeductionComponentCreateRequestDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var createDto = DeductionComponentContractMapper.ToCreateDto(input);
		var createdComponent = await _deductionComponentAppService.CreateAsync(createDto);
		return StatusCode(201, new ComponentCreateResponseDto
		{
			Id = createdComponent.Id,
			CreatedAt = createdComponent.CreationTime
		});
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual async Task<IActionResult> UpdateAsync(Guid id,
		[FromBody] DeductionComponentUpdateRequestDto input)
	{
		var existingComponent = await _deductionComponentAppService.GetAsync(id);
		var updateDto = DeductionComponentContractMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _deductionComponentAppService.UpdateAsync(id, updateDto);
		return Ok();
	}

	[HttpPost("{id}/update-impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(PayrollComponentUpdateImpactPreviewDto), 200)]
	public virtual async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		[FromBody] DeductionComponentUpdateRequestDto input,
		[FromQuery] bool includeEmployees = false,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 20)
	{
		var existingComponent = await _deductionComponentAppService.GetAsync(id);
		var updateDto = DeductionComponentContractMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		return await _deductionComponentAppService.PreviewUpdateImpactAsync(
			id,
			updateDto,
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	[HttpGet("{id}/delete-impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	[ProducesResponseType(typeof(PayrollComponentUpdateImpactPreviewDto), 200)]
	public virtual async Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		[FromQuery] bool includeEmployees = false,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 20)
	{
		return await _deductionComponentAppService.PreviewDeleteImpactAsync(
			id,
			includeEmployees,
			skipCount,
			maxResultCount);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	[ProducesResponseType(204)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] string? previewHash = null)
	{
		await _deductionComponentAppService.DeleteConfirmedAsync(id, previewHash ?? string.Empty);
		return NoContent();
	}
}
