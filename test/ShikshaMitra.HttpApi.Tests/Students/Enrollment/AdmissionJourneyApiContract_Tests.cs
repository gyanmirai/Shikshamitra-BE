using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Uow;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionJourneyApiContract_Tests
{
	[Theory]
	[InlineData(typeof(AdmissionApplicationsController), "GetResumePreviewAsync", Permissions.View)]
	[InlineData(typeof(AdmissionApplicationsController), "ResumeJourneyAsync", Permissions.Decide)]
	[InlineData(typeof(AdmissionApplicationAppService), "GetResumePreviewAsync", Permissions.View)]
	[InlineData(typeof(AdmissionApplicationAppService), "ResumeJourneyAsync", Permissions.Decide)]
	[InlineData(typeof(AdmissionJourneyResumeService), "PreviewAsync", Permissions.View)]
	[InlineData(typeof(AdmissionJourneyResumeService), "ResumeAsync", Permissions.Decide)]
	public void Resume_uses_existing_owner_permission(System.Type type, string name, string permission)
	{
		var method = type.GetMethod(name)!;
		method.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(permission);
		method.GetCustomAttribute<AllowAnonymousAttribute>().ShouldBeNull();
	}
	[Fact]
	public void End_journey_has_matching_permission_and_transaction_boundaries()
	{
		var controller = typeof(AdmissionApplicationsController).GetMethod(nameof(AdmissionApplicationsController.EndJourneyAsync))!;
		controller.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("{id:guid}/end-journey");
		controller.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.Decide);
		var service = typeof(AdmissionApplicationAppService).GetMethod(nameof(AdmissionApplicationAppService.EndJourneyAsync))!;
		service.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.Decide);
		service.GetCustomAttribute<UnitOfWorkAttribute>()!.IsTransactional.ShouldBe(true);
		typeof(AdmissionJourneyClosureService).GetMethod(nameof(AdmissionJourneyClosureService.EndAsync))!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.Decide);
	}
}
