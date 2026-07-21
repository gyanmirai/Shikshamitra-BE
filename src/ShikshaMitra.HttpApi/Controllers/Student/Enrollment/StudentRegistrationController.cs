using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Enrollments;

[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class StudentRegistrationController(
	IStudentRegistrationQueryAppService queryAppService,
	IStudentGeneralInfoAppService generalInfoAppService,
	IStudentFamilyInfoAppService familyInfoAppService,
	IStudentBankDetailsAppService bankDetailsAppService,
	IStudentPreviousSchoolDetailsAppService previousSchoolDetailsAppService,
	IStudentAdditionalDetailsAppService additionalDetailsAppService,
	IStudentCommunicationInfoAppService communicationInfoAppService,
	IStudentMedicalRecordsAppService medicalRecordsAppService,
	IStudentDocumentAppService documentAppService) : AbpControllerBase
{
	[HttpGet]
	public async Task<Result<PagedResultDto<StudentDto>>> GetRegistrationsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10)
	{
		return Result.Success(await queryAppService.GetRegistrationsByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount));
	}

	[HttpGet("search")]
	public async Task<Result<List<StudentSearchResultDto>>> SearchAsync(
		[FromQuery] string searchTerm,
		[FromQuery] int maxResultCount = 20)
	{
		return Result.Success(await queryAppService.SearchStudentsByNameAsync(searchTerm, maxResultCount));
	}

	[HttpPost("general-details")]
	public async Task<Result<StudentGeneralDetailsDto>> CreateDirectRegistrationAsync(
		[FromBody] CreateDirectRegistrationDto input)
	{
		return Result.Success(await generalInfoAppService.CreateDirectRegistrationAsync(input));
	}

	[HttpGet("{studentId:guid}/general-details")]
	public async Task<Result<StudentGeneralDetailsDto>> GetGeneralDetailsAsync(Guid studentId)
	{
		return Result.Success(await generalInfoAppService.GetRegistrationGeneralDetailsByIdAsync(studentId));
	}

	[HttpPut("{studentId:guid}/general-details")]
	public async Task<Result<StudentGeneralDetailsDto>> UpdateGeneralDetailsAsync(
		Guid studentId,
		[FromBody] UpdateGeneralInfoDto input)
	{
		return Result.Success(await generalInfoAppService.UpdateGeneralInfoAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/family-details")]
	public async Task<Result<StudentFamilyInfoDto>> GetFamilyDetailsAsync(Guid studentId)
	{
		return Result.Success(await familyInfoAppService.GetFamilyInfoAsync(studentId));
	}

	[HttpPut("{studentId:guid}/family-details")]
	public async Task<Result<StudentFamilyInfoDto>> UpdateFamilyDetailsAsync(
		Guid studentId,
		[FromBody] UpdateFamilyInfoDto input)
	{
		return Result.Success(await familyInfoAppService.UpdateFamilyInfoAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/bank-details")]
	public async Task<Result<StudentBankDetailsDto>> GetBankDetailsAsync(Guid studentId)
	{
		return Result.Success(await bankDetailsAppService.GetBankDetailsAsync(studentId));
	}

	[HttpPut("{studentId:guid}/bank-details")]
	public async Task<Result<StudentBankDetailsDto>> UpdateBankDetailsAsync(
		Guid studentId,
		[FromBody] UpdateBankDetailsDto input)
	{
		return Result.Success(await bankDetailsAppService.UpdateBankDetailsAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/previous-school-details")]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> GetPreviousSchoolDetailsAsync(Guid studentId)
	{
		return Result.Success(await previousSchoolDetailsAppService.GetPreviousSchoolDetailsAsync(studentId));
	}

	[HttpPut("{studentId:guid}/previous-school-details")]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> UpdatePreviousSchoolDetailsAsync(
		Guid studentId,
		[FromBody] UpdatePreviousSchoolDetailsDto input)
	{
		return Result.Success(await previousSchoolDetailsAppService.UpdatePreviousSchoolDetailsAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/additional-details")]
	public async Task<Result<StudentAdditionalDetailsDto>> GetAdditionalDetailsAsync(Guid studentId)
	{
		return Result.Success(await additionalDetailsAppService.GetAdditionalDetailsAsync(studentId));
	}

	[HttpPut("{studentId:guid}/additional-details")]
	public async Task<Result<StudentAdditionalDetailsDto>> UpdateAdditionalDetailsAsync(
		Guid studentId,
		[FromBody] UpdateAdditionalDetailsDto input)
	{
		return Result.Success(await additionalDetailsAppService.UpdateAdditionalDetailsAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/communication-details")]
	public async Task<Result<StudentCommunicationInfoDto>> GetCommunicationDetailsAsync(Guid studentId)
	{
		return Result.Success(await communicationInfoAppService.GetCommunicationInfoAsync(studentId));
	}

	[HttpPut("{studentId:guid}/communication-details")]
	public async Task<Result<StudentCommunicationInfoDto>> UpdateCommunicationDetailsAsync(
		Guid studentId,
		[FromBody] UpdateCommunicationInfoDto input)
	{
		return Result.Success(await communicationInfoAppService.UpdateCommunicationInfoAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/medical-records")]
	public async Task<Result<StudentMedicalRecordsDto>> GetMedicalRecordsAsync(Guid studentId)
	{
		return Result.Success(await medicalRecordsAppService.GetMedicalRecordsAsync(studentId));
	}

	[HttpPut("{studentId:guid}/medical-records")]
	public async Task<Result<StudentMedicalRecordsDto>> UpdateMedicalRecordsAsync(
		Guid studentId,
		[FromBody] UpdateMedicalRecordsDto input)
	{
		return Result.Success(await medicalRecordsAppService.UpdateMedicalRecordsAsync(studentId, input));
	}

	[HttpPost("{studentId:guid}/documents")]
	public async Task<Result<UploadDocumentResponseDto>> UploadDocumentAsync(
		Guid studentId,
		[FromBody] ProcessedStudentDocumentDto input)
	{
		return Result.Success(await documentAppService.UploadDocumentAsync(studentId, input));
	}

	[HttpGet("{studentId:guid}/documents")]
	public async Task<Result<List<StudentDocumentDto>>> GetDocumentListAsync(Guid studentId)
	{
		return Result.Success(await documentAppService.GetDocumentListAsync(studentId));
	}

	[HttpGet("{studentId:guid}/documents/required-status")]
	public async Task<Result<List<DocumentTypeValidationDto>>> GetRequiredDocumentStatusAsync(Guid studentId)
	{
		return Result.Success(await documentAppService.GetRequiredDocumentsStatusAsync(studentId));
	}

	[HttpGet("{studentId:guid}/documents/{documentId:guid}")]
	public async Task<Result<StudentDocumentDownloadDto>> GetDocumentAsync(Guid studentId, Guid documentId)
	{
		return Result.Success(await documentAppService.GetDocumentAsync(studentId, documentId));
	}

	[HttpDelete("{studentId:guid}/documents/{documentId:guid}")]
	public async Task DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		await documentAppService.DeleteDocumentAsync(studentId, documentId);
	}
}
