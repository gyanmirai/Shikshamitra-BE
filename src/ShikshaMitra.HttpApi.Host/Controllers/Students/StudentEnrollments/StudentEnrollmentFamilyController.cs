using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student family information during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/family")]
public class StudentEnrollmentFamilyController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentFamilyController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets family information for a student in Registration status.
	/// Includes father, mother, guardian, and sibling details.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Family information including all parent/guardian and sibling details</returns>
	/// <response code="200">Returns the family information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentFamilyInfoDto>> GetFamilyInfoAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetFamilyInfoAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates family information for a student in Registration status.
	/// This includes father, mother, guardian details and sibling information.
	/// Updates the registration state to Family upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Family information data</param>
	/// <returns>Updated family information</returns>
	/// <response code="200">Family information updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentFamilyInfoDto>> UpdateFamilyInfoAsync(
		Guid studentId,
		[FromBody] UpdateFamilyInfoDto input)
	{
		var result = await _registrationAppService.UpdateFamilyInfoAsync(studentId, input);
		return Result.Success(result);
	}
}
