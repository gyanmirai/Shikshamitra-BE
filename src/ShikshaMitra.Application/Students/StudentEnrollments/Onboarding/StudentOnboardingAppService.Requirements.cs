using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task<bool> CaptureAutomaticRequirementsAsync(StudentOnboardingCase item) =>
		AutomaticOnboardingReadinessRules.CanAdopt(item) &&
		await CaptureAutomaticRequirementsAsync(item, await configurationManager.GetAsync(item.AcademicYearId));

	private async Task<bool> CaptureAutomaticRequirementsAsync(StudentOnboardingCase item, OnboardingRuleConfiguration? configuration)
	{
		var changed = await LazyServiceProvider.LazyGetRequiredService<OnboardingRequirementSnapshotService>().CaptureAsync(item, configuration);
		if (!changed) return false;
		await caseRepository.UpdateAsync(item, false);
		await AddEventAsync(item.Id, "AutomaticRequirementsAdopted",
			"Admission document requirements and the school's portal access choices were applied automatically to this pending case.");
		return true;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentOnboardingCaseDto> ReviewRequirementsAsync(Guid id, string? concurrencyStamp)
	{
		var item = await GetCaseAsync(id); EnsureConcurrency(item, concurrencyStamp);
		if (item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		// Compatibility endpoint for older clients; normal reads and preparation now adopt
		// Admission requirements automatically without a separately linked readiness profile.
		await RefreshProfileReadinessAsync(item, await stepRepository.GetListAsync(s => s.OnboardingCaseId == id), Clock.Now);
		await CurrentUnitOfWork!.SaveChangesAsync(); return await MapCaseAsync(item, true);
	}
}
