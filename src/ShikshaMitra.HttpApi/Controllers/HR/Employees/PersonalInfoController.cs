using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee personal information management controller
/// Handles CRUD operations for employee personal data in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class PersonalInfoController : AbpControllerBase
{
	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;

	public PersonalInfoController(IEmployeePersonalInfoAppService personalInfoAppService)
	{
		_personalInfoAppService = personalInfoAppService;
	}

	/// <summary>
	/// Create employee personal information
	/// </summary>
	[HttpPost("personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<PersonalInfoResponse> CreateAsync(
		[FromBody, Required] PersonalInfoRequest request,
		[FromQuery] Guid? educationMediumStageId = null)
	{
		ValidateRequest(request);

		var mediumStageId = educationMediumStageId ?? await GetDefaultEducationMediumStageIdAsync();
		var createDto = PersonalInfoContractMapper.ToCreateDto(request, mediumStageId);
		var result = await _personalInfoAppService.CreateAsync(createDto);

		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Get employee personal information
	/// </summary>
	[HttpGet("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		var result = await _personalInfoAppService.GetAsync(employeeId);
		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Update employee personal information
	/// </summary>
	[HttpPut("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PersonalInfoRequest request,
		[FromQuery] Guid? educationMediumStageId = null)
	{
		ValidateRequest(request);

		var mediumStageId = educationMediumStageId ?? await GetEmployeeEducationMediumStageIdAsync(employeeId);
		var updateDto = PersonalInfoContractMapper.ToUpdateDto(request, mediumStageId);
		var result = await _personalInfoAppService.UpdateAsync(employeeId, updateDto);

		return PersonalInfoContractMapper.ToResponse(result);
	}

	#region Validation

	private void ValidateRequest(PersonalInfoRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.FirstName))
		{
			throw new UserFriendlyException("First name is required.");
		}

		if (string.IsNullOrWhiteSpace(request.LastName))
		{
			throw new UserFriendlyException("Last name is required.");
		}

		if (string.IsNullOrWhiteSpace(request.MobileNo))
		{
			throw new UserFriendlyException("Mobile number is required.");
		}

		if (request.RoleId == Guid.Empty)
		{
			throw new UserFriendlyException("Role ID is required.");
		}

		if (request.DepartmentId == Guid.Empty)
		{
			throw new UserFriendlyException("Department ID is required.");
		}

		if (request.ShiftId == Guid.Empty)
		{
			throw new UserFriendlyException("Shift ID is required.");
		}

		// Validate age >= 18 at joining date
		if (request.Dob.HasValue)
		{
			var age = request.Doj.Year - request.Dob.Value.Year;
			if (request.Doj < request.Dob.Value.AddYears(age))
			{
				age--;
			}

			if (age < 18)
			{
				throw new UserFriendlyException("Employee must be at least 18 years old at the time of joining.");
			}
		}

		if (request.Dob.HasValue && request.Dob.Value >= DateTime.UtcNow)
		{
			throw new UserFriendlyException("Date of birth cannot be in the future.");
		}

		if (request.Dob.HasValue && request.Doj < request.Dob.Value)
		{
			throw new UserFriendlyException("Date of joining cannot be before date of birth.");
		}
	}

	private Task<Guid> GetDefaultEducationMediumStageIdAsync()
	{
		throw new UserFriendlyException("Education Medium Stage ID is required. Please provide it as a query parameter.");
	}

	private async Task<Guid> GetEmployeeEducationMediumStageIdAsync(Guid employeeId)
	{
		var employee = await _personalInfoAppService.GetAsync(employeeId);
		return employee.EducationMediumStageId;
	}

	#endregion
}
