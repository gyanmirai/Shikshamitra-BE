using System;
using System.Linq;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;
using System.Threading.Tasks;
using System.Text.Json;
using NSubstitute;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using ShikshaMitra.Constants.Permissions;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAcademicHierarchyMapping_Tests
{
	[Theory]
	[InlineData("EducationMediums", null)]
	[InlineData("EducationMediums", "")]
	[InlineData("StandardsByMedium", null)]
	[InlineData("StandardsByMedium", "")]
	public async Task Public_standalone_legacy_choices_remain_offering_bounded_without_invented_parents(string key, string? parentField)
	{
		var (service, offering) = PublicValidationFixture();
		var descriptor = JsonSerializer.Serialize(new { source = key, parentField });
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEGACY", "Original question",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, "LEGACY", false, false, 0, null, null, null, null, null, descriptor);
		var values = new Dictionary<string, string> { ["LEGACY"] = (key == "EducationMediums" ? offering.MediumId : offering.StandardId).ToString() };
		await service.ValidateAsync([field], values, offering, Guid.NewGuid(), true);
		values["LEGACY"] = Guid.NewGuid().ToString();
		(await Should.ThrowAsync<BusinessException>(() => service.ValidateAsync([field], values, offering, Guid.NewGuid(), true)))
			.Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
	}

	private static (AdmissionInquiryAcademicOptionsService Service, AdmissionOffering Offering) PublicValidationFixture()
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST", true, null, null);
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(offering.TenantId);
		var standards = Substitute.For<IRepository<Standard, Guid>>();
		standards.FindAsync(Arg.Any<Expression<Func<Standard, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new Standard(offering.StandardId, offering.TenantId, "Class 1", 5, offering.StageId, null));
		var mediums = Substitute.For<IRepository<EducationMedium, Guid>>();
		mediums.FindAsync(Arg.Any<Expression<Func<EducationMedium, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new EducationMedium(offering.MediumId, offering.TenantId, "English", offering.BoardId));
		var service = new AdmissionInquiryAcademicOptionsService(null!, standards, null!, Substitute.For<IRepository<EducationBoard, Guid>>(), mediums)
		{ LazyServiceProvider = new AbpLazyServiceProvider(new ServiceCollection().AddSingleton(tenant).BuildServiceProvider()) };
		return (service, offering);
	}
	[Theory]
	[InlineData("EducationMediums", null)]
	[InlineData("EducationMediums", "")]
	[InlineData("EducationMediums", "not-a-guid")]
	[InlineData("StandardsByMedium", null)]
	[InlineData("StandardsByMedium", "")]
	[InlineData("StandardsByMedium", "not-a-guid")]
	public async Task Public_linked_child_requires_a_valid_parent_even_when_questions_are_optional(string key, string? parentAnswer)
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST", true, null, null);
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(offering.TenantId);
		var standard = new Standard(offering.StandardId, offering.TenantId, "Class 1", 5, offering.StageId, null);
		var medium = new EducationMedium(offering.MediumId, offering.TenantId, "English", offering.BoardId);
		var standards = Substitute.For<IRepository<Standard, Guid>>();
		standards.FindAsync(Arg.Any<Expression<Func<Standard, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(standard);
		var boards = Substitute.For<IRepository<EducationBoard, Guid>>();
		var mediums = Substitute.For<IRepository<EducationMedium, Guid>>();
		mediums.FindAsync(Arg.Any<Expression<Func<EducationMedium, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(medium);
		var service = new AdmissionInquiryAcademicOptionsService(null!, standards, null!, boards, mediums)
		{ LazyServiceProvider = new AbpLazyServiceProvider(new ServiceCollection().AddSingleton(tenant).BuildServiceProvider()) };
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CHILD", "Optional child",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, "CHILD", false, false, 0,
			null, null, null, null, null, $"{{\"source\":\"{key}\",\"parentField\":\"PARENT\"}}");
		var values = new Dictionary<string, string> { ["CHILD"] = (key == "EducationMediums" ? offering.MediumId : offering.StandardId).ToString() };
		if (parentAnswer != null) values["PARENT"] = parentAnswer;
		var error = await Should.ThrowAsync<BusinessException>(() => service.ValidateAsync([field], values, offering, Guid.NewGuid(), true));
		error.Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
		values["CHILD"] = "";
		await service.ValidateAsync([field], values, offering, Guid.NewGuid(), true);
		var legacy = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEGACY", "Class interested in",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, "LEGACY", false, false, 0,
			null, null, null, null, null, "{\"source\":\"Standards\"}");
		await service.ValidateAsync([legacy], new Dictionary<string, string> { ["LEGACY"] = offering.StandardId.ToString() }, offering, Guid.NewGuid(), true);
	}
	[Fact]
	public void Class_label_edits_preserve_valid_linked_class_configuration()
	{
		AdmissionClassInterestQuestion.IsConfigured(AdmissionFormFieldType.Select, "{\"source\":\"StandardsByMedium\",\"parentField\":\"MEDIUM_2\"}").ShouldBeTrue();
		AdmissionClassInterestQuestion.IsConfigured(AdmissionFormFieldType.Select, "{\"source\":\"StandardsByMedium\"}").ShouldBeFalse();
	}
	[Fact]
	public void Board_labels_do_not_change_the_academic_question_identity()
	{
		AdmissionClassInterestQuestion.Matches("BOARD", "Class interested in", AdmissionFormBindingType.Custom, "BOARD", "{\"source\":\"EducationBoards\"}").ShouldBeFalse();
	}
	[Fact]
	public void Public_group_choices_are_bounded_to_the_authorized_offering_and_exact_parent()
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST", true, null, null);
		var boards = AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("EducationBoards", null, offering, "Class 1", "CBSE", "English");
		boards.Single().Value.ShouldBe(offering.BoardId.ToString()); boards[0].Label.ShouldBe("CBSE");
		var mediums = AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("EducationMediums", offering.BoardId, offering, "Class 1", "CBSE", "English");
		mediums.Single().Value.ShouldBe(offering.MediumId.ToString()); mediums[0].Label.ShouldBe("English");
		AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("EducationMediums", Guid.NewGuid(), offering, "Class 1", "CBSE", "English").ShouldBeEmpty();
		var classes = AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("StandardsByMedium", offering.MediumId, offering, "Class 1", "CBSE", "English");
		classes.Single().Value.ShouldBe(offering.StandardId.ToString());
		AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("StandardsByMedium", Guid.NewGuid(), offering, "Class 1", "CBSE", "English").ShouldBeEmpty();
		var legacy = AdmissionInquiryAcademicOptionsService.MapPublicAcademicOptions("Standards", null, offering, "Class 1", "CBSE", "English").Single();
		legacy.BoardId.ShouldBe(offering.BoardId); legacy.MediumId.ShouldBe(offering.MediumId);
	}
	[Theory]
	[InlineData("EducationBoards")]
	[InlineData("EducationMediums")]
	public async Task Academic_hierarchy_reads_require_an_owning_permission_and_do_not_open_siblings(string key)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		var access = new AdmissionFormOptionsAccessService(permissions);
		await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReadAsync(key));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(true);
		await access.EnsureReadAsync(key);
		await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReadAsync("StudentSiblings"));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View).Returns(true);
		await access.EnsureReadAsync(key);
	}
	[Fact]
	public void Maps_only_scoped_boards_and_mediums_with_exact_parent_IDs_and_class_context()
	{
		var board = Guid.NewGuid(); var otherBoard = Guid.NewGuid(); var medium = Guid.NewGuid(); var stage = Guid.NewGuid(); var standard = Guid.NewGuid();
		var hierarchy = new StudentDirectoryOptionsDto
		{
			Boards = [new() { Id = board, Name = "CBSE" }, new() { Id = otherBoard, Name = "State" }],
			Mediums = [new() { Id = medium, Name = "English", ParentId = board }, new() { Id = Guid.NewGuid(), Name = "Gujarati", ParentId = otherBoard }],
			Stages = [new() { Id = stage, Name = "Primary", ParentId = medium }],
			Standards = [new() { Id = standard, Name = "Class 1", ParentId = medium, StageId = stage }]
		};
		AdmissionFormDataSourceRegistry.MapAcademicOptions("EducationBoards", null, hierarchy).Select(option => option.Value)
			.ShouldBe([board.ToString(), otherBoard.ToString()]);
		AdmissionFormDataSourceRegistry.MapAcademicOptions("EducationMediums", board, hierarchy).Single().Value.ShouldBe(medium.ToString());
		AdmissionFormDataSourceRegistry.MapAcademicOptions("EducationMediums", Guid.NewGuid(), hierarchy).ShouldBeEmpty();
		var option = AdmissionFormDataSourceRegistry.MapAcademicOptions("Standards", null, hierarchy).Single();
		option.Value.ShouldBe(standard.ToString()); option.BoardId.ShouldBe(board); option.BoardLabel.ShouldBe("CBSE");
	}
}
