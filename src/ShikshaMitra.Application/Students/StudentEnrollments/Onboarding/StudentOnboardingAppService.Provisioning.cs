using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentOnboardingCaseDto> ConfirmServiceConsentAsync(
		Guid id, ConfirmStudentOnboardingServiceConsentDto input)
	{
		var validation = await new ConfirmStudentOnboardingServiceConsentDtoValidator().ValidateAsync(input);
		if (!validation.IsValid)
			throw new BusinessException(StudentOnboardingErrorCodes.ServiceConsentInvalid)
				.WithData("Reason", validation.Errors[0].ErrorMessage);
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var item = await GetCaseAsync(id);
		var handoff = await handoffRepository.FindAsync(h => h.OnboardingCaseId == id &&
			h.ServiceKey == "service-consent" && h.Required);
		if (handoff == null || !IsApplicableServiceHandoff(item, handoff))
			throw new BusinessException(StudentOnboardingErrorCodes.ServiceConsentInvalid)
				.WithData("Reason", "Service consent is not required for this onboarding case.");
		if (handoff.Status == StudentOnboardingServiceHandoffStatus.Acknowledged)
			return await MapCaseAsync(item, true);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		var now = DateTime.UtcNow;
		handoff.Acknowledge(now);
		await handoffRepository.UpdateAsync(handoff, false);
		await AddEventAsync(id, "ServiceConsentConfirmed",
			$"Staff confirmed the saved service-consent record: {input.EvidenceReference}.");
		await RefreshProvisioningAsync(item);
		return await MapCaseAsync(item, true);
	}

	private async Task EnsureSettingsScopeAsync(Guid? yearId)
	{
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (scope.IsAdmin || scope.HasWildcardAcademicYear) return;
		if (yearId.HasValue && scope.WildcardBoardAcademicYearIds.Contains(yearId.Value)) return;
		throw new AbpAuthorizationException("Editing shared onboarding settings requires access to the whole selected academic year, or school-wide access for school settings.");
	}
	internal async Task<IQueryable<StudentOnboardingCase>> GetScopedCasesAsync()
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
		var reviewedReturn = ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue;
		if (reviewedReturn) await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		var handoffs = await handoffRepository.GetListAsync(h => h.OnboardingCaseId == item.Id);
		var now = DateTime.UtcNow;
		handoffs = handoffs.Where(handoff => IsApplicableServiceHandoff(item, handoff)).ToList();
		foreach (var account in handoffs.Where(h => h.ServiceKey is "student-account" or "guardian-account"))
		{
			var type = account.ServiceKey.Replace("-account", "");
			var link = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == type);
			var user = link == null ? null : await identityUserRepository.FindAsync(link.UserId);
			var validReturnAccount = !reviewedReturn || await IsCurrentReturnPortalAsync(item.StudentId, type, link);
			if (user?.IsActive == true && validReturnAccount) account.Acknowledge(now);
			else account.Fail("Create and link the required portal account, or ask an identity administrator to review an inactive account.", now);
			await handoffRepository.UpdateAsync(account, false);
		}
		var pending = handoffs.Where(h => h.Required && h.Status != StudentOnboardingServiceHandoffStatus.Acknowledged).ToList();
		if (pending.Count != 0)
		{
			item.RequireAttention(string.Join("; ", pending.Select(h => h.LastError ?? $"Complete required {h.ServiceKey} provisioning.")));
			await AddEventAsync(item.Id, "ProvisioningChecked", "Required provisioning is still incomplete.");
		}
        else if (await CredentialsPendingAsync(item, requiredOnly: true) && OnboardingRuleConfiguration.Text((await ReadSavedRulesAsync(item))!.Value, "credentialSendingTime", "activation") == "activation")
        {
            item.RequireAttention("Student is active. Complete or retry the requested credential emails in the Credentials tab.");
            await AddEventAsync(item.Id, "AccessPending", "Requested credential delivery is still pending after activation.");
        }
		else
		{
			item.CompleteProvisioning(now);
			if (reviewedReturn) await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().CompleteAsync(item);
			await AddEventAsync(item.Id, "Completed", "Onboarding completed after all requested access handoffs were acknowledged.");
			await distributedEventBus.PublishAsync(new StudentOnboardingCompletedEto(item.Id, item.StudentId,
				item.StudentEnrollmentId!.Value, item.AcademicYearId, item.IntendedStandardId, item.DivisionId!.Value, CurrentTenant.Id, now));
		}
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await QueueAutomaticCredentialEmailsAsync(item);
	}
	private async Task<bool> IsCurrentReturnPortalAsync(Guid studentId, string type, ShikshaMitra.Students.Portal.StudentPortalAccountLink? link)
	{
		if (link == null || link.VerifiedByUserId == Guid.Empty) return false;
		var student = await studentRepository.GetWithDetailsAsync(studentId);
		var user = await portalUserManager.FindByIdAsync(link.UserId.ToString());
		if (student == null || user == null || user.TenantId != CurrentTenant.Id || !user.IsActive) return false;
		var roles = await portalUserManager.GetRolesAsync(user);
		var email = type == "student" && link.Relationship == "self" ? student.ContactDetail?.Email : link.Relationship switch
		{ "father" => student.FamilyDetail?.FatherDetails?.Email, "mother" => student.FamilyDetail?.MotherDetails?.Email, "guardian" => student.FamilyDetail?.GuardianDetails?.Email, _ => null };
		return roles.Count == 1 && roles[0] == (type == "student" ? "Student" : "Parent") && !string.IsNullOrWhiteSpace(email) && string.Equals(email.Trim(), user.Email, StringComparison.OrdinalIgnoreCase);
	}
}
