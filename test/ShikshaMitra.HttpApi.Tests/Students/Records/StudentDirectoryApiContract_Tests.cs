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
			type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
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
		typeof(StudentDirectoryController).GetMethod(controllerMethod)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(StudentDirectoryAppService).GetMethod(serviceMethod)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(DecideStudentLegacyOverrideDto).GetProperty("ConcurrencyStamp").ShouldNotBeNull();
	}
}
