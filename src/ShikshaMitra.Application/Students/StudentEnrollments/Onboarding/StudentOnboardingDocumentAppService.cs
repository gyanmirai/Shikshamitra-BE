using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.StudentOnboarding;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[Authorize(Permissions.View)]
public class StudentOnboardingDocumentAppService(
	IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<DocumentType, Guid> documentTypes,
	IStudentRepository students,
	AdmissionApplicationScopeAccessService scope,
	AdmissionStudentEvidenceReader admissionEvidence,
	StudentDocumentManager documents,
	IStudentOnboardingAppService onboardingService,
	IPermissionChecker permissions) : ApplicationService, IStudentOnboardingDocumentAppService
{
	public async Task<StudentOnboardingDocumentsDto> GetAsync(Guid caseId)
	{
		var (onboarding, student) = await GetContextAsync(caseId);
		var status = await documents.GetRequiredDocumentsStatusAsync(student.Id);
		var accepted = await admissionEvidence.GetAcceptedUploadDocumentTypeIdsAsync(student.Id);
		return new()
		{
			CaseId = caseId, StudentId = student.Id, ConcurrencyStamp = onboarding.ConcurrencyStamp,
			CanUpload = CanEdit(onboarding, student) && await permissions.IsGrantedAsync(Permissions.Edit),
			Documents = status.Select(item => new StudentOnboardingDocumentDto
			{
				DocumentTypeId = item.documentType.Id, Name = item.documentType.Name,
				IsRequired = item.documentType.IsRequired, MaxSizeMb = Math.Min(item.documentType.MaxSizeMB, 20),
				AllowedExtensions = item.documentType.GetAcceptedFormats().Select(format => format.ToString().ToLowerInvariant()).ToList(),
				StudentDocumentId = item.uploadedDocument?.Id, FileName = item.uploadedDocument?.FileName,
				HasStudentUpload = item.uploadedDocument?.ValidatedAt.HasValue == true,
				HasAcceptedAdmissionEvidence = accepted.Contains(item.documentType.Id)
			}).ToList()
		};
	}

	[Authorize(Permissions.Edit)]
	public async Task<StudentOnboardingDocumentsDto> UploadAsync(Guid caseId, StudentOnboardingDocumentUploadInput input)
	{
		var (onboarding, student) = await GetContextAsync(caseId);
		if (!CanEdit(onboarding, student)) throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != onboarding.ConcurrencyStamp)
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		var documentType = await documentTypes.FindAsync(input.DocumentTypeId) ??
			throw new EntityNotFoundException(typeof(DocumentType), input.DocumentTypeId);
		var bytes = await ReadFileAsync(input.File, (long)(Math.Min(documentType.MaxSizeMB, 20) * 1024 * 1024));
		await documents.UploadOnboardingDocumentAsync(student.Id, onboarding.Id, documentType.Id,
			input.File.FileName ?? string.Empty, input.File.ContentType, bytes.LongLength, bytes);
		onboarding.RecordProgress(Clock.Now);
		await cases.UpdateAsync(onboarding, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await onboardingService.RefreshReadinessAsync(onboarding.Id, onboarding.ConcurrencyStamp);
		return await GetAsync(caseId);
	}

	private async Task<(StudentOnboardingCase Onboarding, Student Student)> GetContextAsync(Guid caseId)
	{
		var onboarding = await cases.FindAsync(caseId) ?? throw new EntityNotFoundException(typeof(StudentOnboardingCase), caseId);
		var application = await applications.FindAsync(onboarding.ApplicationId) ??
			throw new EntityNotFoundException(typeof(StudentOnboardingCase), caseId);
		await scope.EnsureCanAccessAsync(application);
		if (application.Status != AdmissionApplicationStatus.AdmissionFinalized || application.StudentId != onboarding.StudentId ||
			application.OnboardingCaseId != onboarding.Id || application.AdmissionOfferingId != onboarding.OfferingId)
			throw new EntityNotFoundException(typeof(StudentOnboardingCase), caseId);
		var student = await students.GetAsync(onboarding.StudentId);
		return (onboarding, student);
	}

	private static bool CanEdit(StudentOnboardingCase onboarding, Student student) =>
		(onboarding.Status is StudentOnboardingStatus.Pending or StudentOnboardingStatus.InProgress or
			StudentOnboardingStatus.ReadyForActivation or StudentOnboardingStatus.NeedsAttention) &&
		(student.Status is StudentLifeCycleStatusEnum.Admission or StudentLifeCycleStatusEnum.Enrolled);

	private static async Task<byte[]> ReadFileAsync(IRemoteStreamContent file, long maxBytes)
	{
		if (file == null) throw new BusinessException("Document:EmptyFile");
		if (file.ContentLength > maxBytes) throw new BusinessException("Document:FileSizeExceeded");
		await using var stream = file.GetStream();
		using var output = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = await stream.ReadAsync(buffer)) > 0)
		{
			if (output.Length + read > maxBytes) throw new BusinessException("Document:FileSizeExceeded");
			await output.WriteAsync(buffer.AsMemory(0, read));
		}
		if (file.ContentLength.HasValue && file.ContentLength.Value != output.Length)
			throw new BusinessException("Document:SizeMismatch");
		return output.ToArray();
	}
}
