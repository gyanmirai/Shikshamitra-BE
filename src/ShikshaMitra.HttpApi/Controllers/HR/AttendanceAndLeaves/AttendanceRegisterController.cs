using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/attendance/register")]
[Authorize]
public class AttendanceRegisterController(IAttendanceRegisterAppService attendanceRegisterAppService) : AbpControllerBase
{
	[HttpGet]
	[ProducesResponseType(typeof(AttendanceRegisterResponse), StatusCodes.Status200OK)]
	public virtual Task<AttendanceRegisterResponse> GetAsync(
		[FromQuery, Required] string startDate,
		[FromQuery, Required] string endDate,
		[FromQuery] Guid? educationMediumStageId = null,
		[FromQuery] Guid? employeeId = null,
		[FromQuery] EmployeeGroupType? employeeGroup = null,
		[FromQuery] string? department = null,
		[FromQuery] Guid? roleId = null,
		[FromQuery] bool? isTeaching = null,
		[FromQuery] EmployeeAttendanceStatusType? status = null,
		[FromQuery] AttendanceRegisterPayrollState payrollState = AttendanceRegisterPayrollState.All,
		[FromQuery] string? search = null,
		[FromQuery, Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery, Range(1, 500)] int maxResultCount = 50)
	{
		return attendanceRegisterAppService.GetAsync(new GetAttendanceRegisterInput
		{
			StartDate = startDate,
			EndDate = endDate,
			EducationMediumStageId = educationMediumStageId,
			EmployeeId = employeeId,
			EmployeeGroup = employeeGroup,
			Department = department,
			RoleId = roleId,
			IsTeaching = isTeaching,
			Status = status,
			PayrollState = payrollState,
			Search = search,
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpGet("grouped")]
	[ProducesResponseType(typeof(GroupedAttendanceRegisterResponse), StatusCodes.Status200OK)]
	public virtual Task<GroupedAttendanceRegisterResponse> GetGroupedAsync(
		[FromQuery, Required] string startDate,
		[FromQuery, Required] string endDate,
		[FromQuery] Guid? educationMediumStageId = null,
		[FromQuery] Guid? employeeId = null,
		[FromQuery] EmployeeGroupType? employeeGroup = null,
		[FromQuery] string? department = null,
		[FromQuery] Guid? roleId = null,
		[FromQuery] bool? isTeaching = null,
		[FromQuery] EmployeeAttendanceStatusType? status = null,
		[FromQuery] AttendanceRegisterPayrollState payrollState = AttendanceRegisterPayrollState.All,
		[FromQuery] AttendanceRegisterExceptionFilter exceptionFilter = AttendanceRegisterExceptionFilter.All,
		[FromQuery] string? search = null,
		[FromQuery, Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery, Range(1, 500)] int maxResultCount = 50)
	{
		return attendanceRegisterAppService.GetGroupedAsync(new GetGroupedAttendanceRegisterInput
		{
			StartDate = startDate,
			EndDate = endDate,
			EducationMediumStageId = educationMediumStageId,
			EmployeeId = employeeId,
			EmployeeGroup = employeeGroup,
			Department = department,
			RoleId = roleId,
			IsTeaching = isTeaching,
			Status = status,
			PayrollState = payrollState,
			ExceptionFilter = exceptionFilter,
			Search = search,
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpPost("adjust")]
	[ProducesResponseType(typeof(AttendanceRegisterAdjustmentResult), StatusCodes.Status200OK)]
	public virtual Task<AttendanceRegisterAdjustmentResult> AdjustAsync(
		[FromBody, Required] AttendanceRegisterAdjustRequest input)
	{
		return attendanceRegisterAppService.AdjustAsync(input);
	}

	[HttpPost("bulk-adjust")]
	[ProducesResponseType(typeof(AttendanceRegisterAdjustmentResult), StatusCodes.Status200OK)]
	public virtual Task<AttendanceRegisterAdjustmentResult> BulkAdjustAsync(
		[FromBody, Required] AttendanceRegisterBulkAdjustRequest input)
	{
		return attendanceRegisterAppService.BulkAdjustAsync(input);
	}
}
