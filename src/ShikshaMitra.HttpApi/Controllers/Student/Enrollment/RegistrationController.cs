using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Models;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles student registration listing, form assignment, and search.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationController(
	IStudentRegistrationQueryAppService registrationAppService,
	IStudentInquiryAppService inquiryAppService) : AbpControllerBase
{
	/// <summary>
	/// Assigns a registration form to a student inquiry.
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
		await inquiryAppService.AssignRegistrationFormAsync(studentId, issueFormDto);
		return Result.Success();
	}

	/// <summary>
	/// Gets a paginated list of students who have been issued registration forms.
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum number of records to return</param>
	/// <param name="searchTerm">Optional search term to filter results</param>
	/// <returns>Paginated list of students with registration forms</returns>
	[HttpGet("forms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<PagedResultDto<StudentDto>>> GetRegistrationFormsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var result = await registrationAppService.GetRegistrationFormsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(result);
	}

	/// <summary>
	/// Gets a paginated list of students in registration status.
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum number of records to return</param>
	/// <param name="searchTerm">Optional search term to filter results</param>
	/// <returns>Paginated list of registered students</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<PagedResultDto<StudentDto>>> GetRegistrationsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var result = await registrationAppService.GetRegistrationsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(result);
	}

	/// <summary>
	/// Searches for students by name (first, middle, or last name).
	/// Returns a lightweight list of matching students with their full name and class details.
	/// </summary>
	/// <param name="searchTerm">The term to search for within student names (case-insensitive)</param>
	/// <returns>List of matching students with id and formatted name</returns>
	[HttpGet("search")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Default)]
	public async Task<Result<List<StudentSearchResultDto>>> SearchStudentsByNameAsync(
		[FromQuery] string searchTerm)
	{
		var result = await registrationAppService.SearchStudentsByNameAsync(searchTerm);
		return Result.Success(result);
	}
}
