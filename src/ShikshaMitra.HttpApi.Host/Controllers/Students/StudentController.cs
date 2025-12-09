using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentDetails;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Controllers.Students;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Default)]
[Route("api/students")]
public class StudentController(IStudentInquiryAppService studentInquiryAppService, IStudentRegistrationAppService studentRegistrationAppService,
	IStudentAppService studentAppService) : AbpControllerBase
{
	#region Registration Forms
	[HttpPost("{studentId}/inquiries/registration-forms")]
	public async Task<Result> AssignRegistrationForm(
		Guid studentId,
		[FromBody] IssueRegistrationFormDto issueFormDto)
	{
		await studentInquiryAppService.AssignRegistrationFormAsync(studentId, issueFormDto);

		return Result.Success();
	}

	[HttpGet("registration-forms/{academicMediumId}")]
	public async Task<Result<PagedResultDto<StudentRegistrationFormDto>>> GetRegistrationFormsByMedium(
		Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var registrationForms = await studentRegistrationAppService.GetRegistrationFormsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(registrationForms);
	}
	#endregion

	[HttpGet("registrations/{academicMediumId}")]
	public async Task<Result<PagedResultDto<StudentRegistrationDto>>> GetRegistrationsByMedium(
		Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var registrations = await studentRegistrationAppService.GetRegistrationsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(registrations);
	}

	[HttpGet("{studentId}/general-information")]
	public async Task<Result<StudentGeneralInformationDto>> GetStudentGeneralInformation(Guid studentId)
	{
		var studentGeneralInfo = await studentAppService.GetStudentGeneralInformationAsync(studentId);
		return Result.Success(studentGeneralInfo);
	}

	[HttpPut("{studentId}/general-information")]
	public async Task<Result> UpdateStudentGeneralInformation(
		Guid studentId,
		[FromBody] CreateUpdateStudentGeneralInformationDto updateStudentGeneralInfoDto)
	{
		var updatedGeneralInfo = await studentAppService.UpdateStudentGeneralInformationAsync(studentId, updateStudentGeneralInfoDto);
		return Result.Success(updatedGeneralInfo);
	}
}
