using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for managing student documents during registration
/// </summary>
public class StudentDocumentAppService : ApplicationService, IStudentDocumentAppService
{
	private readonly StudentDocumentManager _documentManager;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public StudentDocumentAppService(
		StudentDocumentManager documentManager,
		StudentScopeAccessService studentScopeAccessService)
	{
		_documentManager = documentManager;
		_studentScopeAccessService = studentScopeAccessService;
	}

	/// <summary>
	/// Uploads a document for a student in registration
	/// </summary>
	public async Task<UploadDocumentResponseDto> UploadDocumentAsync(
		Guid studentId,
		ProcessedStudentDocumentDto input)
	{
		try
		{
			await _studentScopeAccessService.GetStudentAsync(studentId, nameof(UploadDocumentAsync));
			Logger.LogInformation(
				"Uploading document for student {StudentId}, DocumentType {DocumentTypeId}, File {FileName}",
				studentId, input.DocumentTypeId, input.FileName);

			// Check if document already exists (for wasReplaced flag)
			var existingDocuments = await _documentManager.GetDocumentListAsync(studentId);
			var wasReplaced = existingDocuments.Any(d => d.document.DocumentTypeId == input.DocumentTypeId);

			// Upload document using domain service
			var document = await _documentManager.UploadDocumentAsync(
				studentId,
				input.DocumentTypeId,
				input.FileName,
				input.ContentType,
				input.Size,
				input.Content);

			Logger.LogInformation(
				"Successfully uploaded document {DocumentId} for student {StudentId}",
				document.Id, studentId);

			return new UploadDocumentResponseDto
			{
				DocumentId = document.Id,
				Message = wasReplaced
					? "Document replaced successfully"
					: "Document uploaded successfully",
				WasReplaced = wasReplaced,
				Sha256Hash = document.IsSensitive ? null : document.Sha256Hash,
				StorageStatus = document.StorageStatus,
				IsSensitive = document.IsSensitive
			};
		}
		catch (BusinessException ex)
		{
			Logger.LogWarning(ex,
				"Business validation failed while uploading document for student {StudentId}: {Message}",
				studentId, ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex,
				"Error uploading document for student {StudentId}",
				studentId);
			throw new UserFriendlyException(
				"An error occurred while uploading the document. Please try again.");
		}
	}

