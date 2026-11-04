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

public class AdmissionInquiryFamilyConfirmationApiContract_Tests
{
	[Fact]
	public async Task Confirm_family_is_an_additive_staff_post_requiring_manage_at_both_boundaries_and_forwards_exact_input()
	{
		foreach (var type in new[] { typeof(AdmissionInquiriesController), typeof(AdmissionInquiryAppService) })
			type.GetMethod(nameof(AdmissionInquiriesController.ConfirmFamilyAsync))!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy).ShouldContain(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage);
		var route = typeof(AdmissionInquiriesController).GetMethod(nameof(AdmissionInquiriesController.ConfirmFamilyAsync))!.GetCustomAttribute<HttpPostAttribute>()!;
		route.Template.ShouldBe("{id:guid}/confirm-family");
		var service = Substitute.For<IAdmissionInquiryAppService>(); var id = Guid.NewGuid();
		var input = new ConfirmAdmissionInquiryFamilyDto { ExistingFamilyId = Guid.NewGuid(), ExistingParentApplicantId = Guid.NewGuid(), ParentRelationship = "mother", ConcurrencyStamp = "current-stamp" };
		service.ConfirmFamilyAsync(id, input).Returns(new AdmissionInquiryDto { Id = id, SiblingFamilyConfirmed = true });
		await new AdmissionInquiriesController(service).ConfirmFamilyAsync(id, input);
		await service.Received(1).ConfirmFamilyAsync(id, input);
		await service.DidNotReceiveWithAnyArgs().CreateAsync(default!);
		await service.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!);
		typeof(ConfirmAdmissionInquiryFamilyDto).GetProperties().Select(property => property.Name).OrderBy(name => name)
			.ShouldBe(new[] { "ConcurrencyStamp", "ExistingFamilyId", "ExistingParentApplicantId", "ParentRelationship" });
	}
}
