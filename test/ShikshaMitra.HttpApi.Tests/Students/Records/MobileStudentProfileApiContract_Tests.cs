using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.HttpApi.Controllers.Students.Portal;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public class MobileStudentProfileApiContract_Tests
{
	[Fact]
	public void Mobile_profile_requires_authentication_at_both_boundaries_and_has_no_write_endpoint()
	{
		foreach (var boundary in new[] { typeof(MobileStudentProfileAppService), typeof(MobileStudentProfileController) })
		{
			var authorization = boundary.GetCustomAttribute<AuthorizeAttribute>();
			authorization.ShouldNotBeNull(); authorization.Policy.ShouldBeNull();
		}
		typeof(MobileStudentProfileController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/mobile/v1/students/{studentId:guid}/profile");
		var method = typeof(MobileStudentProfileController).GetMethod(nameof(MobileStudentProfileController.GetAsync))!;
		method.GetCustomAttribute<HttpGetAttribute>().ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<Result<MobileStudentProfileDto>>));
		typeof(IMobileStudentProfileAppService).GetMethods().Select(m => m.Name).ShouldBe(["GetAsync"]);
		typeof(MobileProfileFormDto).GetProperty("OriginalValues").ShouldBeNull();
		typeof(MobileProfileFormDto).GetProperty("FormEditableFieldCodes").ShouldBeNull();
	}
}
