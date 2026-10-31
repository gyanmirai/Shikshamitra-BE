using System;
using System.Text.Json;
using Shouldly;
using Xunit;
using System.Threading.Tasks;
using NSubstitute;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAcademicOptionsUnit_Tests
{
	[Fact]
	public void Publication_rejects_class_alias_duplicates_and_manual_class_choices()
	{
		AdmissionFormFieldDefinition Question(string code, string label, string options) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			code, label, AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, code, false, false, 0, null, null, null, null, null, options);
		var first = Question("CLASS_INTERESTED", "Class interested in", "{\"source\":\"Standards\"}");
		var duplicate = Question("FIELD_1", "Interested Class", "[\"Class 1\"]");
		var result = new AdmissionFormValidationResultDto();
		AdmissionFormAppService.ValidateClassInterestQuestions([first, duplicate], result);
		result.Errors.ShouldContain(AdmissionClassInterestQuestion.DuplicateMessage);
		result.Errors.ShouldContain(AdmissionClassInterestQuestion.ConfigurationMessage);
		var valid = new AdmissionFormValidationResultDto();
		AdmissionFormAppService.ValidateClassInterestQuestions([first, Question("CLASS_APPLYING_FOR", "Class applying for", "{\"source\":\"Standards\"}")], valid);
		valid.Errors.ShouldBeEmpty();
		var heading = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CLASS_HEADING", "Interested Class",
			AdmissionFormFieldType.Static, AdmissionFormBindingType.Custom, "CLASS_HEADING", false, false, 0, null, null, null, null, null, null);
		AdmissionFormAppService.ValidateClassInterestQuestions([first, heading], valid);
		valid.Errors.ShouldBeEmpty();
	}
	[Fact]
	public async Task Runtime_academic_reads_accept_the_owning_View_without_opening_other_form_sources()
	{
		var permissions = Substitute.For<IPermissionChecker>();
		var access = new AdmissionFormOptionsAccessService(permissions);
		await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReadAsync("Standards"));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(true);
		await access.EnsureReadAsync("Standards");
		await access.EnsureReadAsync("DivisionsByStandard");
		await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReadAsync("StudentSiblings"));
		await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReadAsync("EducationBoards"));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View).Returns(true);
		await access.EnsureReadAsync("StandardsByMedium");
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View).Returns(true);
		await access.EnsureReadAsync("StudentSiblings");
	}

	[Theory]
	[InlineData("CLASS_INTERESTED", "Standards")]
	[InlineData("CLASS_APPLYING_FOR", "Standards")]
	[InlineData("CLASS", "StandardsByMedium")]
	[InlineData("DIVISION", "DivisionsByStandard")]
	public void Legacy_academic_choices_use_live_sources_without_rewriting_originals(string code, string key)
	{
		const string original = "[{\"value\":\"Old\",\"label\":\"Old\"}]";
		var resolved = AdmissionAcademicFormOptions.OptionsJson(code, AdmissionFormFieldType.Select, original);
		AdmissionFormMasterDataValidator.TrySource(resolved, out var source, out _).ShouldBeTrue();
		source.ShouldBe(key);
		AdmissionAcademicFormOptions.OptionsJson("HOUSE", AdmissionFormFieldType.Select, original).ShouldBe(original);
		AdmissionAcademicFormOptions.OptionsJson("CLASS", AdmissionFormFieldType.Text, original).ShouldBe(original);
	}

	[Fact]
	public void Resolved_class_choices_preserve_board_context_without_changing_class_IDs()
	{
		var boardId = Guid.NewGuid();
		var option = JsonSerializer.Deserialize<AdmissionFormOptionDto>(
			$"{{\"Value\":\"class-id\",\"Label\":\"Class 6\",\"BoardId\":\"{boardId}\",\"BoardLabel\":\"CBSE\"}}")!;
		using var json = JsonDocument.Parse(AdmissionAcademicFormOptions.SerializeOptions([option]));
		json.RootElement[0].GetProperty("boardId").GetGuid().ShouldBe(boardId);
		json.RootElement[0].GetProperty("boardLabel").GetString().ShouldBe("CBSE");
		json.RootElement[0].GetProperty("value").GetString().ShouldBe("class-id");
	}

	[Fact]
	public void Public_academic_choices_use_the_runtime_renderer_JSON_contract()
	{
		using var json = JsonDocument.Parse(AdmissionAcademicFormOptions.SerializeOptions([
			new() { Value = "class-id", Label = "Preparatory", DisplayOrder = 2, IsActive = true }
		]));
		var option = json.RootElement[0];
		option.GetProperty("value").GetString().ShouldBe("class-id");
		option.GetProperty("label").GetString().ShouldBe("Preparatory");
		option.GetProperty("isActive").GetBoolean().ShouldBeTrue();
	}
}
