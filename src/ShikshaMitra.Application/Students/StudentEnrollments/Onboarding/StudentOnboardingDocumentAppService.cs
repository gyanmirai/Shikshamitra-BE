using System;
using System.Collections.Generic;
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
	IRepository<StudentOnboardingEvent, Guid> events,
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
		var overrides = onboarding.GetDocumentOverrides().ToDictionary(item => item.DocumentTypeId);
		return new()
		{
			CaseId = caseId, StudentId = student.Id, ConcurrencyStamp = onboarding.ConcurrencyStamp,
			CanUpload = CanEdit(onboarding, student) && await permissions.IsGrantedAsync(Permissions.Edit),
			CanOverride = CanEdit(onboarding, student) && await permissions.IsGrantedAsync(Permissions.Edit) && await permissions.IsGrantedAsync(Permissions.Waive),
			Documents = status.Select(item => new StudentOnboardingDocumentDto
			{
				DocumentTypeId = item.documentType.Id, Name = item.documentType.Name,
				IsRequired = item.documentType.IsRequired, MaxSizeMb = Math.Min(item.documentType.MaxSizeMB, 20),
				AllowedExtensions = item.documentType.GetAcceptedFormats().Select(format => format.ToString().ToLowerInvariant()).ToList(),
				StudentDocumentId = item.uploadedDocument?.Id, FileName = item.uploadedDocument?.FileName,
				HasStudentUpload = item.uploadedDocument != null,
				HasAcceptedAdmissionEvidence = accepted.Contains(item.documentType.Id),
				IsValidated = item.uploadedDocument?.ValidatedAt.HasValue == true || accepted.Contains(item.documentType.Id),
				IsOverridden = overrides.ContainsKey(item.documentType.Id),
				OverrideReason = overrides.GetValueOrDefault(item.documentType.Id)?.Reason
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
		if (!onboarding.StudentEnrollmentId.HasValue) onboarding.RecordProgress(Clock.Now);
		await cases.UpdateAsync(onboarding, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await onboardingService.RefreshReadinessAsync(onboarding.Id, onboarding.ConcurrencyStamp);
		return await GetAsync(caseId);
	}

	[Authorize(Permissions.Waive)]
	[Authorize(Permissions.Edit)]
	[Volo.Abp.Uow.UnitOfWork(true)]
	public async Task<StudentOnboardingDocumentsDto> OverrideAsync(Guid caseId, StudentOnboardingDocumentOverrideInput input)
	{
		var (onboarding, student) = await GetContextAsync(caseId);
		if (!CanEdit(onboarding, student)) throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (!CurrentUser.Id.HasValue) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != onboarding.ConcurrencyStamp)
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 2000)
			throw OnboardingRuleConfiguration.Invalid("An override note is required (maximum 2000 characters).");
		var status = await documents.GetRequiredDocumentsStatusAsync(student.Id);
		var required = status.Where(item => item.documentType.IsRequired).Select(item => item.documentType.Id).ToHashSet();
		var selected = input.OverrideAll ? required.ToArray() : (input.DocumentTypeIds ?? []).Distinct().ToArray();
		if (selected.Length == 0 || selected.Any(id => !required.Contains(id)))
			throw OnboardingRuleConfiguration.Invalid("Only current required documents may be overridden.");
		onboarding.OverrideRequiredDocuments(selected, input.Reason, CurrentUser.Id.Value, Clock.Now);
		await cases.UpdateAsync(onboarding, false);
		await events.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id, caseId,
			"RequiredDocumentsOverridden", $"Staff overrode {selected.Length} required document(s) with a recorded note.",
			System.Text.Json.JsonSerializer.Serialize(new { DocumentTypeIds = selected, Reason = input.Reason.Trim(), StaffUserId = CurrentUser.Id })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await onboardingService.RefreshReadinessAsync(caseId, onboarding.ConcurrencyStamp);
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
			StudentOnboardingStatus.ReadyForActivation or StudentOnboardingStatus.NeedsAttention or StudentOnboardingStatus.Provisioning) &&
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
