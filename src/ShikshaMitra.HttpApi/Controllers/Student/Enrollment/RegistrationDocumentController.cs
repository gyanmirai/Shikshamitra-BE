using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Models;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// Handles document upload, retrieval, download, and deletion for students in Registration status.
/// If a document of the same type already exists, it is replaced on upload.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/enrollment/registrations")]
public class RegistrationDocumentController(
    IStudentDocumentAppService documentAppService) : AbpControllerBase
{
    private const long MaxUploadBytes = (long)(DocumentTypeConsts.MaxDocumentSizeMB * 1024 * 1024);

    /// <summary>
    /// Uploads a document for a student. Replaces an existing document of the same type.
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
    public async Task<ActionResult<Result<UploadDocumentResponseDto>>> UploadDocumentAsync(
        Guid studentId,
        [FromForm] StudentDocumentUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(Result.Failure<UploadDocumentResponseDto>(
                new Common.Responses.Error("Document:NoFileUploaded", "No file was uploaded")));
        }

        if (request.File.Length > MaxUploadBytes)
        {
            return BadRequest(Result.Failure<UploadDocumentResponseDto>(
                new Common.Responses.Error("Document:FileSizeExceeded", "Uploaded file exceeds the allowed size")));
        }

        byte[] content;
        try
        {
            content = await ReadBoundedAsync(request.File, MaxUploadBytes, HttpContext.RequestAborted);
        }
        catch (BadHttpRequestException)
        {
            return BadRequest(Result.Failure<UploadDocumentResponseDto>(
                new Common.Responses.Error("Document:FileSizeExceeded", "Uploaded file exceeds the allowed size")));
        }

        var input = new ProcessedStudentDocumentDto
        {
            DocumentTypeId = request.DocumentTypeId,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
            Size = request.File.Length,
            Content = content
        };

        var result = await documentAppService.UploadDocumentAsync(studentId, input);
        return Ok(Result.Success(result));
    }

    /// <summary>
    /// Gets a list of all documents uploaded for a student.
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

    [HttpGet("{studentId}/documents/required-status")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(Result<List<DocumentTypeValidationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result<List<DocumentTypeValidationDto>>> GetRequiredDocumentsStatusAsync(Guid studentId)
    {
        var result = await documentAppService.GetRequiredDocumentsStatusAsync(studentId);
        return Result.Success(result);
    }

    /// <summary>
    /// Downloads a specific document for a student.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="documentId">The document identifier</param>
    /// <returns>File download with appropriate content-type headers</returns>
    /// <response code="200">Returns the file for download</response>
    /// <response code="404">Document not found or doesn't belong to student</response>
    [HttpGet("{studentId}/documents/{documentId:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.View)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocumentAsync(Guid studentId, Guid documentId)
    {
        var document = await documentAppService.GetDocumentAsync(studentId, documentId);
        return File(document.Content, document.ContentType, document.FileName);
    }

    /// <summary>
    /// Deletes a specific document for a student.
    /// </summary>
    /// <param name="studentId">The student identifier</param>
    /// <param name="documentId">The document identifier</param>
    /// <returns>Success result</returns>
    /// <response code="200">Document deleted successfully</response>
    /// <response code="404">Document not found or doesn't belong to student</response>
    [HttpDelete("{studentId}/documents/{documentId:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Delete)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<Result> DeleteDocumentAsync(Guid studentId, Guid documentId)
    {
        await documentAppService.DeleteDocumentAsync(studentId, documentId);
        return Result.Success("Document deleted successfully");
    }

    private static async Task<byte[]> ReadBoundedAsync(IFormFile file, long maxBytes, CancellationToken cancellationToken)
    {
        await using var inputStream = file.OpenReadStream();
        await using var memoryStream = new MemoryStream(capacity: (int)Math.Min(file.Length, 1024 * 1024));
        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var read = await inputStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new BadHttpRequestException("Uploaded file exceeds the allowed size");
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return memoryStream.ToArray();
    }
}
