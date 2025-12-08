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
/// API controller for managing student communication/address information during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/communication")]
public class StudentEnrollmentCommunicationController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentCommunicationController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets communication/address information for a student in Registration status.
	/// Includes local address, permanent address, and emergency address details.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Communication information including all address details</returns>
	/// <response code="200">Returns the communication information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentCommunicationInfoDto>> GetCommunicationInfoAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetCommunicationInfoAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates communication/address information for a student in Registration status.
	/// This includes local address, permanent address, and emergency address with contact details.
	/// Updates the registration state to Communication upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Communication information data</param>
	/// <returns>Updated communication information</returns>
	/// <response code="200">Communication information updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentCommunicationInfoDto>> UpdateCommunicationInfoAsync(
		Guid studentId,
		[FromBody] UpdateCommunicationInfoDto input)
	{
		var result = await _registrationAppService.UpdateCommunicationInfoAsync(studentId, input);
		return Result.Success(result);
	}
}
