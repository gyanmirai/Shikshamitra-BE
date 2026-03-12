using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Models;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student registration operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationController : AbpControllerBase
{
	private readonly IStudentRegistrationAppService registrationAppService;
	private readonly IStudentInquiryAppService inquiryAppService;
	private readonly IStudentDocumentAppService documentAppService;

	public RegistrationController(
		IStudentRegistrationAppService registrationAppService,
		IStudentInquiryAppService inquiryAppService,
		IStudentDocumentAppService documentAppService)
	{
		this.registrationAppService = registrationAppService;
		this.inquiryAppService = inquiryAppService;
		this.documentAppService = documentAppService;
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
		await inquiryAppService.AssignRegistrationFormAsync(studentId, issueFormDto);
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
	public async Task<Result<PagedResultDto<StudentEnrollmentListDto>>> GetRegistrationFormsAsync(
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
	/// Gets a paginated list of students in registration status
	/// </summary>
	/// <param name="academicMediumId">The academic medium identifier</param>
	/// <param name="skipCount">Number of records to skip</param>
	/// <param name="maxResultCount">Maximum number of records to return</param>
	/// <param name="searchTerm">Optional search term to filter results</param>
	/// <returns>Paginated list of registered students</returns>
	[HttpGet()]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<PagedResultDto<StudentEnrollmentListDto>>> GetRegistrationsAsync(
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
	/// Gets a single student registration by student ID
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Student registration details including progress</returns>
	[HttpGet("{studentId}/general-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	public async Task<Result<StudentGeneralDetailsDto>> GetRegistrationByIdAsync(Guid studentId)
	{
		var result = await registrationAppService.GetRegistrationGeneralDetailsByIdAsync(studentId);
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
	[HttpPut("{studentId}/general-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	public async Task<Result<StudentGeneralDetailsDto>> UpdateGeneralInfoAsync(
		Guid studentId,
		[FromBody] UpdateGeneralInfoDto input)
	{
		var result = await registrationAppService.UpdateGeneralInfoAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Creates a new student directly in Registration status without going through
	/// the inquiry or registration form issued flow. This allows direct registration
	/// of students from the Registration screen.
	/// </summary>
	/// <param name="input">General information data including education medium</param>
	/// <returns>Newly created student in Registration status</returns>
	[HttpPost("general-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	public async Task<Result<StudentGeneralDetailsDto>> CreateDirectRegistrationAsync(
		[FromBody] CreateDirectRegistrationDto input)
	{
		var result = await registrationAppService.CreateDirectRegistrationAsync(input);
		return Result.Success(result);
	}

	/// <summary>
	/// Uploads a document for a student in registration.
	/// If a document of the same type already exists, it will be replaced.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="request">The upload request containing document type and file</param>
	/// <returns>Upload response with document ID and status</returns>
	/// <response code="200">Document uploaded successfully</response>
	/// <response code="400">Invalid file format, size exceeds limit, or student not in valid status</response>
	/// <response code="404">Student or document type not found</response>
	[HttpPost("{studentId}/documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(Result<UploadDocumentResponseDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<UploadDocumentResponseDto>> UploadDocumentAsync(
		Guid studentId,
		[FromForm] StudentDocumentUploadRequest request)
	{
		// Validate file
		if (request.File == null || request.File.Length == 0)
		{
			return Result.Failure<UploadDocumentResponseDto>(
				new Common.Responses.Error("Document:NoFileUploaded", "No file was uploaded"));
		}

		// Convert IFormFile to ProcessedStudentDocumentDto
		await using var memoryStream = new MemoryStream();
		await request.File.CopyToAsync(memoryStream);

		var input = new ProcessedStudentDocumentDto
		{
			DocumentTypeId = request.DocumentTypeId,
			FileName = request.File.FileName,
			ContentType = request.File.ContentType,
			Size = request.File.Length,
			Content = memoryStream.ToArray()
		};

		var result = await documentAppService.UploadDocumentAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Gets a list of all documents uploaded for a student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>List of uploaded documents with metadata</returns>
	/// <response code="200">Returns the list of documents</response>
	/// <response code="404">Student not found</response>
	[HttpGet("{studentId}/documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<List<StudentDocumentDto>>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<List<StudentDocumentDto>>> GetDocumentListAsync(Guid studentId)
	{
		var result = await documentAppService.GetDocumentListAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Downloads a specific document for a student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="documentId">The document identifier</param>
	/// <returns>File download with appropriate headers</returns>
	/// <response code="200">Returns the file for download</response>
	/// <response code="404">Document not found or doesn't belong to student</response>
	[HttpGet("{studentId}/documents/{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DownloadDocumentAsync(Guid studentId, Guid documentId)
	{
		var document = await documentAppService.GetDocumentAsync(studentId, documentId);

		return File(
			document.Content,
			document.ContentType,
			document.FileName);
	}

	/// <summary>
	/// Deletes a document for a student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="documentId">The document identifier</param>
	/// <returns>Success result</returns>
	/// <response code="200">Document deleted successfully</response>
	/// <response code="404">Document not found or doesn't belong to student</response>
	[HttpDelete("{studentId}/documents/{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Delete)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result> DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		await documentAppService.DeleteDocumentAsync(studentId, documentId);
		return Result.Success("Document deleted successfully");
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
	[HttpGet("{studentId}/family-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentFamilyInfoDto>> GetFamilyInfoAsync(Guid studentId)
	{
		var result = await registrationAppService.GetFamilyInfoAsync(studentId);
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
	[HttpPut("{studentId}/family-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentFamilyInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentFamilyInfoDto>> UpdateFamilyInfoAsync(
		Guid studentId,
		[FromBody] UpdateFamilyInfoDto input)
	{
		var result = await registrationAppService.UpdateFamilyInfoAsync(studentId, input);
		return Result.Success(result);
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
	[HttpGet("{studentId}/communication-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentCommunicationInfoDto>> GetCommunicationInfoAsync(Guid studentId)
	{
		var result = await registrationAppService.GetCommunicationInfoAsync(studentId);
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
	[HttpPut("{studentId}/communication-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentCommunicationInfoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentCommunicationInfoDto>> UpdateCommunicationInfoAsync(
		Guid studentId,
		[FromBody] UpdateCommunicationInfoDto input)
	{
		var result = await registrationAppService.UpdateCommunicationInfoAsync(studentId, input);
		return Result.Success(result);
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
	[HttpGet("{studentId}/additional-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentAdditionalDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentAdditionalDetailsDto>> GetAdditionalDetailsAsync(Guid studentId)
	{
		var result = await registrationAppService.GetAdditionalDetailsAsync(studentId);
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
	[HttpPut("{studentId}/additional-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentAdditionalDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentAdditionalDetailsDto>> UpdateAdditionalDetailsAsync(
		Guid studentId,
		[FromBody] UpdateAdditionalDetailsDto input)
	{
		var result = await registrationAppService.UpdateAdditionalDetailsAsync(studentId, input);
		return Result.Success(result);
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
	[HttpGet("{studentId}/medical-records")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentMedicalRecordsDto>> GetMedicalRecordsAsync(Guid studentId)
	{
		var result = await registrationAppService.GetMedicalRecordsAsync(studentId);
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
	[HttpPut("{studentId}/medical-records")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentMedicalRecordsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentMedicalRecordsDto>> UpdateMedicalRecordsAsync(
		Guid studentId,
		[FromBody] UpdateMedicalRecordsDto input)
	{
		var result = await registrationAppService.UpdateMedicalRecordsAsync(studentId, input);
		return Result.Success(result);
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
	[HttpGet("{studentId}/previous-school-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> GetPreviousSchoolDetailsAsync(Guid studentId)
	{
		var result = await registrationAppService.GetPreviousSchoolDetailsAsync(studentId);
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
	[HttpPut("{studentId}/previous-school-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentPreviousSchoolDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentPreviousSchoolDetailsDto>> UpdatePreviousSchoolDetailsAsync(
		Guid studentId,
		[FromBody] UpdatePreviousSchoolDetailsDto input)
	{
		var result = await registrationAppService.UpdatePreviousSchoolDetailsAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Gets bank details information for a student in Registration status.
	/// Includes bank name, account number, IFSC code, branch details, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Bank details information</returns>
	/// <response code="200">Returns the bank details information</response>
	/// <response code="400">Student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpGet("{studentId}/bank-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<StudentBankDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentBankDetailsDto>> GetBankDetailsAsync(Guid studentId)
	{
		var result = await registrationAppService.GetBankDetailsAsync(studentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates bank details for a student in Registration status.
	/// This includes banking information for fee payments, refunds, and financial transactions.
	/// Updates the registration state to BankDetails upon successful validation.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Bank details data</param>
	/// <returns>Updated bank details information</returns>
	/// <response code="200">Bank details updated successfully</response>
	/// <response code="400">Invalid data or student not in valid status</response>
	/// <response code="404">Student not found</response>
	[HttpPut("{studentId}/bank-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[ProducesResponseType(typeof(Result<StudentBankDetailsDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<StudentBankDetailsDto>> UpdateBankDetailsAsync(
		Guid studentId,
		[FromBody] UpdateBankDetailsDto input)
	{
		var result = await registrationAppService.UpdateBankDetailsAsync(studentId, input);
		return Result.Success(result);
	}
}
