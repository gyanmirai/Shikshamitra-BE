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
/// API controller for managing student medical records during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/medical-records")]
public class StudentEnrollmentMedicalRecordsController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;

	public StudentEnrollmentMedicalRecordsController(IStudentRegistrationAppService registrationAppService)
	{
		_registrationAppService = registrationAppService;
	}

	/// <summary>
	/// Gets medical records information for a student in Registration status.
	/// Includes weight, height, immunization, allergies, and other health information.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Medical records information</returns>
	/// <response code="200">Returns the medical records information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentMedicalRecordsDto>> GetMedicalRecordsAsync(Guid studentId)
	{
		var result = await _registrationAppService.GetMedicalRecordsAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates medical records for a student in Registration status.
	/// This includes physical measurements, immunization records, allergies, and health conditions.
	/// Updates the registration state to MedicalRecords upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Medical records data</param>
	/// <returns>Updated medical records information</returns>
	/// <response code="200">Medical records updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentMedicalRecordsDto>> UpdateMedicalRecordsAsync(
		Guid studentId,
		[FromBody] UpdateMedicalRecordsDto input)
	{
		var result = await _registrationAppService.UpdateMedicalRecordsAsync(studentId, input);
		return Result.Success(result);
	}
}
