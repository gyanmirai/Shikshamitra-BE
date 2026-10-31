using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[RemoteService(false)]
[Authorize(Permissions.StudentOnboarding.View)]
public class OnboardingWorkflowDocumentsAppService : ApplicationService, IOnboardingWorkflowDocumentsAppService
{
	private readonly IStudentOnboardingAppService onboarding;
	private readonly IRepository<StudentOnboardingCase, Guid> cases;
	private readonly AdmissionDocumentReadService reader;
	private readonly AdmissionDocumentCommandService commands;
	private readonly IRepository<AdmissionDocumentEvidence, Guid> evidence;

	public OnboardingWorkflowDocumentsAppService(
		IStudentOnboardingAppService onboarding,
		IRepository<StudentOnboardingCase, Guid> cases,
		AdmissionDocumentReadService reader,
		AdmissionDocumentCommandService commands,
		IRepository<AdmissionDocumentEvidence, Guid> evidence,
		OnboardingRequirementSnapshotService snapshots
	)
	{
		this.onboarding = onboarding;
		this.cases = cases;
		this.reader = reader;
		this.commands = commands;
		this.evidence = evidence;
		_ = snapshots;
	}

	public async Task<List<AdmissionDocumentVerificationDto>> GetAsync(Guid id)
	{
		await onboarding.EnsureCaseAccessAsync(id);
		var item = await cases.GetAsync(id);
		var result = new List<AdmissionDocumentVerificationDto>();
		foreach (var policy in OnboardingRequirementSnapshotService.Read(item))
			result.Add(await reader.GetAsync(item.ApplicationId, policy.StageId, onboarding: true));
		return result;
	}

	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.Upload)]
	public async Task<AdmissionDocumentVerificationDto> UploadAsync(Guid id, Guid stageId, string code, AdmissionDocumentUploadInput input)
	{
		await onboarding.EnsureCaseAccessAsync(id); var item = await cases.GetAsync(id);
		var result = await commands.UploadAsync(item.ApplicationId, stageId, code, input, onboarding: true);
		await onboarding.RefreshReadinessAsync(id, item.ConcurrencyStamp); return result;
	}

	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.RecordOriginal)]
	public async Task<AdmissionDocumentVerificationDto> RecordOriginalAsync(Guid id, Guid stageId, string code, AdmissionDocumentActionInput input)
	{
		await onboarding.EnsureCaseAccessAsync(id); var item = await cases.GetAsync(id);
		var result = await commands.RecordOriginalAsync(item.ApplicationId, stageId, code, input, onboarding: true);
		await onboarding.RefreshReadinessAsync(id, item.ConcurrencyStamp); return result;
	}

	[Authorize(Permissions.StudentOnboarding.Edit)]
	[Authorize(Permissions.AdmissionDocuments.Review)]
	public async Task<AdmissionDocumentVerificationDto> ReviewAsync(Guid id, Guid evidenceId, AdmissionDocumentReviewInput input)
	{
		await onboarding.EnsureCaseAccessAsync(id); var item = await cases.GetAsync(id);
		var row = await evidence.GetAsync(evidenceId);
		if (row.ApplicationId != item.ApplicationId) throw new BusinessException(AdmissionDocumentErrorCodes.InvalidEvidence);
		var result = await commands.ReviewAsync(evidenceId, input, onboarding: true);
		await onboarding.RefreshReadinessAsync(id, item.ConcurrencyStamp); return result;
	}
}
