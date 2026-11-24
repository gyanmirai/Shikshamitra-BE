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
	public async Task Sibling_matches_is_a_scoped_view_get_and_forwards_one_read_without_mutation()
	{
		foreach (var type in new[] { typeof(AdmissionInquiriesController), typeof(AdmissionInquiryAppService) })
			type.GetMethod(nameof(AdmissionInquiriesController.GetSiblingMatchesAsync))!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy).ShouldContain(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View);
		typeof(AdmissionInquiriesController).GetMethod(nameof(AdmissionInquiriesController.GetSiblingMatchesAsync))!
			.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("{id:guid}/sibling-matches");
		var service = Substitute.For<IAdmissionInquiryAppService>(); var id = Guid.NewGuid();
		service.GetSiblingMatchesAsync(id).Returns(new AdmissionInquirySiblingMatchesDto
		{
			Inquiry = new() { Id = id, ConcurrencyStamp = "fresh", HasPossibleSiblingMatch = true },
			Matches = [new() { ApplicantId = Guid.NewGuid(), CanLinkFamily = true }]
		});
		var response = await new AdmissionInquiriesController(service).GetSiblingMatchesAsync(id);
		response.Data!.Inquiry.ConcurrencyStamp.ShouldBe("fresh"); response.Data.Matches.Count.ShouldBe(1);
		await service.Received(1).GetSiblingMatchesAsync(id);
		await service.DidNotReceiveWithAnyArgs().ConfirmFamilyAsync(default, default!);
		await service.DidNotReceiveWithAnyArgs().CreateAsync(default!);
	}

	[Fact]
	public async Task Family_only_confirmation_forwards_the_child_witness_without_parent_role_or_account_proof()
	{
		new ConfirmAdmissionInquiryFamilyDto().FamilyLinkOnly.ShouldBeFalse();
		new CreateAdmissionInquiryDto().FamilyLinkOnly.ShouldBeFalse();
		var service = Substitute.For<IAdmissionInquiryAppService>();
		var id = Guid.NewGuid();
		var input = new ConfirmAdmissionInquiryFamilyDto { FamilyLinkOnly = true, ExistingFamilyId = Guid.NewGuid(),
			ExistingParentApplicantId = Guid.NewGuid(), ConcurrencyStamp = "current-stamp" };
		service.ConfirmFamilyAsync(id, input).Returns(new AdmissionInquiryDto { Id = id, HasLinkedSiblings = true, SiblingFamilyConfirmed = false });
		var response = await new AdmissionInquiriesController(service).ConfirmFamilyAsync(id, input);
		var body = response.Data.ShouldNotBeNull();
		body.HasLinkedSiblings.ShouldBeTrue(); body.SiblingFamilyConfirmed.ShouldBeFalse();
		input.ParentRelationship.ShouldBeNull();
		await service.Received(1).ConfirmFamilyAsync(id, input);
	}

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
			.ShouldBe(new[] { "ConcurrencyStamp", "ExistingFamilyId", "ExistingParentApplicantId", "FamilyLinkOnly", "ParentRelationship" });
	}
}
