using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;

/// <summary>
/// Domain service for managing student documents with business rules and validations
/// </summary>
public class StudentDocumentManager : DomainService
{
	private readonly IRepository<StudentDocument, Guid> _studentDocumentRepository;
	private readonly IRepository<DocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IBlobContainer _blobContainer;

	public StudentDocumentManager(
		IRepository<StudentDocument, Guid> studentDocumentRepository,
		IRepository<DocumentType, Guid> documentTypeRepository,
		IRepository<Student, Guid> studentRepository,
		IBlobContainer blobContainer)
	{
		_studentDocumentRepository = studentDocumentRepository;
		_documentTypeRepository = documentTypeRepository;
		_studentRepository = studentRepository;
		_blobContainer = blobContainer;
	}

	/// <summary>
	/// Validates and uploads a document for a student
	/// </summary>
	public async Task<StudentDocument> UploadDocumentAsync(
		Guid studentId,
		Guid documentTypeId,
		string fileName,
		string contentType,
		long size,
		byte[] content)
	{
		// 1. Validate student exists and is in appropriate status
		var student = await _studentRepository.GetAsync(studentId);
		ValidateStudentStatus(student);

		// 2. Validate document type exists and belongs to tenant
		var documentType = await _documentTypeRepository.GetAsync(documentTypeId);
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFoundInTenant")
				.WithData("DocumentTypeId", documentTypeId);
		}

		// 3. Validate file format
		ValidateFileFormat(fileName, contentType, documentType);

		// 4. Validate file size
		ValidateFileSize(size, documentType);

		// 5. Check if document of same type already exists (replace if yes)
		var existingDocument = await _studentDocumentRepository.FirstOrDefaultAsync(
			d => d.StudentId == studentId && d.DocumentTypeId == documentTypeId);

		StudentDocument studentDocument;

		if (existingDocument != null)
		{
			// Delete old blob
			await _blobContainer.DeleteAsync(existingDocument.BlobName);

			// Generate new blob name
			var blobName = GenerateBlobName(studentId, documentTypeId, fileName);

			// Update existing document
			existingDocument.UpdateDocument(blobName, fileName, contentType, size);
			studentDocument = existingDocument;
		}
		else
		{
			// Generate blob name
			var blobName = GenerateBlobName(studentId, documentTypeId, fileName);

			// Create new document entity
			studentDocument = new StudentDocument(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				studentId,
				documentTypeId,
				blobName,
				fileName,
				contentType,
				size);
		}

		// 6. Save to blob storage
		await _blobContainer.SaveAsync(studentDocument.BlobName, content, overrideExisting: true);

		// 7. Save entity
		if (existingDocument != null)
		{
			await _studentDocumentRepository.UpdateAsync(studentDocument);
		}
		else
		{
			await _studentDocumentRepository.InsertAsync(studentDocument);
		}

