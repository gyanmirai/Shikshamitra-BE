using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.Search;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employees Controller (OpenAPI contract compliant)
/// Provides read-only access to employee data
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class EmployeesController : AbpControllerBase
{
	private readonly IEmployeeSearchAppService _searchAppService;
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public EmployeesController(
		IEmployeeSearchAppService searchAppService,
		IEmployeePersonalInfoAppService personalInfoAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_searchAppService = searchAppService;
		_personalInfoAppService = personalInfoAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Get all employees with pagination and filtering (OpenAPI contract)
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(EmployeeListResponse), StatusCodes.Status200OK)]
	public virtual async Task<EmployeeListResponse> GetAllEmployeesAsync(
		[FromQuery] Guid? mediumStageId = null,
		[FromQuery] string? status = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 50)] int pageSize = 10,
		[FromQuery] bool? isTeaching = null,
		[FromQuery] Guid? roleId = null,
		[FromQuery] string? search = null)
	{
		pageSize = Math.Min(pageSize, 50);

		var input = new GetEmployeeListInput
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize,
			Status = status,
			IsTeaching = isTeaching,
			RoleId = roleId,
			Search = search
		};

		return await _searchAppService.GetFilteredListAsync(input);
	}

	/// <summary>
	/// Get employee by ID (OpenAPI contract)
	/// </summary>
	[HttpGet("{employeeId}")]
	[ProducesResponseType(typeof(EmployeeDetails), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<EmployeeDetails> GetEmployeeByIdAsync(Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);

		var employee = await _personalInfoAppService.GetWithDetailsAsync(employeeId);
		return MapToEmployeeDetails(employee);
	}

	private static EmployeeDetails MapToEmployeeDetails(EmployeeDto employee)
	{
		return new EmployeeDetails
		{
			Id = employee.Id,
			RoleName = employee.Role?.Name,
			FullName = employee.EmployeeName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = employee.GenderType?.ToString(),
			Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			IsTeaching = employee.EmployeeGroup == Common.EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Status = (int)(employee.EmployeeStatusType ?? Common.EmployeeStatusType.Active)
		};
	}
}
