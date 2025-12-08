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
/// API controller for managing student additional details during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/additional-details")]
public class StudentEnrollmentAdditionalDetailsController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentAdditionalDetailsController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets additional details information for a student in Registration status.
	/// Includes category, BPL status, disability status, emergency contacts, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Additional details information</returns>
	/// <response code="200">Returns the additional details information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentAdditionalDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentAdditionalDetailsDto>> GetAdditionalDetailsAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetAdditionalDetailsAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates additional details for a student in Registration status.
	/// This includes category, BPL status, disability information, emergency contacts, and family composition.
	/// Updates the registration state to AdditionalDetails upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Additional details data</param>
	/// <returns>Updated additional details information</returns>
	/// <response code="200">Additional details updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentAdditionalDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentAdditionalDetailsDto>> UpdateAdditionalDetailsAsync(
		Guid studentId,
		[FromBody] UpdateAdditionalDetailsDto input)
	{
		var result = await _registrationAppService.UpdateAdditionalDetailsAsync(studentId, input);
		return Result.Success(result);
	}
}
