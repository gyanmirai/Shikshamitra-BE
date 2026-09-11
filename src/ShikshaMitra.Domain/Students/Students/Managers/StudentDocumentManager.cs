using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;
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
	private readonly IRepository<StudentOnboardingCase, Guid> _onboardingRepository;
	private readonly IBlobContainer _blobContainer;
	private readonly ICurrentUser _currentUser;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public StudentDocumentManager(
		IRepository<StudentDocument, Guid> studentDocumentRepository,
		IRepository<DocumentType, Guid> documentTypeRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<StudentDocumentAccessAudit, Guid> auditRepository,
		IRepository<StudentOnboardingCase, Guid> onboardingRepository,
		IBlobContainer blobContainer,
		ICurrentUser currentUser,
		IUnitOfWorkManager unitOfWorkManager)
	{
		_studentDocumentRepository = studentDocumentRepository;
		_documentTypeRepository = documentTypeRepository;
		_studentRepository = studentRepository;
		_auditRepository = auditRepository;
		_onboardingRepository = onboardingRepository;
		_blobContainer = blobContainer;
		_currentUser = currentUser;
		_unitOfWorkManager = unitOfWorkManager;
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
		return await SaveDocumentAsync(studentId, documentTypeId, fileName, contentType, size, content);
	}

	/// <summary>Allows document collection only for the exact Student's active onboarding case.</summary>
	public async Task<StudentDocument> UploadOnboardingDocumentAsync(Guid studentId, Guid onboardingCaseId,
		Guid documentTypeId, string fileName, string contentType, long size, byte[] content)
	{
		var student = await _studentRepository.GetAsync(studentId);
		var onboarding = await _onboardingRepository.FindAsync(onboardingCaseId);
		if (student.TenantId != CurrentTenant.Id || onboarding == null || onboarding.TenantId != CurrentTenant.Id ||
			onboarding.StudentId != studentId || onboarding.Status is not (StudentOnboardingStatus.Pending or StudentOnboardingStatus.InProgress or
				StudentOnboardingStatus.ReadyForActivation or StudentOnboardingStatus.NeedsAttention) ||
			student.Status is not (StudentLifeCycleStatusEnum.Admission or StudentLifeCycleStatusEnum.Enrolled))
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		return await SaveDocumentAsync(studentId, documentTypeId, fileName, contentType, size, content);
	}

	private async Task<StudentDocument> SaveDocumentAsync(Guid studentId, Guid documentTypeId,
		string fileName, string contentType, long size, byte[] content)
	{

		var documentType = await _documentTypeRepository.GetAsync(documentTypeId);
		if (documentType.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("DocumentType:NotFoundInTenant")
				.WithData("DocumentTypeId", documentTypeId);
		}

		DocumentFileValidationResult validation;
		try
		{
			validation = DocumentFileValidator.Validate(fileName, contentType, size, content, documentType);
		}
		catch (BusinessException ex)
		{
			await AddAuditAsync(
				studentId,
				null,
				documentTypeId,
				StudentDocumentAuditAction.ValidationFailed,
				DocumentFileValidator.SanitizeFileName(fileName),
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
			// A later failure may restore the previous database reference. Keep its file until commit.
			var unitOfWork = _unitOfWorkManager.Current;
			if (unitOfWork != null)
				unitOfWork.OnCompleted(() => DeleteReplacedBlobAsync(replacedBlobName));
			else
				await DeleteReplacedBlobAsync(replacedBlobName);
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

	private async Task DeleteReplacedBlobAsync(string blobName)
	{
		try { await _blobContainer.DeleteAsync(blobName); }
		catch (Exception exception)
		{
			// The committed replacement remains available; storage maintenance can remove the old file.
			Logger.LogWarning(exception, "Student document replacement committed, but previous-file cleanup failed.");
		}
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

	private string GenerateBlobName(Guid studentId, Guid documentTypeId, string safeFileName)
	{
		var uniqueId = Guid.NewGuid().ToString("N");
		var tenantId = CurrentTenant.Id?.ToString("N") ?? "host";

		return $"student-documents/{tenantId}/{studentId:N}/{documentTypeId:N}_{uniqueId}_{safeFileName}";
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
}
