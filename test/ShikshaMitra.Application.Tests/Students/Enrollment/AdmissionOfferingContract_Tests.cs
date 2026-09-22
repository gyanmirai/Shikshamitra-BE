using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOfferingContract_Tests
{
	[Fact]
	public void Bulk_contract_rejects_duplicate_standards()
	{
		var standardId = Guid.NewGuid();
		var input = ValidBulkInput();
		input.Items.Add(new BulkAdmissionOfferingItemDto { StandardId = standardId, Code = "GRADE-1" });
		input.Items.Add(new BulkAdmissionOfferingItemDto { StandardId = standardId, Code = "GRADE-1-COPY" });

		var result = new BulkCreateAdmissionOfferingsDtoValidator().Validate(input);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.ErrorMessage.Contains("only once"));
	}

	[Fact]
	public void Override_window_requires_dates_in_order()
	{
		var input = ValidBulkInput();
		input.UseCycleDates = false;
		input.OpenDate = new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero);
		input.CloseDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

		new BulkCreateAdmissionOfferingsDtoValidator().Validate(input).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Incomplete_configuration_can_be_saved_inactive_but_cannot_be_activated()
	{
		var validation = new AdmissionOfferingValidationResult(
			[new AdmissionOfferingValidationError(
				AdmissionOfferingValidationCodes.WorkflowMissing,
				"Published Admission Workflow is required.")]);

		AdmissionOfferingConfigurationValidator.CanSave(validation, isActive: false).ShouldBeTrue();
		AdmissionOfferingConfigurationValidator.CanSave(validation, isActive: true).ShouldBeFalse();
	}

	[Theory]
	[InlineData(AdmissionOfferingConfigurationStatus.Incomplete, false)]
	[InlineData(AdmissionOfferingConfigurationStatus.Ready, true)]
	public void Offering_contract_exposes_activation_readiness(
		AdmissionOfferingConfigurationStatus status,
		bool expected)
	{
		new AdmissionOfferingListDto { ConfigurationStatus = status }.CanActivate.ShouldBe(expected);
	}

	[Fact]
	public void Invalid_scope_cannot_be_saved_even_when_inactive()
	{
		var validation = new AdmissionOfferingValidationResult(
			[new AdmissionOfferingValidationError(
				AdmissionOfferingValidationCodes.HierarchyInvalid,
				"The academic hierarchy is invalid.")]);

		AdmissionOfferingConfigurationValidator.CanSave(validation, isActive: false).ShouldBeFalse();
	}

	[Fact]
	public void Offering_actions_are_exposed_through_only_view_add_and_delete_permissions()
	{
		ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.All.ShouldBe([
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View,
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add,
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete
		]);
		ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Clone
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Deactivate
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete);
	}

	[Theory]
	[InlineData(nameof(AdmissionOfferingAppService.CreateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Create)]
	[InlineData(nameof(AdmissionOfferingAppService.UpdateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Edit)]
	[InlineData(nameof(AdmissionOfferingAppService.ActivateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Activate)]
	[InlineData(nameof(AdmissionOfferingAppService.DeactivateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Deactivate)]
	[InlineData(nameof(AdmissionOfferingAppService.DeleteAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Delete)]
	[InlineData(nameof(AdmissionOfferingAppService.CloneAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Clone)]
	[InlineData(nameof(AdmissionOfferingAppService.ValidateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Validate)]
	[InlineData(nameof(AdmissionOfferingAppService.BulkCreateAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.BulkCreate)]
	public void Command_endpoints_require_the_expected_three_permission_alias(string methodName, string permission)
	{
		var method = typeof(AdmissionOfferingAppService).GetMethods().Single(value => value.Name == methodName);

		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == permission);
	}

	private static BulkCreateAdmissionOfferingsDto ValidBulkInput() => new()
	{
		BoardId = Guid.NewGuid(),
		MediumId = Guid.NewGuid(),
		StageId = Guid.NewGuid(),
		UseCycleDates = true,
		WorkflowDefinitionId = Guid.NewGuid(),
		InquiryFormDefinitionId = Guid.NewGuid(),
		ApplicationFormDefinitionId = Guid.NewGuid()
	};
}
