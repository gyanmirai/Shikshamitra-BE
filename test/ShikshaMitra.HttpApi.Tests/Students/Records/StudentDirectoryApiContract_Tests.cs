using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Records;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public class StudentDirectoryApiContract_Tests
{
	[Fact]
	public void Directory_is_explicit_paged_record_read_with_compatible_view_permission()
	{
		foreach (var type in new[] { typeof(StudentDirectoryController), typeof(StudentDirectoryAppService) })
			type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBeNull();
		// The service checks compatible List/I-Card/Certificates grants for both search and options.
		typeof(StudentDirectoryController).GetMethod("OptionsAsync")!.GetCustomAttributes<AuthorizeAttribute>().ShouldBeEmpty();
		typeof(StudentDirectoryController).GetMethod("OptionsAsync")!.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("options");
		typeof(StudentDirectoryController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/students/records/directory");
		typeof(StudentDirectoryRequest).GetProperty("SkipCount").ShouldNotBeNull();
		typeof(StudentDirectoryRequest).GetProperty("MaxResultCount").ShouldNotBeNull();
		typeof(StudentDirectoryItemDto).GetProperty("StudentId")!.PropertyType.ShouldBe(typeof(Guid));
	}

	[Theory]
	[InlineData("RequestAsync", "RequestLegacyOverrideAsync", ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request)]
	[InlineData("ApproveAsync", "ApproveLegacyOverrideAsync", ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	[InlineData("RejectAsync", "RejectLegacyOverrideAsync", ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	public void Override_write_permissions_are_explicit_at_controller_and_service(string controllerMethod, string serviceMethod, string permission)
	{
		typeof(StudentDirectoryController).GetMethod(controllerMethod)!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy)
			.ShouldContain(permission);
		typeof(StudentDirectoryController).GetMethod(controllerMethod)!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy)
			.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		typeof(StudentDirectoryAppService).GetMethod(serviceMethod)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(DecideStudentLegacyOverrideDto).GetProperty("ConcurrencyStamp").ShouldNotBeNull();
	}
}
