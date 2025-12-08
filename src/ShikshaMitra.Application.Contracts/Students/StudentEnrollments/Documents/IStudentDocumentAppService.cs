using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Documents;

/// <summary>
/// Application service for managing student documents
/// </summary>
public interface IStudentDocumentAppService : IApplicationService
{
	/// <summary>
	/// Uploads a document for a student in registration
	/// </summary>
	/// <param name="studentId">Student identifier</param>
	/// <param name="input">Document data</param>
	/// <returns>Upload response with document ID</returns>
	Task<UploadDocumentResponseDto> UploadDocumentAsync(
		Guid studentId,
		ProcessedStudentDocumentDto input);

	/// <summary>
	/// Gets list of all documents for a student
	/// </summary>
	/// <param name="studentId">Student identifier</param>
	/// <returns>List of student documents</returns>
	Task<List<StudentDocumentDto>> GetDocumentListAsync(Guid studentId);

	/// <summary>
	/// Gets a specific document for download
	/// </summary>
	/// <param name="studentId">Student identifier</param>
	/// <param name="documentId">Document identifier</param>
	/// <returns>Document content and metadata</returns>
	Task<StudentDocumentDownloadDto> GetDocumentAsync(Guid studentId, Guid documentId);

	/// <summary>
	/// Deletes a document
	/// </summary>
	/// <param name="studentId">Student identifier</param>
	/// <param name="documentId">Document identifier</param>
	Task DeleteDocumentAsync(Guid studentId, Guid documentId);

	/// <summary>
	/// Gets the status of required documents (uploaded/missing)
	/// </summary>
	/// <param name="studentId">Student identifier</param>
	/// <returns>List of document types with upload status</returns>
	Task<List<DocumentTypeValidationDto>> GetRequiredDocumentsStatusAsync(Guid studentId);
}
