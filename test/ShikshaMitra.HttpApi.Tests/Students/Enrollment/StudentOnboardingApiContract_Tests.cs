using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class StudentOnboardingApiContract_Tests
{
	[Fact]
	public void Controller_exposes_queue_workspace_activation_and_settings_routes()
	{
		Route(typeof(StudentOnboardingController)).ShouldBe("api/student/onboarding");
		Get(nameof(StudentOnboardingController.GetListAsync)).ShouldBeNull();
		Get(nameof(StudentOnboardingController.GetFilterOptionsAsync)).ShouldBe("filter-options");
		Get(nameof(StudentOnboardingController.GetAnalyticsAsync)).ShouldBe("analytics");
		Get(nameof(StudentOnboardingController.GetAsync)).ShouldBe("{id:guid}");
		Get(nameof(StudentOnboardingController.GetPlacementOptionsAsync)).ShouldBe("{id:guid}/placement-options");
		Get(nameof(StudentOnboardingController.GetAvailableIdentifierSeriesAsync)).ShouldBe("{id:guid}/identifier-series");
		Post(nameof(StudentOnboardingController.RefreshReadinessAsync)).ShouldBe("{id:guid}/readiness/refresh");
		Post(nameof(StudentOnboardingController.SetPlacementAsync)).ShouldBe("{id:guid}/placement");
		Post(nameof(StudentOnboardingController.AllocateIdentifierAsync)).ShouldBe("{id:guid}/identifiers");
		Post(nameof(StudentOnboardingController.ActivateAsync)).ShouldBe("{id:guid}/activate");
		Post(nameof(StudentOnboardingController.CancelAsync)).ShouldBe("{id:guid}/cancel");
		Post(nameof(StudentOnboardingController.CreatePolicyAsync)).ShouldBe("settings/policies");
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
	[InlineData(nameof(StudentOnboardingAppService.GetAnalyticsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(nameof(StudentOnboardingAppService.GetPlacementOptionsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	[InlineData(nameof(StudentOnboardingAppService.GetAvailableIdentifierSeriesAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	[InlineData(nameof(StudentOnboardingAppService.RefreshReadinessAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.UpdateStepAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(nameof(StudentOnboardingAppService.SetPlacementAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateSeat)]
	[InlineData(nameof(StudentOnboardingAppService.AllocateIdentifierAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	[InlineData(nameof(StudentOnboardingAppService.ActivateAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate)]
	[InlineData(nameof(StudentOnboardingAppService.CancelAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel)]
	[InlineData(nameof(StudentOnboardingAppService.CreatePolicyAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure)]
	[InlineData(nameof(StudentOnboardingAppService.GetHistoricalReconciliationAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	[InlineData(nameof(StudentOnboardingAppService.BackfillHistoricalAdmissionsAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair)]
	public void Operations_require_granular_permissions(string methodName, string permission)
	{
		var method = typeof(StudentOnboardingAppService).GetMethods().Single(value => value.Name == methodName);
		method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
	}

	private static string? Route(System.Type controller) =>
		controller.GetCustomAttribute<RouteAttribute>()?.Template;

	private static string? Get(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpGetAttribute>()?.Template;

	private static string? Post(string methodName) => typeof(StudentOnboardingController).GetMethods()
		.Single(method => method.Name == methodName).GetCustomAttribute<HttpPostAttribute>()?.Template;
}
