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
[Route("api/attendance/leave-types")]
[Authorize]
public class LeaveTypeController : AbpControllerBase
{
	private readonly ILeaveTypeAppService _leaveTypeAppService;

	public LeaveTypeController(ILeaveTypeAppService leaveTypeAppService)
	{
		_leaveTypeAppService = leaveTypeAppService;
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.View)]
	public virtual Task<PagedResultDto<LeaveTypeDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _leaveTypeAppService.GetListAsync(input);
	}

	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.View)]
	public virtual Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		return _leaveTypeAppService.GetAllActiveAsync(input);
	}

	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.View)]
	public virtual Task<LeaveTypeDto> GetAsync(Guid id)
	{
		return _leaveTypeAppService.GetAsync(id);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.Add)]
	public virtual Task<LeaveTypeDto> CreateAsync([FromBody] CreateLeaveTypeDto input)
	{
		return _leaveTypeAppService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.Add)]
	public virtual Task<LeaveTypeDto> UpdateAsync(Guid id, [FromBody] UpdateLeaveTypeDto input)
	{
		return _leaveTypeAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_SettingLeaveType.Delete)]
	public virtual Task DeleteAsync(Guid id)
	{
		return _leaveTypeAppService.DeleteAsync(id);
	}
}