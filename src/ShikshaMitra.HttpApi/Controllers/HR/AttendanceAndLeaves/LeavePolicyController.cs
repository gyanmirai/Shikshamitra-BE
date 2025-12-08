using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance/leave-policies")]
[Authorize]
public class LeavePolicyController : AbpControllerBase
{
	private readonly ILeavePolicyAppService _leavePolicyAppService;

	public LeavePolicyController(ILeavePolicyAppService leavePolicyAppService)
	{
		_leavePolicyAppService = leavePolicyAppService;
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.View)]
	public virtual Task<PagedResultDto<LeavePolicyDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _leavePolicyAppService.GetListAsync(input);
	}

	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.View)]
	public virtual Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		return _leavePolicyAppService.GetAllActiveAsync(input);
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.View)]
	public virtual Task<LeavePolicyDto> GetAsync(Guid id)
	{
		return _leavePolicyAppService.GetAsync(id);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.Add)]
	public virtual Task<LeavePolicyDto> CreateAsync([FromBody] CreateLeavePolicyDto input)
	{
		return _leavePolicyAppService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.Add)]
	public virtual Task<LeavePolicyDto> UpdateAsync(Guid id, [FromBody] UpdateLeavePolicyDto input)
	{
		return _leavePolicyAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeavePolicy.Delete)]
	public virtual Task DeleteAsync(Guid id)
	{
		return _leavePolicyAppService.DeleteAsync(id);
	}
}