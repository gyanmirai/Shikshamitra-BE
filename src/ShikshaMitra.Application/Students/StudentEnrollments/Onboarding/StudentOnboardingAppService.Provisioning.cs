using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task EnsureSettingsScopeAsync(Guid? yearId)
	{
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return;
		if (yearId.HasValue && scope.WildcardBoardAcademicYearIds.Contains(yearId.Value)) return;
		throw new AbpAuthorizationException("Editing shared onboarding settings requires access to the whole selected academic year, or school-wide access for school settings.");
	}
	private async Task<IQueryable<StudentOnboardingCase>> GetScopedCasesAsync()
	{
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		var query = await caseRepository.GetQueryableAsync();
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);
		return query.Where(c => scope.AcademicYearIds.Contains(c.AcademicYearId) &&
			(scope.WildcardBoardAcademicYearIds.Contains(c.AcademicYearId) ||
			 scope.WildcardMediumBoardIds.Contains(c.IntendedBoardId) ||
			 scope.WildcardMediumStageMediumIds.Contains(c.IntendedMediumId) ||
			 scope.WildcardStandardMediumStageIds.Contains(c.IntendedStageId) ||
			 scope.WildcardDivisionStandardIds.Contains(c.IntendedStandardId) ||
			 c.DivisionId.HasValue && scope.DivisionIds.Contains(c.DivisionId.Value)));
	}

	private async Task RefreshProvisioningAsync(StudentOnboardingCase item)
	{
		// Historical/completed records are immutable. A retry never enrolls the student twice.
		if (item.Status == StudentOnboardingStatus.Completed) return;
		var handoffs = await handoffRepository.GetListAsync(h => h.OnboardingCaseId == item.Id);
		var finance = handoffs.SingleOrDefault(h => h.ServiceKey == "finance-first-payment");
		if (finance == null)
			throw OnboardingRuleConfiguration.Invalid("The required Finance handoff is missing. Reconcile this case before completion.");
		var now = DateTime.UtcNow;
		var readiness = await feeReadinessReader.GetAsync(item.StudentId, item.AcademicYearId);
		if (readiness.Assigned && readiness.FirstInstallmentSettled) finance.Acknowledge(now);
		else finance.Fail(readiness.Detail, now);
		await handoffRepository.UpdateAsync(finance, false);
		foreach (var account in handoffs.Where(h => h.ServiceKey is "student-account" or "guardian-account"))
		{
			var type = account.ServiceKey.Replace("-account", "");
			var link = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == type);
			var user = link == null ? null : await identityUserRepository.FindAsync(link.UserId);
			if (user?.IsActive == true) account.Acknowledge(now);
			else account.Fail("Create and link the required portal account, or ask an identity administrator to review an inactive account.", now);
			await handoffRepository.UpdateAsync(account, false);
		}
		var pending = handoffs.Where(h => h.Required && h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged).ToList();
		if (pending.Count != 0)
		{
			item.RequireAttention(string.Join("; ", pending.Select(h => h.LastError ?? $"Complete required {h.ServiceKey} provisioning.")));
			await AddEventAsync(item.Id, "ProvisioningChecked", "Required provisioning is still incomplete.");
		}
		else
		{
			item.CompleteProvisioning(now);
			await AddEventAsync(item.Id, "Completed", "Onboarding completed after Finance confirmed the first installment and all required handoffs were acknowledged.");
			await distributedEventBus.PublishAsync(new StudentOnboardingCompletedEto(item.Id, item.StudentId,
				item.StudentEnrollmentId!.Value, item.AcademicYearId, item.IntendedStandardId, item.DivisionId!.Value, CurrentTenant.Id, now));
		}
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}
}
