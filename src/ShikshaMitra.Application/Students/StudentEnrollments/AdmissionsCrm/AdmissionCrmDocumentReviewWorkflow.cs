using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentReviewWorkflow(
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IBlobContainer blobContainer,
	AdmissionCrmDocumentAccessReader accessReader,
	AdmissionCrmDocumentChecklistWorkflow checklistWorkflow,
	AdmissionCrmDocumentTimelineWriter timelineWriter)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromApplicantAsync(
		Guid leadId,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		var assignment = await accessReader.GetAssignmentByLeadOrDefaultAsync(leadId)
		                 ?? throw new BusinessException("AdmissionCrm:DocumentChecklistNotAssigned");
		var application = await applicationRepository.GetAsync(assignment.RegistrationApplicationId);
		return await ProcessUploadAsync(assignment, application, input, actorUserId: null);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromStaffAsync(
		Guid registrationApplicationId,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		var application = await accessReader.GetApplicationAsync(registrationApplicationId, nameof(UploadFromStaffAsync));
		var assignment = await accessReader.GetAssignmentByApplicationAsync(registrationApplicationId);
		return await ProcessUploadAsync(assignment, application, input, CurrentUser.Id);
	}

	public async Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(
		Guid documentId,
		ReviewAdmissionCrmApplicantDocumentDto input)
	{
		var document = await accessReader.GetDocumentAsync(documentId, nameof(ReviewAsync));
		var assignment = await assignmentRepository.GetAsync(document.ChecklistAssignmentId);
		var requirement = AdmissionCrmDocumentModelMapper.GetRequirement(assignment, document.RequirementKey);
		if (input.Approve) document.Verify(CurrentUser.Id!.Value, Clock.Now);
		else document.Reject(CurrentUser.Id!.Value, Check.NotNullOrWhiteSpace(input.Reason, nameof(input.Reason), AdmissionCrmConsts.MaxNotesLength), Clock.Now);
		await documentRepository.UpdateAsync(document, autoSave: true);
		await timelineWriter.AddAsync(document.LeadId, document.AcademicYearId,
			input.Approve ? AdmissionCrmTimelineEventType.DocumentVerified : AdmissionCrmTimelineEventType.DocumentRejected,
			input.Approve ? "Document verified" : "Document rejected", input.Approve ? requirement.Name : input.Reason, document.Id);
		return AdmissionCrmDocumentModelMapper.ToDocumentDto(requirement, document, true);
	}

	public async Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(
		Guid assignmentId,
		string requirementKey,
		AdmissionCrmDocumentReasonDto input)
	{
		var assignment = await accessReader.GetAssignmentAsync(assignmentId, nameof(WaiveAsync));
		var requirement = AdmissionCrmDocumentModelMapper.GetRequirement(assignment, requirementKey);
		var current = await accessReader.GetCurrentDocumentOrDefaultAsync(assignment.Id, requirement.Key);
		current?.MarkSuperseded(Clock.Now);
		if (current != null) await documentRepository.UpdateAsync(current, autoSave: false);
		var waiver = AdmissionCrmApplicantDocument.CreateWaiver(
			GuidGenerator.Create(), CurrentTenant.Id, assignment.AcademicYearId, assignment.LeadId, assignment.Id,
			requirement.Key, (current?.Version ?? 0) + 1, CurrentUser.Id!.Value, input.Reason, Clock.Now);
		await documentRepository.InsertAsync(waiver, autoSave: true);
		await timelineWriter.AddAsync(assignment.LeadId, assignment.AcademicYearId, AdmissionCrmTimelineEventType.DocumentWaived,
			"Document requirement waived", input.Reason, waiver.Id);
		return AdmissionCrmDocumentModelMapper.ToDocumentDto(requirement, waiver, true);
	}

	public async Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId)
	{
		var document = await accessReader.GetDocumentAsync(documentId, nameof(MarkOriginalSeenAsync));
		var assignment = await assignmentRepository.GetAsync(document.ChecklistAssignmentId);
		var requirement = AdmissionCrmDocumentModelMapper.GetRequirement(assignment, document.RequirementKey);
		document.MarkOriginalSeen(CurrentUser.Id!.Value, Clock.Now);
		await documentRepository.UpdateAsync(document, autoSave: true);
		await timelineWriter.AddAsync(document.LeadId, document.AcademicYearId, AdmissionCrmTimelineEventType.DocumentOriginalSeen,
			"Original document seen", requirement.Name, document.Id);
		return AdmissionCrmDocumentModelMapper.ToDocumentDto(requirement, document, true);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(
		Guid assignmentId,
		ConfirmAdmissionCrmReadinessDto input)
	{
		var assignment = await accessReader.GetAssignmentAsync(assignmentId, nameof(ConfirmReadinessAsync));
		var application = await accessReader.GetApplicationAsync(assignment.RegistrationApplicationId, nameof(ConfirmReadinessAsync));
		var checklist = await checklistWorkflow.BuildDtoAsync(assignment, application);
		if (!checklist.AdmissionReady && !input.OverrideBlockingRequirements)
			throw new BusinessException("AdmissionCrm:AdmissionReadinessBlocked").WithData("BlockingReasons", checklist.BlockingReasons);
		if (input.OverrideBlockingRequirements)
		{
			if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideAdmissionReadiness))
				throw new Volo.Abp.Authorization.AbpAuthorizationException();
			Check.NotNullOrWhiteSpace(input.Reason, nameof(input.Reason), AdmissionCrmConsts.MaxNotesLength);
		}
		var completed = assignment.ConfirmReadiness(CurrentUser.Id!.Value, checklist.RequiredReadinessApprovalCount,
			input.OverrideBlockingRequirements, input.Reason, Clock.Now);
		if (completed) application.MarkAdmissionReady();
		await assignmentRepository.UpdateAsync(assignment, autoSave: false);
		if (completed) await applicationRepository.UpdateAsync(application, autoSave: false);
		await timelineWriter.AddAsync(assignment.LeadId, assignment.AcademicYearId,
			completed
				? assignment.ReadinessOverridden ? AdmissionCrmTimelineEventType.AdmissionReadinessOverridden : AdmissionCrmTimelineEventType.AdmissionReadinessConfirmed
				: AdmissionCrmTimelineEventType.AdmissionReadinessApprovalRecorded,
			completed ? assignment.ReadinessOverridden ? "Admission readiness overridden" : "Admission readiness confirmed" : "First admission readiness approval recorded",
			input.Reason, assignment.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await checklistWorkflow.BuildDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentDownloadDto> DownloadAsync(Guid documentId)
	{
		var document = await accessReader.GetDocumentAsync(documentId, nameof(DownloadAsync));
		if (string.IsNullOrWhiteSpace(document.BlobName)) throw new BusinessException("AdmissionCrm:DocumentFileNotAvailable");
		return new AdmissionCrmApplicantDocumentDownloadDto
		{
			FileName = document.FileName!, ContentType = document.ContentType!, Content = await blobContainer.GetAllBytesAsync(document.BlobName)
		};
	}

	private async Task<AdmissionCrmApplicantDocumentChecklistDto> ProcessUploadAsync(
		AdmissionCrmDocumentChecklistAssignment assignment,
		AdmissionCrmRegistrationApplication application,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input,
		Guid? actorUserId)
	{
		var template = AdmissionCrmDocumentModelMapper.Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson);
		var requirement = template.Requirements.FirstOrDefault(x => string.Equals(x.Key, input.RequirementKey, StringComparison.OrdinalIgnoreCase))
			?? throw new BusinessException("AdmissionCrm:DocumentRequirementNotFound");
		AdmissionCrmDocumentUploadValidator.Validate(requirement, input, Clock.Now);
		var current = await accessReader.GetCurrentDocumentOrDefaultAsync(assignment.Id, requirement.Key);
		var hash = Convert.ToHexString(SHA256.HashData(input.Content)).ToLowerInvariant();
		if (current?.Sha256Hash == hash) throw new BusinessException("AdmissionCrm:DuplicateDocumentUpload");
		var safeName = SanitizeFileName(input.FileName);
		var blobName = $"admissions-crm/{CurrentTenant.Id:D}/{application.LeadId:D}/{requirement.Key}/{Guid.NewGuid():N}/{safeName}";
		await blobContainer.SaveAsync(blobName, input.Content, overrideExisting: false);
		try
		{
			var document = new AdmissionCrmApplicantDocument(GuidGenerator.Create(), CurrentTenant.Id, assignment.AcademicYearId,
				application.LeadId, assignment.Id, requirement.Key, (current?.Version ?? 0) + 1, blobName, safeName,
				input.ContentType, input.Size, hash, input.ValidUntil, Clock.Now);
			current?.MarkSuperseded(Clock.Now);
			if (current != null) await documentRepository.UpdateAsync(current, autoSave: false);
			await documentRepository.InsertAsync(document, autoSave: true);
			await timelineWriter.AddAsync(application.LeadId, assignment.AcademicYearId, AdmissionCrmTimelineEventType.DocumentUploaded,
				"Document uploaded", $"{requirement.Name} v{document.Version}.", document.Id, actorUserId);
		}
		catch
		{
			await blobContainer.DeleteAsync(blobName);
			throw;
		}
		return await checklistWorkflow.BuildDtoAsync(assignment, application);
	}

	private static string SanitizeFileName(string fileName)
	{
		var safe = Path.GetFileName(fileName);
		foreach (var character in Path.GetInvalidFileNameChars()) safe = safe.Replace(character, '_');
		return Check.NotNullOrWhiteSpace(safe, nameof(fileName), AdmissionCrmConsts.MaxDocumentFileNameLength);
	}
}
