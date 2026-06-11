using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Users;

namespace ShikshaMitra.Students;

/// <summary>
/// Domain service for managing student documents with business rules and validations.
/// </summary>
public class StudentDocumentManager : DomainService
{
	private readonly IRepository<StudentDocument, Guid> _studentDocumentRepository;
	private readonly IRepository<DocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<StudentDocumentAccessAudit, Guid> _auditRepository;
	private readonly IBlobContainer _blobContainer;
	private readonly ICurrentUser _currentUser;

	public StudentDocumentManager(
		IRepository<StudentDocument, Guid> studentDocumentRepository,
		IRepository<DocumentType, Guid> documentTypeRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<StudentDocumentAccessAudit, Guid> auditRepository,
		IBlobContainer blobContainer,
		ICurrentUser currentUser)
	{
		_studentDocumentRepository = studentDocumentRepository;
		_documentTypeRepository = documentTypeRepository;
		_studentRepository = studentRepository;
		_auditRepository = auditRepository;
		_blobContainer = blobContainer;
		_currentUser = currentUser;
	}

	public async Task<StudentDocument> UploadDocumentAsync(
		Guid studentId,
		Guid documentTypeId,
		string fileName,
		string contentType,
		long size,
		byte[] content)
	{
		var student = await _studentRepository.GetAsync(studentId);
		ValidateStudentStatus(student);

		var documentType = await _documentTypeRepository.GetAsync(documentTypeId);
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFoundInTenant")
				.WithData("DocumentTypeId", documentTypeId);
		}

		DocumentValidationResult validation;
		try
		{
			validation = ValidateDocument(fileName, contentType, size, content, documentType);
		}
		catch (BusinessException ex)
		{
			await AddAuditAsync(
				studentId,
				null,
				documentTypeId,
				StudentDocumentAuditAction.ValidationFailed,
				SanitizeFileName(fileName),
				contentType,
				size,
				null,
				ex.Code);
			throw;
		}

		var existingDocument = await _studentDocumentRepository.FirstOrDefaultAsync(
			d => d.StudentId == studentId && d.DocumentTypeId == documentTypeId);

		if (existingDocument?.Sha256Hash == validation.Sha256Hash)
		{
			await AddAuditAsync(
				studentId,
				existingDocument.Id,
				documentTypeId,
				StudentDocumentAuditAction.ValidationFailed,
				validation.SafeFileName,
				validation.ContentType,
				size,
				validation.Sha256Hash,
				"Document:DuplicateUpload");

			throw new BusinessException("Document:DuplicateUpload")
				.WithData("DocumentTypeName", documentType.Name);
		}

		var blobName = GenerateBlobName(studentId, documentTypeId, validation.SafeFileName);
		var now = DateTimeOffset.UtcNow;
		StudentDocument studentDocument;
		var action = StudentDocumentAuditAction.Upload;
		string? replacedBlobName = null;

		if (existingDocument != null)
		{
			replacedBlobName = existingDocument.BlobName;
			existingDocument.UpdateDocument(
				blobName,
				validation.SafeFileName,
				validation.ContentType,
				size,
				validation.Sha256Hash,
				_currentUser.Id,
				now,
				validation.IsSensitive);
			studentDocument = existingDocument;
			action = StudentDocumentAuditAction.Replace;
		}
		else
		{
			studentDocument = new StudentDocument(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				studentId,
				documentTypeId,
				blobName,
				validation.SafeFileName,
				validation.ContentType,
				size,
				validation.Sha256Hash,
				_currentUser.Id,
				now,
				validation.IsSensitive);
		}

		await _blobContainer.SaveAsync(studentDocument.BlobName, content, overrideExisting: true);

		if (existingDocument != null)
		{
			await _studentDocumentRepository.UpdateAsync(studentDocument);
		}
		else
		{
			await _studentDocumentRepository.InsertAsync(studentDocument);
		}

		if (!string.IsNullOrWhiteSpace(replacedBlobName))
		{
			try
			{
				await _blobContainer.DeleteAsync(replacedBlobName);
			}
			catch
			{
				// Replacement succeeded; stale private blobs can be cleaned by storage maintenance.
			}
		}

