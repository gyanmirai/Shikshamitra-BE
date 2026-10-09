using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<AdmissionFeeSelectionDto> GetFeeStructureAsync(Guid id)
	{
		var item = await GetCaseAsync(id);
		var result = await LazyServiceProvider.LazyGetRequiredService<AdmissionFeeSelectionAppService>().GetAsync(item.ApplicationId);
		result.Available = true;
		result.CanEdit = !item.StudentEnrollmentId.HasValue && item.Status != StudentOnboardingStatus.Cancelled &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		result.OnboardingBlocker = result.OnboardingConfirmed ? null : "Select and confirm a published fee structure.";
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<List<AdmissionFeeStructureCandidateDto>> GetFeeStructureCandidatesAsync(Guid id)
	{
		var item = await GetCaseAsync(id);
		var application = await applicationRepository.GetAsync(item.ApplicationId);
		return await LazyServiceProvider.LazyGetRequiredService<AdmissionFeeStructureReader>().GetCandidatesAsync(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<AdmissionFeeSelectionDto> SaveFeeStructureAsync(Guid id, SaveStudentOnboardingFeeStructureDto input)
	{
		var item = await GetCaseAsync(id); EnsureConcurrency(item, input.ConcurrencyStamp);
		if (item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		var actor = CurrentUser.Id ?? throw new AbpAuthorizationException();
		var application = await applicationRepository.GetAsync(item.ApplicationId);
		var snapshot = await LazyServiceProvider.LazyGetRequiredService<AdmissionFeeStructureReader>().CreateSnapshotAsync(application,
			application.GetFeeSelection()?.OwnerStageId ?? item.Id, AdmissionFeeStructureReader.ReferenceCurrency,
			new SaveAdmissionFeeSelectionDto { StructureId = input.StructureId, OptionalHeadIds = input.OptionalHeadIds }, actor);
		application.SaveOnboardingFeeSelection(snapshot);
		await applicationRepository.UpdateAsync(application, false);
		item.RecordProgress(Clock.Now); await caseRepository.UpdateAsync(item, false);
		await InvalidateApprovalAsync(id, "Fee structure confirmation changed; review readiness again.");
		await AddEventAsync(id, "FeeStructureConfirmed", $"Published fee structure {snapshot.StructureName}, revision {snapshot.RevisionNumber}, confirmed. No payment is required for activation.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		await RefreshProfileReadinessAsync(item, await stepRepository.GetListAsync(s => s.OnboardingCaseId == id), Clock.Now);
		await CurrentUnitOfWork.SaveChangesAsync();
		return await GetFeeStructureAsync(id);
	}
}
