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
/// API controller for managing student previous school details during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/previous-school")]
public class StudentEnrollmentPreviousSchoolController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentPreviousSchoolController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets previous school details information for a student in Registration status.
	/// Includes school name, board, TC number, academic performance, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Previous school details information</returns>
	/// <response code="200">Returns the previous school details information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> GetPreviousSchoolDetailsAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetPreviousSchoolDetailsAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates previous school details for a student in Registration status.
	/// This includes school information, academic board, TC number, grades, and reason for transfer.
	/// Updates the registration state to PrevSchoolDetails upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Previous school details data</param>
	/// <returns>Updated previous school details information</returns>
	/// <response code="200">Previous school details updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> UpdatePreviousSchoolDetailsAsync(
		Guid studentId,
		[FromBody] UpdatePreviousSchoolDetailsDto input)
	{
		var result = await _registrationAppService.UpdatePreviousSchoolDetailsAsync(studentId, input);
		return Result.Success(result);
	}
}
