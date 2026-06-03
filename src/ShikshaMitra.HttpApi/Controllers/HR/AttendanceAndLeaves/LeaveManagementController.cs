using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HttpApi.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/leave-management")]
[Authorize]
public partial class LeaveManagementController : AbpControllerBase
{
	private const string ExcelContentType =
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	private readonly ILeaveManagementAppService _leaveManagementAppService;

	public LeaveManagementController(ILeaveManagementAppService leaveManagementAppService)
	{
		_leaveManagementAppService = leaveManagementAppService;
	}

	private static EmployeeLeaveStatusType? ParseStatus(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return null;
		}

		return Enum.TryParse<EmployeeLeaveStatusType>(status.Trim(), true, out var parsed)
			? parsed
			: throw new BusinessException(HRErrorCodes.LeaveInvalidStatus);
	}
}
