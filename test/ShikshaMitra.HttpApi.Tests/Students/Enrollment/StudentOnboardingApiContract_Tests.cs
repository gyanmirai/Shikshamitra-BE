using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class StudentOnboardingApiContract_Tests
{
	[Fact]
	public void Student_access_correction_is_case_scoped_and_requires_configuration_authority_at_both_boundaries()
	{
		Post(nameof(StudentOnboardingController.EnableStudentAppAccessAsync)).ShouldBe("{id:guid}/student-app-access/enable");
		foreach (var type in new[] { typeof(StudentOnboardingController), typeof(StudentOnboardingAppService) })
			type.GetMethod(nameof(StudentOnboardingAppService.EnableStudentAppAccessAsync))!
				.GetCustomAttributes<AuthorizeAttribute>().Select(value => value.Policy)
				.ShouldContain(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure);
		typeof(EnableStudentAppAccessDto).GetProperties().Select(value => value.Name).ShouldBe(["Reason", "ConcurrencyStamp"]);
	}

	[Fact]
	public void Profile_email_write_is_scoped_and_owner_confirmation_is_separate_without_token_auditing()
	{
		Put(nameof(StudentOnboardingController.UpdateCredentialsEmailAsync)).ShouldBe("{id:guid}/credentials/email");
		var type = typeof(StudentPortalEmailController);
		Route(type).ShouldBe("api/student/portal/email");
		var confirm = type.GetMethod(nameof(StudentPortalEmailController.ConfirmAsync))!;
		confirm.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("confirm");
		confirm.GetCustomAttribute<AllowAnonymousAttribute>().ShouldNotBeNull();
		confirm.GetCustomAttribute<Volo.Abp.Auditing.DisableAuditingAttribute>().ShouldNotBeNull();
		typeof(StudentPortalEmailChangeService).GetMethod(nameof(StudentPortalEmailChangeService.ConfirmAsync))!
			.GetCustomAttribute<Volo.Abp.Auditing.DisableAuditingAttribute>().ShouldNotBeNull();
	}

	[Fact]
	public void Portal_creation_requires_both_onboarding_and_identity_permissions_on_both_boundaries()
	{
		Get(nameof(StudentOnboardingController.GetPortalAccountsAsync)).ShouldBe("{id:guid}/portal-accounts");
		Post(nameof(StudentOnboardingController.ProvisionPortalAccountAsync)).ShouldBe("{id:guid}/portal-accounts");
		Get(nameof(StudentOnboardingController.GetMyPortalStudentsAsync)).ShouldBe("portal/my-students");
		foreach (var type in new[] { typeof(StudentOnboardingController), typeof(StudentOnboardingAppService) })
		{
			var policies = type.GetMethod(nameof(StudentOnboardingAppService.ProvisionPortalAccountAsync))!
				.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray();
			policies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
			policies.ShouldContain(Volo.Abp.Identity.IdentityPermissions.Users.Create);
		}
	}

	[Fact]
	public void Controller_exposes_queue_workspace_activation_and_settings_routes()
	{
		Route(typeof(StudentOnboardingController)).ShouldBe("api/student/onboarding");
		Get(nameof(StudentOnboardingController.GetListAsync)).ShouldBeNull();
		Get(nameof(StudentOnboardingController.GetFilterOptionsAsync)).ShouldBe("filter-options");
		Get(nameof(StudentOnboardingController.GetAvailableCampusesAsync)).ShouldBe("settings/campuses");
		Get(nameof(StudentOnboardingController.GetAnalyticsAsync)).ShouldBe("analytics");
		Get(nameof(StudentOnboardingController.GetAsync)).ShouldBe("{id:guid}");
		Get(nameof(StudentOnboardingController.GetPlacementOptionsAsync)).ShouldBe("{id:guid}/placement-options");
		Get(nameof(StudentOnboardingController.GetAvailableIdentifierSeriesAsync)).ShouldBe("{id:guid}/identifier-series");
		Post(nameof(StudentOnboardingController.RefreshReadinessAsync)).ShouldBe("{id:guid}/readiness/refresh");
		Post(nameof(StudentOnboardingController.SetPlacementAsync)).ShouldBe("{id:guid}/placement");
		Post(nameof(StudentOnboardingController.AllocateIdentifierAsync)).ShouldBe("{id:guid}/identifiers");
		Post(nameof(StudentOnboardingController.ActivateAsync)).ShouldBe("{id:guid}/activate");
		Post(nameof(StudentOnboardingController.RetryParentSetupAsync)).ShouldBe("{id:guid}/credentials/retry-parent-setup");
		Post(nameof(StudentOnboardingController.LinkParentAsync)).ShouldBe("{id:guid}/credentials/link-parent");
		Get(nameof(StudentOnboardingController.PreviewActivationAsync)).ShouldBe("{id:guid}/activation-preview");
		Post(nameof(StudentOnboardingController.CancelAsync)).ShouldBe("{id:guid}/cancel");
		Post(nameof(StudentOnboardingController.ConfirmServiceConsentAsync))
			.ShouldBe("{id:guid}/service-consent/confirm");
		Post(nameof(StudentOnboardingController.CreatePolicyAsync)).ShouldBe("settings/policies");
		Get(nameof(StudentOnboardingController.GetSettingsAsync)).ShouldBe("settings/configurations");
		Put(nameof(StudentOnboardingController.SaveSettingsAsync)).ShouldBe("settings/configurations");
		Delete(nameof(StudentOnboardingController.DeleteSettingsAsync))
			.ShouldBe("settings/configurations/{id:guid}");
		Post(nameof(StudentOnboardingController.CreateIdentifierSeriesAsync)).ShouldBe("settings/identifier-series");
		Post(nameof(StudentOnboardingController.DeactivateIdentifierSeriesAsync))
			.ShouldBe("settings/identifier-series/{id:guid}/deactivate");
		Get(nameof(StudentOnboardingController.GetHistoricalReconciliationAsync))
			.ShouldBe("settings/historical-reconciliation");
		Post(nameof(StudentOnboardingController.BackfillHistoricalAdmissionsAsync))
			.ShouldBe("settings/historical-reconciliation/backfill");
	}

	[Theory]
	[InlineData(nameof(StudentOnboardingAppService.GetAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetFilterOptionsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetAvailableCampusesAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetAvailableOfferingsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetAnalyticsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetPlacementOptionsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetAvailableIdentifierSeriesAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.RefreshReadinessAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.RetryParentSetupAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.UpdateCredentialsEmailAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.UpdateStepAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.SetPlacementAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	[InlineData(nameof(StudentOnboardingAppService.AllocateIdentifierAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	[InlineData(nameof(StudentOnboardingAppService.ActivateAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate)]
	[InlineData(nameof(StudentOnboardingAppService.PreviewActivationAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.CancelAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel)]
	[InlineData(nameof(StudentOnboardingAppService.ConfirmServiceConsentAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.CreatePolicyAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	[InlineData(nameof(StudentOnboardingAppService.GetPoliciesAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetIdentifierSeriesAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetSettingsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.SaveSettingsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	[InlineData(nameof(StudentOnboardingAppService.DeleteSettingsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Delete)]
	[InlineData(nameof(StudentOnboardingAppService.GetHistoricalReconciliationAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.BackfillHistoricalAdmissionsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public void Operations_require_granular_permissions(string methodName, string permission)
	{
		var method = typeof(StudentOnboardingAppService).GetMethods().Single(value => value.Name == methodName);
		method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(StudentOnboardingController).GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
	}

	[Fact]
	public void Service_consent_confirmation_requires_onboarding_edit_at_the_http_boundary()
	{
		typeof(StudentOnboardingController).GetMethod(nameof(StudentOnboardingController.ConfirmServiceConsentAsync))!
			.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(
				ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
	}

	[Fact]
	public void Parent_link_requires_both_onboarding_edit_and_identity_update_at_each_boundary()
	{
		foreach (var type in new[] { typeof(StudentOnboardingAppService), typeof(StudentOnboardingController) })
		{
			var policies = type.GetMethod(nameof(StudentOnboardingController.LinkParentAsync))!
				.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray();
			policies.Length.ShouldBe(2);
			policies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
			policies.ShouldContain(Volo.Abp.Identity.IdentityPermissions.Users.Update);
		}
	}

	private static string? Route(System.Type controller) =>
		controller.GetCustomAttribute<RouteAttribute>()?.Template;

	private static string? Get(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpGetAttribute>()?.Template;

	private static string? Post(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpPostAttribute>()?.Template;

	private static string? Put(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpPutAttribute>()?.Template;

	private static string? Delete(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpDeleteAttribute>()?.Template;
}
