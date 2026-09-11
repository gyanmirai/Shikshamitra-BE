using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionOffers;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionOfferApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionOfferAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionOfferAppService.IssueAsync), Permissions.Issue)]
	[InlineData(nameof(AdmissionOfferAppService.RespondAsync), Permissions.Respond)]
	[InlineData(nameof(AdmissionOfferAppService.ExtendAsync), Permissions.Extend)]
	[InlineData(nameof(AdmissionOfferAppService.WithdrawAsync), Permissions.Withdraw)]
	[InlineData(nameof(AdmissionOfferAppService.ReissueAsync), Permissions.Reissue)]
	public void Offer_operations_require_distinct_permissions_at_every_boundary(string method, string permission)
	{
		foreach (var boundary in new[]
		{
			typeof(IAdmissionOfferAppService), typeof(AdmissionOfferAppService), typeof(AdmissionOffersController)
		})
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(item => item.Policy).ShouldContain(permission);
	}

	[Fact]
	public void Managed_offer_contract_is_additive_and_runtime_ready()
	{
		typeof(AdmissionOffersController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/offers");
		typeof(AdmissionApplicationStageDto).GetProperty("OfferStageEnabled")!.PropertyType.ShouldBe(typeof(bool));
		typeof(AdmissionOfferStageDto).GetProperty(nameof(AdmissionOfferStageDto.Events)).ShouldNotBeNull();
		typeof(RespondAdmissionOfferDto).GetProperty(nameof(RespondAdmissionOfferDto.IdempotencyKey)).ShouldNotBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Offer)!;
		metadata.IsRuntimeReady.ShouldBeTrue();
		metadata.SupportedConfiguration.ShouldContain("offer");
	}
}
