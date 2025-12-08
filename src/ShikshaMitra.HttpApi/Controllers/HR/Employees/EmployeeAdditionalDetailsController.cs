using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/app/employee")]
[Authorize]
public class EmployeeAdditionalDetailsController : AbpControllerBase
{
	private readonly IEmployeeAppService _employeeAppService;

	public EmployeeAdditionalDetailsController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// Gets additional details for an employee
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	/// <returns>Employee additional details</returns>
	[HttpGet("{employeeId}/additional-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<EmployeeAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid employeeId)
	{
		return await _employeeAppService.GetAdditionalDetailsAsync(employeeId);
	}

	/// <summary>
	/// Updates additional details for an employee
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	/// <param name="input">Updated additional details</param>
	/// <returns>Updated additional details</returns>
	[HttpPut("{employeeId}/additional-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeeAdditionalDetailsDto> UpdateAdditionalDetailsAsync(
		Guid employeeId,
		[FromBody] UpdateEmployeeAdditionalDetailsDto input)
	{
		return await _employeeAppService.UpdateAdditionalDetailsAsync(employeeId, input);
	}
}
