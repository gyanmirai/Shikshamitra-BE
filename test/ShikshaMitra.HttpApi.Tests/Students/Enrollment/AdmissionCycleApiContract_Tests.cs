using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionCycleApiContract_Tests
{
	[Fact]
	public void Controller_exposes_the_governed_cycle_routes()
	{
		var controller = typeof(AdmissionCyclesController);
		controller.GetCustomAttribute<RouteAttribute>()!.Template
			.ShouldBe("api/student/enrollment/admissions/cycles");

		PostTemplate(nameof(AdmissionCyclesController.Publish)).ShouldBe("{id:guid}/publish");
		PostTemplate(nameof(AdmissionCyclesController.Close)).ShouldBe("{id:guid}/close");
		PostTemplate(nameof(AdmissionCyclesController.Reopen)).ShouldBe("{id:guid}/reopen");
		PostTemplate(nameof(AdmissionCyclesController.Archive)).ShouldBe("{id:guid}/archive");
		PostTemplate(nameof(AdmissionCyclesController.Clone)).ShouldBe("clone");
		PostTemplate(nameof(AdmissionCyclesController.Validate)).ShouldBe("{id:guid}/validate");
		GetTemplate(nameof(AdmissionCyclesController.GetImpact)).ShouldBe("{id:guid}/impact");
		GetTemplate(nameof(AdmissionCyclesController.GetHistory)).ShouldBe("{id:guid}/history");
		GetTemplate(nameof(AdmissionCyclesController.GetCapacity)).ShouldBe("{id:guid}/capacity");
		PutTemplate(nameof(AdmissionCyclesController.UpdateOfferingCapacity))
			.ShouldBe("{id:guid}/offerings/{offeringId:guid}/capacity");
	}

	[Theory]
	[InlineData(nameof(AdmissionCyclesController.GetList), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCyclesController.GetImpact), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCyclesController.GetHistory), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCyclesController.GetCapacity), ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	[InlineData(nameof(AdmissionCyclesController.Create), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.Publish), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.Reopen), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.UpdateWindow), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.Schedule), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.UpdateOfferingCapacity), ShikshaMitraSubmodulePermissions.Students.Admissions.Add)]
	[InlineData(nameof(AdmissionCyclesController.Close), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	[InlineData(nameof(AdmissionCyclesController.Archive), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	[InlineData(nameof(AdmissionCyclesController.Delete), ShikshaMitraSubmodulePermissions.Students.Admissions.Delete)]
	public void Controller_uses_only_the_three_admissions_permissions(string methodName, string permission)
	{
		var method = typeof(AdmissionCyclesController).GetMethods().Single(value => value.Name == methodName);
		method.GetCustomAttributes<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == permission);
	}

	[Fact]
	public void Dependency_and_history_reads_remain_inside_the_standard_result_envelope()
	{
		var impactReturn = typeof(AdmissionCyclesController).GetMethod(nameof(AdmissionCyclesController.GetImpact))!.ReturnType;
		var historyReturn = typeof(AdmissionCyclesController).GetMethod(nameof(AdmissionCyclesController.GetHistory))!.ReturnType;

		impactReturn.GenericTypeArguments.Single().ShouldBe(typeof(Result<AdmissionCycleImpactDto>));
		historyReturn.GenericTypeArguments.Single().GetGenericTypeDefinition().ShouldBe(typeof(Result<>));
	}

	private static string? PostTemplate(string methodName) =>
		typeof(AdmissionCyclesController).GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<HttpPostAttribute>()?.Template;

	private static string? GetTemplate(string methodName) =>
		typeof(AdmissionCyclesController).GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<HttpGetAttribute>()?.Template;

	private static string? PutTemplate(string methodName) =>
		typeof(AdmissionCyclesController).GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<HttpPutAttribute>()?.Template;
}
