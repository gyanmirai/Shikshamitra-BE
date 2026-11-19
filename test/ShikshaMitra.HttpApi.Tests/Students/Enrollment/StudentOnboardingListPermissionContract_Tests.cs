using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class StudentOnboardingListPermissionContract_Tests
{
	[Fact]
	public void List_requires_onboarding_view_at_controller_and_application_boundaries()
	{
		foreach (var type in new[] { typeof(StudentOnboardingController), typeof(StudentOnboardingAppService) })
		{
			var method = type.GetMethod(nameof(StudentOnboardingAppService.GetListAsync))!;
			method.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy)
				.ShouldContain(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View);
		}
	}
}
