using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	[UnitOfWork(true)]
	public async Task<StudentOnboardingCaseDto> EnableGuardianCredentialEmailAsync(Guid id, EnableGuardianCredentialEmailDto input)
	{
		var validation = await new EnableGuardianCredentialEmailDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw PortalInvalid(validation.Errors[0].ErrorMessage);
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (!item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (!item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) || snapshot is not string text)
			throw PortalInvalid("This case has no reviewed readiness snapshot. Review its requirements first.");
		var rules = JsonNode.Parse(text) as JsonObject
			?? throw PortalInvalid("The saved case rules are unavailable for correction.");
		if (!OnboardingRuleConfiguration.Boolean(JsonSerializer.SerializeToElement(rules), "guardianAccount"))
			throw PortalInvalid("A guardian portal account is not required by this case's saved rules.");
		if (OnboardingRuleConfiguration.Boolean(JsonSerializer.SerializeToElement(rules), "sendGuardianCredentialEmail"))
			return await MapCaseAsync(item, true);

		// Preserve every other pinned requirement and the school/Offering configuration.
		rules["sendGuardianCredentialEmail"] = true;
		item.ExtraProperties["ReadinessRulesSnapshot"] = rules.ToJsonString();
		await eventRepository.InsertAsync(new StudentOnboardingEvent(GuidGenerator.Create(), CurrentTenant.Id, id,
			"GuardianCredentialEmailEnabled", $"Parent/guardian credential email enabled for this case only. Reason: {input.Reason.Trim()}",
			JsonSerializer.Serialize(new { Rule = "sendGuardianCredentialEmail", PreviousValue = false, NewValue = true })), false);
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		// This correction intentionally does not refresh provisioning or enqueue delivery.
		return await MapCaseAsync(item, true);
	}
}
