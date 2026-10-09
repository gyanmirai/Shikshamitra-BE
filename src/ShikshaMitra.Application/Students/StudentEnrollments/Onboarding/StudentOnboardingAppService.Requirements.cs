using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentOnboardingCaseDto> ReviewRequirementsAsync(Guid id, string? concurrencyStamp)
	{
		var item = await GetCaseAsync(id); EnsureConcurrency(item, concurrencyStamp);
		if (item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (!item.ExtraProperties.ContainsKey("ReadinessRulesSnapshot"))
		{
			await LazyServiceProvider.LazyGetRequiredService<OnboardingRequirementSnapshotService>().CaptureAsync(item);
			if (!item.ExtraProperties.ContainsKey("ReadinessRulesSnapshot"))
                throw OnboardingRuleConfiguration.Invalid("Connect this Offering to a readiness rule before reviewing its requirements.");
            await AddEventAsync(id, "OfferingRequirementsReviewed", "Staff reviewed and adopted the Offering readiness and document requirements for this pending case.");
			await caseRepository.UpdateAsync(item, false);
		}
		await RefreshProfileReadinessAsync(item, await stepRepository.GetListAsync(s => s.OnboardingCaseId == id), Clock.Now);
		await CurrentUnitOfWork!.SaveChangesAsync(); return await MapCaseAsync(item, true);
	}
}
