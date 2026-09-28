using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using OfferingPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionOfferingApiContract_Tests
{
	[Fact]
	public void Controller_exposes_required_command_and_read_routes()
	{
		var controller = typeof(AdmissionOfferingsController);
		controller.GetCustomAttribute<RouteAttribute>()!.Template
			.ShouldBe("api/student/enrollment/admissions/offerings");

		PostTemplate(nameof(AdmissionOfferingsController.Create)).ShouldBe("cycles/{admissionCycleId:guid}");
		PostTemplate(nameof(AdmissionOfferingsController.Activate)).ShouldBe("{id:guid}/activate");
		PostTemplate(nameof(AdmissionOfferingsController.Deactivate)).ShouldBe("{id:guid}/deactivate");
		PostTemplate(nameof(AdmissionOfferingsController.Clone)).ShouldBe("{id:guid}/clone");
		PostTemplate(nameof(AdmissionOfferingsController.Validate)).ShouldBe("validate");
		PostTemplate(nameof(AdmissionOfferingsController.BulkCreate)).ShouldBe("cycles/{admissionCycleId:guid}/bulk");
	}

	[Fact]
	public void Mutating_concurrency_sensitive_routes_require_a_stamp()
	{
		foreach (var methodName in new[] { nameof(AdmissionOfferingsController.Update), nameof(AdmissionOfferingsController.Activate), nameof(AdmissionOfferingsController.Deactivate), nameof(AdmissionOfferingsController.Delete) })
		{
			var method = typeof(AdmissionOfferingsController).GetMethods().Single(value => value.Name == methodName);
			var hasStamp = method.GetParameters().Any(parameter =>
				parameter.Name == "concurrencyStamp" || parameter.ParameterType == typeof(UpdateAdmissionOfferingDto));
			hasStamp.ShouldBeTrue($"{methodName} must carry optimistic concurrency data.");
		}
	}

	[Fact]
	public void Class_capacity_routes_preserve_view_and_configuration_permissions()
	{
		var controller = typeof(AdmissionClassCapacityController);
		controller.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/offerings/{offeringId:guid}/class-capacity");
		var get = controller.GetMethod(nameof(AdmissionClassCapacityController.Get))!;
		get.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBeNull();
		get.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy)
			.ShouldContain(OfferingPermissions.View);
		var rooms = controller.GetMethod(nameof(AdmissionClassCapacityController.GetRooms))!;
		rooms.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("rooms");
		rooms.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy)
			.ShouldContain(OfferingPermissions.View);
		var update = controller.GetMethod(nameof(AdmissionClassCapacityController.Update))!;
		update.GetCustomAttribute<HttpPutAttribute>()!.Template.ShouldBeNull();
		update.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy)
			.ShouldContain(OfferingPermissions.Add);
		update.GetParameters().ShouldContain(parameter =>
			parameter.ParameterType == typeof(UpdateAdmissionClassRoomsDto));
	}

	private static string? PostTemplate(string methodName) =>
		typeof(AdmissionOfferingsController).GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<HttpPostAttribute>()?.Template;
}
