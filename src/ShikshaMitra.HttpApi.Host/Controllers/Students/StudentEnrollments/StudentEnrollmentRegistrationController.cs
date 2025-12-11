using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student registration operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration")]
public class StudentEnrollmentRegistrationController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService _registrationAppService;
	private readonly IStudentInquiryAppService _inquiryAppService;

	public StudentEnrollmentRegistrationController(
		IStudentRegistrationAppService registrationAppService,
		IStudentInquiryAppService inquiryAppService)
	{
		_registrationAppService = registrationAppService;
		_inquiryAppService = inquiryAppService;
	}

	/// <summary>
	/// Assigns a registration form to a student inquiry
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="issueFormDto">Registration form details</param>
	/// <returns>Success result</returns>
	[HttpPost("{studentId}/forms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Default)]
	public async Task<Result> AssignRegistrationFormAsync(
		Guid studentId,
		[FromBody] IssueRegistrationFormDto issueFormDto)
	{
		await _inquiryAppService.AssignRegistrationFormAsync(studentId, issueFormDto);
		return Result.Success();
	}

	/// <summary>
	/// Gets a paginated list of students who have been issued registration forms
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum number of records to return</param>
	/// <param name="searchTerm">Optional search term to filter results</param>
	/// <returns>Paginated list of students with registration forms</returns>
	[HttpGet("forms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<PagedResultDto<StudentRegistrationFormDto>>> GetRegistrationFormsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var result = await _registrationAppService.GetRegistrationFormsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(result);
	}

	/// <summary>
	/// Gets a paginated list of students in registration status
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum number of records to return</param>
	/// <param name="searchTerm">Optional search term to filter results</param>
	/// <returns>Paginated list of registered students</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<PagedResultDto<StudentRegistrationDto>>> GetRegistrationsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var result = await _registrationAppService.GetRegistrationsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates general information for a student and moves them to Registration status.
	/// This endpoint accepts only the General tab information and transitions the student
	/// from RegistrationFormIssued to Registration status upon successful validation.
	/// Can also be used to update an existing registration.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">General information data</param>
	/// <returns>Updated student information with Registration status</returns>
	[HttpPatch("{studentId}/general")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	public async Task<Result<UpdateGeneralInfoOutputDto>> UpdateGeneralInfoAsync(
		Guid studentId,
		[FromBody] UpdateGeneralInfoDto input)
	{
		var result = await _registrationAppService.UpdateGeneralInfoAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Creates a new student directly in Registration status without going through 
	/// the inquiry or registration form issued flow. This allows direct registration 
	/// of students from the Registration screen.
	/// </summary>
	/// <param name="input">General information data including education medium</param>
	/// <returns>Newly created student in Registration status</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	public async Task<Result<UpdateGeneralInfoOutputDto>> CreateDirectRegistrationAsync(
		[FromBody] CreateDirectRegistrationDto input)
	{
		var result = await _registrationAppService.CreateDirectRegistrationAsync(input);
		return Result.Success(result);
	}
}
