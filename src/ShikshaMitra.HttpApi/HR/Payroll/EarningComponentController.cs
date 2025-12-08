using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// API Controller for managing earning components
/// </summary>
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

	/// <summary>
	/// Get list of all earning components (paginated)
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	public virtual Task<PagedResultDto<EarningComponentDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _earningComponentAppService.GetListAsync(input);
	}

	/// <summary>
	/// Get all active earning components with pagination support
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	public virtual Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		return _earningComponentAppService.GetAllActiveAsync(input);
	}

	/// <summary>
	/// Get a specific earning component by ID
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	public virtual Task<EarningComponentDto> GetAsync(Guid id)
	{
		return _earningComponentAppService.GetAsync(id);
	}

	/// <summary>
	/// Create a new earning component
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	public virtual Task<EarningComponentDto> CreateAsync([FromBody] CreateEarningComponentDto input)
	{
		return _earningComponentAppService.CreateAsync(input);
	}

	/// <summary>
	/// Update an existing earning component
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	public virtual Task<EarningComponentDto> UpdateAsync(Guid id, [FromBody] UpdateEarningComponentDto input)
	{
		return _earningComponentAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Delete an earning component
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	public virtual Task DeleteAsync(Guid id)
	{
		return _earningComponentAppService.DeleteAsync(id);
	}

	/// <summary>
	/// Activate an earning component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	public virtual Task ActivateAsync(Guid id)
	{
		return _earningComponentAppService.ActivateAsync(id);
	}

	/// <summary>
	/// Deactivate an earning component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	public virtual Task DeactivateAsync(Guid id)
	{
		return _earningComponentAppService.DeactivateAsync(id);
	}
}
