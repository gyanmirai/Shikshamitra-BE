using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFormLifecycleApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionFormsController.RestoreAsync))]
	[InlineData(nameof(AdmissionFormsController.GetArchiveImpactAsync))]
	public void Lifecycle_operations_require_existing_form_manage_permission_at_both_boundaries(string method)
	{
		foreach (var boundary in new[] { typeof(AdmissionFormsController), typeof(AdmissionFormAppService) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy).ShouldContain(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Archive);
	}

	[Fact]
	public async Task Restore_forwards_the_exact_form_and_query_concurrency_stamp()
	{
		var service = Substitute.For<IAdmissionFormAppService>();
		var id = Guid.NewGuid();
		service.RestoreAsync(id, "reviewed-version").Returns(new AdmissionFormDto { Id = id });
		await new AdmissionFormsController(service).RestoreAsync(id, "reviewed-version");
		await service.Received(1).RestoreAsync(id, "reviewed-version");
		typeof(AdmissionFormsController).GetMethod(nameof(AdmissionFormsController.RestoreAsync))!.GetParameters()
			.Single(parameter => parameter.Name == "concurrencyStamp").GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}
}
