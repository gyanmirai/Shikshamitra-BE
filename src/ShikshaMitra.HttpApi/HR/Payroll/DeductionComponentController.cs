using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// API Controller for managing deduction components.
/// Implements OpenAPI contract-compliant endpoints.
/// </summary>
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

	#region OpenAPI Contract-Compliant Endpoints

	/// <summary>
	/// Get paginated list of deduction components (OpenAPI contract)
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(DeductionComponentPaginatedResponseDto), 200)]
	public virtual async Task<DeductionComponentPaginatedResponseDto> GetPaginatedAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
	{
		var result = await _deductionComponentAppService.GetListAsync(new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		});

		return DeductionComponentOpenApiMapper.ToPaginatedResponse(result, page, pageSize);
	}

	/// <summary>
	/// Create a new deduction component (OpenAPI contract)
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ComponentCreateResponseDto), 201)]
	public virtual async Task<ActionResult<ComponentCreateResponseDto>> CreateAsync([FromBody] PayrollComponentCreateRequestDto input)
	{
		var createDto = DeductionComponentOpenApiMapper.ToCreateDto(input);
		var createdComponent = await _deductionComponentAppService.CreateAsync(createDto);
		return StatusCode(201, new ComponentCreateResponseDto
		{
			Id = createdComponent.Id,
			CreatedAt = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Update an existing deduction component (OpenAPI contract)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(200)]
	public virtual async Task<IActionResult> UpdateAsync(Guid id, [FromBody] PayrollComponentCreateRequestDto input)
	{
		var existingComponent = await _deductionComponentAppService.GetAsync(id);
		var updateDto = DeductionComponentOpenApiMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _deductionComponentAppService.UpdateAsync(id, updateDto);
		return Ok();
	}

	/// <summary>
	/// Delete a deduction component (OpenAPI contract)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	[ProducesResponseType(204)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id)
	{
		await _deductionComponentAppService.DeleteAsync(id);
		return NoContent();
	}

		#endregion
	}