		await AddAuditAsync(
			studentId,
			studentDocument.Id,
			documentTypeId,
			action,
			studentDocument.FileName,
			studentDocument.ContentType,
			studentDocument.Size,
			studentDocument.Sha256Hash,
			null);

		return studentDocument;
	}

	public async Task<(byte[] content, StudentDocument metadata)> GetDocumentAsync(
		Guid studentId,
		Guid documentId)
	{
		var document = await _studentDocumentRepository.GetAsync(documentId);

		if (document.StudentId != studentId)
		{
			throw new BusinessException("StudentDocument:NotBelongsToStudent")
				.WithData("DocumentId", documentId)
				.WithData("StudentId", studentId);
		}

		await using var stream = await _blobContainer.GetAsync(document.BlobName);
		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);

		document.MarkDownloaded(DateTimeOffset.UtcNow);
		await _studentDocumentRepository.UpdateAsync(document);

		await AddAuditAsync(
			studentId,
			document.Id,
			document.DocumentTypeId,
			StudentDocumentAuditAction.Download,
			document.FileName,
			document.ContentType,
			document.Size,
			document.Sha256Hash,
			null);

		return (memoryStream.ToArray(), document);
	}

	public async Task<List<(StudentDocument document, DocumentType documentType)>> GetDocumentListAsync(
		Guid studentId)
	{
		var documents = await _studentDocumentRepository.GetListAsync(d => d.StudentId == studentId);

		if (!documents.Any())
		{
			return new List<(StudentDocument, DocumentType)>();
		}

		var documentTypeIds = documents.Select(d => d.DocumentTypeId).Distinct().ToList();
		var documentTypes = await _documentTypeRepository.GetListAsync(
			dt => documentTypeIds.Contains(dt.Id));

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

	public async Task DeleteDocumentAsync(Guid studentId, Guid documentId)
	{
		var document = await _studentDocumentRepository.GetAsync(documentId);

		if (document.StudentId != studentId)
		{
			throw new BusinessException("StudentDocument:NotBelongsToStudent")
				.WithData("DocumentId", documentId)
				.WithData("StudentId", studentId);
		}

		await _blobContainer.DeleteAsync(document.BlobName);
		document.MarkDeleted();

		await AddAuditAsync(
			studentId,
			document.Id,
			document.DocumentTypeId,
			StudentDocumentAuditAction.Delete,
			document.FileName,
			document.ContentType,
			document.Size,
			document.Sha256Hash,
			null);

		await _studentDocumentRepository.DeleteAsync(document);
	}

	public async Task<List<(DocumentType documentType, StudentDocument? uploadedDocument)>>
		GetRequiredDocumentsStatusAsync(Guid studentId)
	{
		var allDocumentTypes = await _documentTypeRepository.GetListAsync(
			dt => dt.TenantId == CurrentTenant.Id);

		var uploadedDocuments = await _studentDocumentRepository.GetListAsync(
			d => d.StudentId == studentId);

		return allDocumentTypes
			.Select(docType =>
			{
				var uploaded = uploadedDocuments.FirstOrDefault(d => d.DocumentTypeId == docType.Id);
				return (docType, uploaded);
			})
			.OrderBy(x => x.docType.DisplayOrder)
			.ThenBy(x => x.docType.Name)
			.ToList();
	}

	private DocumentValidationResult ValidateDocument(
		string fileName,
		string contentType,
		long size,
		byte[] content,
		DocumentType documentType)
	{
		if (size <= 0 || content.Length == 0)
		{
			throw new BusinessException("Document:EmptyFile");
		}

		ValidateFileSize(size, documentType);

		var safeFileName = SanitizeFileName(fileName);
		var fileExtension = Path.GetExtension(safeFileName)?.ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(fileExtension))
		{
			throw new BusinessException("Document:InvalidFileName");
		}

		var format = MapExtensionToFormat(fileExtension);
		if (!format.HasValue)
		{
			throw new BusinessException("Document:UnsupportedFileExtension")
				.WithData("Extension", fileExtension);
		}

		var acceptedFormats = documentType.GetAcceptedFormats();
		if (!acceptedFormats.Contains(format.Value))
		{
			throw new BusinessException("Document:InvalidFormat")
				.WithData("DocumentTypeName", documentType.Name)
				.WithData("ProvidedFormat", format.Value.ToString())
				.WithData("AcceptedFormats", string.Join(", ", acceptedFormats.Select(f => f.ToString())));
		}

		var expectedContentType = GetContentTypeForFormat(format.Value);
		if (!contentType.Equals(expectedContentType, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("Document:ContentTypeMismatch")
				.WithData("ProvidedContentType", contentType)
				.WithData("ExpectedContentType", expectedContentType)
				.WithData("Extension", fileExtension);
		}

		if (!HasExpectedSignature(content, format.Value))
		{
			throw new BusinessException("Document:SignatureMismatch")
				.WithData("Extension", fileExtension)
				.WithData("ContentType", expectedContentType);
		}

		return new DocumentValidationResult(
			safeFileName,
			expectedContentType,
			ComputeSha256(content),
			IsSensitiveDocumentType(documentType.Name));
	}

	private void ValidateStudentStatus(Student student)
	{
		var validStatuses = new[]
		{
			StudentLifeCycleStatusEnum.RegistrationFormIssued,
			StudentLifeCycleStatusEnum.Registration
		};

		if (!validStatuses.Contains(student.Status))
		{
			throw new BusinessException("Student:InvalidStatusForDocumentUpload")
				.WithData("CurrentStatus", student.Status.ToString())
				.WithData("ValidStatuses", string.Join(", ", validStatuses.Select(s => s.ToString())));
		}
	}

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

	private string GenerateBlobName(Guid studentId, Guid documentTypeId, string safeFileName)
	{
		var uniqueId = Guid.NewGuid().ToString("N");
		var tenantId = CurrentTenant.Id?.ToString("N") ?? "host";

		return $"student-documents/{tenantId}/{studentId:N}/{documentTypeId:N}_{uniqueId}_{safeFileName}";
	}

	private static string SanitizeFileName(string fileName)
	{
		var name = Path.GetFileName(fileName);
		var invalidChars = Path.GetInvalidFileNameChars();
		var safe = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();

		return string.IsNullOrWhiteSpace(safe)
			? "document"
			: safe;
	}

	private static AcceptedFileFormat? MapExtensionToFormat(string extension)
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

	private static string GetContentTypeForFormat(AcceptedFileFormat format)
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

	private static bool HasExpectedSignature(byte[] content, AcceptedFileFormat format)
	{
		if (content.Length < 4)
		{
			return false;
		}

		return format switch
		{
			AcceptedFileFormat.Jpg or AcceptedFileFormat.Jpeg =>
				content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
			AcceptedFileFormat.Png =>
				content.Length >= 8 &&
				content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47 &&
				content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A,
			AcceptedFileFormat.Pdf =>
				content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46,
			AcceptedFileFormat.Docx or AcceptedFileFormat.Xlsx =>
				content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04,
			_ => false
		};
	}

	private static string ComputeSha256(byte[] content)
	{
		var hash = SHA256.HashData(content);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static bool IsSensitiveDocumentType(string documentTypeName)
	{
		var normalized = documentTypeName.ToLowerInvariant();
		return normalized.Contains("aadhaar", StringComparison.Ordinal) ||
		       normalized.Contains("aadhar", StringComparison.Ordinal) ||
		       normalized.Contains("caste", StringComparison.Ordinal) ||
		       normalized.Contains("rte", StringComparison.Ordinal) ||
		       normalized.Contains("scholarship", StringComparison.Ordinal);
	}

	private Task AddAuditAsync(
		Guid studentId,
		Guid? documentId,
		Guid? documentTypeId,
		StudentDocumentAuditAction action,
		string? fileName,
		string? contentType,
		long? size,
		string? sha256Hash,
		string? reasonCode)
	{
		var audit = new StudentDocumentAccessAudit(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			studentId,
			documentId,
			documentTypeId,
			action,
			_currentUser.Id,
			DateTimeOffset.UtcNow,
			fileName,
			contentType,
			size,
			sha256Hash,
			reasonCode);

		return _auditRepository.InsertAsync(audit);
	}

	private sealed record DocumentValidationResult(
		string SafeFileName,
		string ContentType,
		string Sha256Hash,
		bool IsSensitive);
}