	/// <summary>
	/// Gets list of all documents for a student
	/// </summary>
	public async Task<List<StudentDocumentDto>> GetDocumentListAsync(Guid studentId)
	{
		try
		{
			await _studentScopeAccessService.GetStudentAsync(studentId, nameof(GetDocumentListAsync));
			Logger.LogDebug("Retrieving document list for student {StudentId}", studentId);

			// Get all document types and uploaded documents for the student
			var documentsStatus = await _documentManager.GetRequiredDocumentsStatusAsync(studentId);

			var result = documentsStatus.Select(d => new StudentDocumentDto
			{
				Id = d.uploadedDocument?.Id ?? Guid.Empty,
				StudentId = studentId,
				DocumentTypeId = d.documentType.Id,
				DocumentTypeName = d.documentType.Name,
				FileName = d.uploadedDocument?.FileName ?? string.Empty,
				ContentType = d.uploadedDocument?.ContentType ?? string.Empty,
				Size = d.uploadedDocument?.Size ?? 0,
				CreationTime = d.uploadedDocument?.CreationTime ?? default,
				CreatorId = d.uploadedDocument?.CreatorId,
				IsUploaded = d.uploadedDocument != null,
				Sha256Hash = d.uploadedDocument is { IsSensitive: false } ? d.uploadedDocument.Sha256Hash : null,
				StorageStatus = d.uploadedDocument?.StorageStatus ?? StudentDocumentStorageStatus.PendingValidation,
				UploadedByUserId = d.uploadedDocument?.UploadedByUserId,
				ValidatedAt = d.uploadedDocument?.ValidatedAt,
				DownloadCount = d.uploadedDocument?.DownloadCount ?? 0,
				LastDownloadedAt = d.uploadedDocument?.LastDownloadedAt,
				IsSensitive = d.uploadedDocument?.IsSensitive ?? false
			}).ToList();

			Logger.LogDebug(
				"Retrieved {Count} document types for student {StudentId} (Uploaded: {Uploaded})",
				result.Count, studentId, result.Count(r => r.IsUploaded));

			return result;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex,
				"Error retrieving document list for student {StudentId}",
				studentId);
			throw new UserFriendlyException(
				"An error occurred while retrieving documents. Please try again.");
		}
	}

	/// <summary>
	/// Gets a specific document for download
	/// </summary>
	public async Task<StudentDocumentDownloadDto> GetDocumentAsync(
		Guid studentId,
		Guid documentId)
	{
		try
		{
			await _studentScopeAccessService.GetStudentAsync(studentId, nameof(GetDocumentAsync));
			Logger.LogInformation(
				"Downloading document {DocumentId} for student {StudentId}",
				documentId, studentId);

			var (content, metadata) = await _documentManager.GetDocumentAsync(studentId, documentId);

			Logger.LogInformation(
				"Successfully retrieved document {DocumentId} ({FileName}, {Size} bytes)",
				documentId, metadata.FileName, content.Length);

			return new StudentDocumentDownloadDto
			{
				FileName = metadata.FileName,
				ContentType = metadata.ContentType,
				Content = content
			};
		}
		catch (BusinessException ex)
		{
			Logger.LogWarning(ex,
				"Business validation failed while downloading document {DocumentId} for student {StudentId}",
				documentId, studentId);
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex,
				"Error downloading document {DocumentId} for student {StudentId}",
				documentId, studentId);
			throw new UserFriendlyException(
				"An error occurred while downloading the document. Please try again.");
		}
	}

	/// <summary>
	/// Deletes a document
	/// </summary>
	public async Task DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		try
		{
			await _studentScopeAccessService.GetStudentAsync(studentId, nameof(DeleteDocumentAsync));
			Logger.LogInformation(
				"Deleting document {DocumentId} for student {StudentId}",
				documentId, studentId);

			await _documentManager.DeleteDocumentAsync(studentId, documentId);

			Logger.LogInformation(
				"Successfully deleted document {DocumentId} for student {StudentId}",
				documentId, studentId);
		}
		catch (BusinessException ex)
		{
			Logger.LogWarning(ex,
				"Business validation failed while deleting document {DocumentId} for student {StudentId}",
				documentId, studentId);
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex,
				"Error deleting document {DocumentId} for student {StudentId}",
				documentId, studentId);
			throw new UserFriendlyException(
				"An error occurred while deleting the document. Please try again.");
		}
	}

	/// <summary>
	/// Gets the status of required documents (uploaded/missing)
	/// </summary>
	public async Task<List<DocumentTypeValidationDto>> GetRequiredDocumentsStatusAsync(Guid studentId)
	{
		try
		{
			await _studentScopeAccessService.GetStudentAsync(studentId, nameof(GetRequiredDocumentsStatusAsync));
			Logger.LogDebug(
				"Retrieving required documents status for student {StudentId}",
				studentId);

			var documentsStatus = await _documentManager.GetRequiredDocumentsStatusAsync(studentId);

			var result = documentsStatus.Select(d => new DocumentTypeValidationDto
			{
				DocumentTypeId = d.documentType.Id,
				DocumentTypeName = d.documentType.Name,
				IsRequired = d.documentType.IsRequired,
				IsUploaded = d.uploadedDocument != null,
				UploadedDocumentId = d.uploadedDocument?.Id,
				MaxSizeMB = d.documentType.MaxSizeMB,
				AcceptedFormats = d.documentType.GetAcceptedFormats(),
				DisplayOrder = d.documentType.DisplayOrder
			}).ToList();

			Logger.LogDebug(
				"Retrieved status for {Total} document types ({Uploaded} uploaded, {Required} required) for student {StudentId}",
				result.Count,
				result.Count(r => r.IsUploaded),
				result.Count(r => r.IsRequired),
				studentId);

			return result;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex,
				"Error retrieving required documents status for student {StudentId}",
				studentId);
			throw new UserFriendlyException(
				"An error occurred while checking document status. Please try again.");
		}
	}
}
