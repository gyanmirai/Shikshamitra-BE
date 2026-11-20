using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionApproveAndFinalizeApiContract_Tests
{
	[Fact]
	public void Combined_approval_posts_with_all_owning_action_permissions()
	{
		var controller = typeof(AdmissionApplicationsController).GetMethod("ApproveAndFinalizeAsync")!;
		controller.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("{id:guid}/approve-and-finalize");
		var permissions = controller.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy);
		permissions.ShouldContain(ApplicationPermissions.Decide);
		permissions.ShouldContain(ApplicationPermissions.Advance);
		permissions.ShouldContain(ApplicationPermissions.Finalize);
	}

	[Fact]
	public void Approval_preview_is_an_optional_query_without_changing_the_existing_read_permission()
	{
		var controller = typeof(AdmissionApplicationsController).GetMethod("GetFinalizationPreviewAsync")!;
		controller.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("{id:guid}/finalization-preview");
		controller.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ShouldContain(ApplicationPermissions.View);
		var option = controller.GetParameters().Single(parameter => parameter.Name == "forApproval");
		option.GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
		option.DefaultValue.ShouldBe(false);
	}
}
