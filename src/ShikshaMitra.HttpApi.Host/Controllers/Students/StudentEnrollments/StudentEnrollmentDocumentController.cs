using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student documents during registration
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/registration/{studentId}/documents")]
public class StudentEnrollmentDocumentController : AbpControllerBase
{
	private readonly IStudentDocumentAppService _documentAppService;

	public StudentEnrollmentDocumentController(IStudentDocumentAppService documentAppService)
	{
		_documentAppService = documentAppService;
	}

	/// <summary>
	/// Uploads a document for a student in registration.
	/// If a document of the same type already exists, it will be replaced.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="documentTypeId">The document type identifier from settings</param>
	/// <param name="file">The document file to upload</param>
	/// <returns>Upload response with document ID and status</returns>
	/// <response code="200">Document uploaded successfully</response>
	/// <response code="400">Invalid file format, size exceeds limit, or student not in valid status</response>
	/// <response code="404">Student or document type not found</response>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add)]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(Result<UploadDocumentResponseDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<UploadDocumentResponseDto>> UploadDocumentAsync(
		Guid studentId,
		[FromForm] Guid documentTypeId,
		[FromForm] IFormFile file)
	{
		// Validate file
		if (file == null || file.Length == 0)
		{
			return Result.Failure<UploadDocumentResponseDto>(
				new Common.Responses.Error("Document:NoFileUploaded", "No file was uploaded"));
		}

		// Convert IFormFile to ProcessedStudentDocumentDto
		await using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream);

		var input = new ProcessedStudentDocumentDto
		{
			DocumentTypeId = documentTypeId,
			FileName = file.FileName,
			ContentType = file.ContentType,
			Size = file.Length,
			Content = memoryStream.ToArray()
		};

		var result = await _documentAppService.UploadDocumentAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Gets a list of all documents uploaded for a student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>List of uploaded documents with metadata</returns>
	/// <response code="200">Returns the list of documents</response>
	/// <response code="404">Student not found</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<List<StudentDocumentDto>>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<List<StudentDocumentDto>>> GetDocumentListAsync(Guid studentId)
	{
		var result = await _documentAppService.GetDocumentListAsync(studentId);
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
	[HttpGet("{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DownloadDocumentAsync(Guid studentId, Guid documentId)
	{
		var document = await _documentAppService.GetDocumentAsync(studentId, documentId);

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
	[HttpDelete("{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Delete)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result> DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		await _documentAppService.DeleteDocumentAsync(studentId, documentId);
		return Result.Success("Document deleted successfully");
	}

	/// <summary>
	/// Gets the status of all document types (required/optional, uploaded/missing) for a student.
	/// This helps the UI display which documents are still needed.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>List of document types with their upload status</returns>
	/// <response code="200">Returns the document status list</response>
	/// <response code="404">Student not found</response>
	[HttpGet("required-status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
	[ProducesResponseType(typeof(Result<List<DocumentTypeValidationDto>>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public async Task<Result<List<DocumentTypeValidationDto>>> GetRequiredDocumentsStatusAsync(Guid studentId)
	{
		var result = await _documentAppService.GetRequiredDocumentsStatusAsync(studentId);
		return Result.Success(result);
	}
}