		return studentDocument;
	}

	/// <summary>
	/// Gets a document with its content for download
	/// </summary>
	public async Task<(byte[] content, StudentDocument metadata)> GetDocumentAsync(
		Guid studentId,
		Guid documentId)
	{
		// 1. Validate document exists and belongs to student
		var document = await _studentDocumentRepository.GetAsync(documentId);

		if (document.StudentId != studentId)
		{
			throw new BusinessException("StudentDocument:NotBelongsToStudent")
				.WithData("DocumentId", documentId)
				.WithData("StudentId", studentId);
		}

		// 2. Retrieve from blob storage
		var stream = await _blobContainer.GetAsync(document.BlobName);
		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		var content = memoryStream.ToArray();

		return (content, document);
	}

	/// <summary>
	/// Gets all documents for a student with DocumentType information
	/// </summary>
	public async Task<List<(StudentDocument document, DocumentType documentType)>> GetDocumentListAsync(
		Guid studentId)
	{
		// Get all documents for the student
		var documents = await _studentDocumentRepository.GetListAsync(d => d.StudentId == studentId);

		if (!documents.Any())
		{
			return new List<(StudentDocument, DocumentType)>();
		}

		// Get all related document types
		var documentTypeIds = documents.Select(d => d.DocumentTypeId).Distinct().ToList();
		var documentTypes = await _documentTypeRepository.GetListAsync(
			dt => documentTypeIds.Contains(dt.Id));

		// Join and return
		return documents
			.Select(doc =>
			{
				var docType = documentTypes.First(dt => dt.Id == doc.DocumentTypeId);
				return (doc, docType);
			})
			.OrderBy(x => x.docType.DisplayOrder)
			.ThenBy(x => x.docType.Name)
			.ToList();
	}

	/// <summary>
	/// Deletes a document
	/// </summary>
	public async Task DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		// 1. Validate document exists and belongs to student
		var document = await _studentDocumentRepository.GetAsync(documentId);

		if (document.StudentId != studentId)
		{
			throw new BusinessException("StudentDocument:NotBelongsToStudent")
				.WithData("DocumentId", documentId)
				.WithData("StudentId", studentId);
		}

		// 2. Delete from blob storage
		await _blobContainer.DeleteAsync(document.BlobName);

		// 3. Delete entity
		await _studentDocumentRepository.DeleteAsync(document);
	}

	/// <summary>
	/// Gets the status of required documents for a student
	/// </summary>
	public async Task<List<(DocumentType documentType, StudentDocument? uploadedDocument)>>
		GetRequiredDocumentsStatusAsync(Guid studentId)
	{
		// Get all document types for the tenant
		var allDocumentTypes = await _documentTypeRepository.GetListAsync(
			dt => dt.TenantId == CurrentTenant.Id);

		// Get uploaded documents for the student
		var uploadedDocuments = await _studentDocumentRepository.GetListAsync(
			d => d.StudentId == studentId);

		// Match and return
		var result = allDocumentTypes
			.Select(docType =>
			{
				var uploaded = uploadedDocuments.FirstOrDefault(d => d.DocumentTypeId == docType.Id);
				return (docType, uploaded);
			})
			.OrderBy(x => x.docType.DisplayOrder)
			.ThenBy(x => x.docType.Name)
			.ToList();

		return result;
	}

	#region Private Helper Methods

	/// <summary>
	/// Validates that student is in appropriate status for document upload
	/// </summary>
	private void ValidateStudentStatus(Student student)
	{
		var validStatuses = new[]
		{
			StudentLifeCycleStatusEnum.RegistrationFormIssued,
			StudentLifeCycleStatusEnum.Registation
		};

		if (!validStatuses.Contains(student.Status))
		{
			throw new BusinessException("Student:InvalidStatusForDocumentUpload")
				.WithData("CurrentStatus", student.Status.ToString())
				.WithData("ValidStatuses", string.Join(", ", validStatuses.Select(s => s.ToString())));
		}
	}

	/// <summary>
	/// Validates file format against document type configuration
	/// </summary>
	private void ValidateFileFormat(string fileName, string contentType, DocumentType documentType)
	{
		var fileExtension = Path.GetExtension(fileName)?.ToLowerInvariant();
		if (string.IsNullOrEmpty(fileExtension))
		{
			throw new BusinessException("Document:InvalidFileName")
				.WithData("FileName", fileName);
		}

		// Map extension to AcceptedFileFormat enum
		var format = MapExtensionToFormat(fileExtension);
		if (!format.HasValue)
		{
			throw new BusinessException("Document:UnsupportedFileExtension")
				.WithData("Extension", fileExtension)
				.WithData("FileName", fileName);
		}

		// Check if format is accepted by document type
		var acceptedFormats = documentType.GetAcceptedFormats();
		if (!acceptedFormats.Contains(format.Value))
		{
			throw new BusinessException("Document:InvalidFormat")
				.WithData("DocumentTypeName", documentType.Name)
				.WithData("ProvidedFormat", format.Value.ToString())
				.WithData("AcceptedFormats", string.Join(", ", acceptedFormats.Select(f => f.ToString())));
		}

		// Validate content type matches
		var expectedContentType = GetContentTypeForFormat(format.Value);
		if (!contentType.Equals(expectedContentType, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("Document:ContentTypeMismatch")
				.WithData("ProvidedContentType", contentType)
				.WithData("ExpectedContentType", expectedContentType)
				.WithData("Extension", fileExtension);
		}
	}

	/// <summary>
	/// Validates file size against document type configuration
	/// </summary>
	private void ValidateFileSize(long sizeInBytes, DocumentType documentType)
	{
		var sizeInMB = sizeInBytes / (1024.0 * 1024.0);

		if (sizeInMB > documentType.MaxSizeMB)
		{
			throw new BusinessException("Document:FileSizeExceeded")
				.WithData("ActualSizeMB", Math.Round(sizeInMB, 2))
				.WithData("MaxSizeMB", documentType.MaxSizeMB)
				.WithData("DocumentTypeName", documentType.Name);
		}
	}

	/// <summary>
	/// Generates a unique blob name for storage
	/// Pattern: student-documents/{tenantId}/{studentId}/{documentTypeId}_{guid}_{filename}
	/// </summary>
	private string GenerateBlobName(Guid studentId, Guid documentTypeId, string fileName)
	{
		var sanitizedFileName = SanitizeFileName(fileName);
		var uniqueId = Guid.NewGuid().ToString("N");
		var tenantId = CurrentTenant.Id?.ToString("N") ?? "host";

		return $"student-documents/{tenantId}/{studentId:N}/{documentTypeId:N}_{uniqueId}_{sanitizedFileName}";
	}

	/// <summary>
	/// Sanitizes filename to remove invalid characters
	/// </summary>
	private string SanitizeFileName(string fileName)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
	}

	/// <summary>
	/// Maps file extension to AcceptedFileFormat enum
	/// </summary>
	private AcceptedFileFormat? MapExtensionToFormat(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".jpg" => AcceptedFileFormat.Jpg,
			".jpeg" => AcceptedFileFormat.Jpeg,
			".png" => AcceptedFileFormat.Png,
			".pdf" => AcceptedFileFormat.Pdf,
			".docx" => AcceptedFileFormat.Docx,
			".xlsx" => AcceptedFileFormat.Xlsx,
			_ => null
		};
	}

	/// <summary>
	/// Gets the expected content type for a file format
	/// </summary>
	private string GetContentTypeForFormat(AcceptedFileFormat format)
	{
		return format switch
		{
			AcceptedFileFormat.Jpg => "image/jpeg",
			AcceptedFileFormat.Jpeg => "image/jpeg",
			AcceptedFileFormat.Png => "image/png",
			AcceptedFileFormat.Pdf => "application/pdf",
			AcceptedFileFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			AcceptedFileFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			_ => throw new ArgumentException($"Unsupported format: {format}")
		};
	}

	#endregion
}
