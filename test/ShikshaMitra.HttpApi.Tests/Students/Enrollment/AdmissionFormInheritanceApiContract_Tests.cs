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
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFormInheritanceApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionFormsController.GetInheritanceSourcesAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionForms.ViewUsage)]
	[InlineData(nameof(AdmissionFormsController.PreviewInheritanceAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)]
	public void Inheritance_endpoints_require_existing_permissions_at_both_boundaries(string name, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionFormsController), typeof(AdmissionFormAppService) })
			boundary.GetMethod(name)!.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ShouldContain(permission);
	}

	[Fact]
	public async Task Preview_forwards_the_unsaved_draft_without_calling_a_write_operation()
	{
		var service = Substitute.For<IAdmissionFormAppService>();
		var input = new AdmissionFormInheritancePreviewInput { InquiryFormDefinitionId = Guid.NewGuid(), Draft = new() };
		service.PreviewInheritanceAsync(input).Returns(new AdmissionFormInheritancePreviewDto());
		await new AdmissionFormsController(service).PreviewInheritanceAsync(input);
		await service.Received(1).PreviewInheritanceAsync(input);
		await service.DidNotReceiveWithAnyArgs().CreateAsync(default!);
		await service.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!);
	}

	[Fact]
	public async Task Source_discovery_forwards_registration_identity_and_paging()
	{
		var service = Substitute.For<IAdmissionFormAppService>();
		var id = Guid.NewGuid();
		var input = new PagedResultRequestDto { SkipCount = 10, MaxResultCount = 20 };
		service.GetInheritanceSourcesAsync(id, input).Returns(new PagedResultDto<AdmissionFormInheritanceSourceSummaryDto>());
		await new AdmissionFormsController(service).GetInheritanceSourcesAsync(id, input);
		await service.Received(1).GetInheritanceSourcesAsync(id, input);
		typeof(AdmissionFormsController).GetMethod(nameof(AdmissionFormsController.GetInheritanceSourcesAsync))!
			.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("{id:guid}/inheritance/sources");
	}
}
