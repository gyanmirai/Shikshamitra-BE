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
