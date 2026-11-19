using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Records;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

/// <summary>Route/action contracts only; actual authorization and scope are covered separately.</summary>
public class StudentRecordWorkflowApiContract_Tests
{
	[Theory]
	[InlineData("CreateAsync", ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create, null)]
	[InlineData("ApproveAsync", ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve, "{id:guid}/approve")]
	[InlineData("RejectAsync", ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve, "{id:guid}/reject")]
	[InlineData("CancelAsync", ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel, "{id:guid}/cancel")]
	public void Record_change_actions_use_the_same_exact_grant_at_both_boundaries(string method, string permission, string? route)
	{
		AssertAction(typeof(StudentRecordChangesController), typeof(StudentRecordChangeAppService), method, method, permission, route);
	}

	[Theory]
	[InlineData("PreviewAsync", "PreviewAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "preview")]
	[InlineData("SubmitAsync", "SubmitBatchAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "batches")]
	[InlineData("ApproveAsync", "ApproveAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve, "{id:guid}/approve")]
	[InlineData("RejectAsync", "RejectAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve, "{id:guid}/reject")]
	[InlineData("CancelAsync", "CancelAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "{id:guid}/cancel")]
	[InlineData("ApplyAsync", "ApplyAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply, "{id:guid}/apply")]
	[InlineData("CorrectAsync", "CorrectAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "{id:guid}/correct")]
	[InlineData("ReturnAsync", "ReturnAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "returns")]
	[InlineData("ConfirmAsync", "ConfirmRenewalAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create, "renewals/{id:guid}/confirm")]
	[InlineData("ActivateAsync", "ActivateRenewalAsync", ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply, "renewals/{id:guid}/activate")]
	public void Record_transition_commands_preserve_route_and_service_action_grants(string controllerMethod, string serviceMethod, string permission, string route)
	{
		AssertAction(typeof(StudentRecordTransitionsController), typeof(StudentRecordTransitionAppService), controllerMethod, serviceMethod, permission, route);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(IdentityPermissions.Users.Create)]
	public void Record_renewal_portal_requires_every_owner_grant_at_both_boundaries(string permission)
	{
		AssertAction(typeof(StudentRecordTransitionsController), typeof(StudentRecordTransitionAppService), "ProvisionPortalAsync", "ProvisionRenewalPortalAsync", permission, "renewals/{id:guid}/portal-accounts");
	}

	[Fact]
	public void Record_mutation_controllers_retain_their_separate_view_grants()
	{
		foreach (var (controller, service, permission) in new[]
		{
			(typeof(StudentRecordChangesController), typeof(StudentRecordChangeAppService), ShikshaMitraSubmodulePermissions.Students.Records_Changes.View),
			(typeof(StudentRecordTransitionsController), typeof(StudentRecordTransitionAppService), ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View)
		})
		{
			controller.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(permission);
			service.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(permission);
		}
	}

	private static void AssertAction(Type controller, Type service, string controllerMethod, string serviceMethod, string permission, string? route)
	{
		var endpoint = controller.GetMethod(controllerMethod)!;
		endpoint.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(permission);
		service.GetMethod(serviceMethod)!.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(permission);
		endpoint.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe(route);
	}
}
