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
	public void Bulk_contract_rejects_a_null_item_list_without_throwing()
	{
		var input = ValidBulkInput();
		input.Items = null!;

		var result = new BulkCreateAdmissionOfferingsDtoValidator().Validate(input);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(input.Items));
	}

	[Fact]
	public void Offering_contract_rejects_unsupported_track_and_entry_mode()
	{
		var input = new CreateAdmissionOfferingDto
		{
			BoardId = Guid.NewGuid(), MediumId = Guid.NewGuid(), StageId = Guid.NewGuid(),
			StandardId = Guid.NewGuid(), Code = "GRADE-5-RTE", UseCycleDates = true,
			Track = (AdmissionTrack)99, EntryMode = (AdmissionEntryMode)99
		};

		var result = new CreateAdmissionOfferingDtoValidator().Validate(input);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(input.Track));
		result.Errors.ShouldContain(error => error.PropertyName == nameof(input.EntryMode));
	}

	[Fact]
	public void Direct_registration_requires_system_owned_identity_and_valid_family_choice()
	{
		var input = new StartAdmissionDirectRegistrationDto
		{
			RequestId = Guid.NewGuid(), AdmissionOfferingId = Guid.NewGuid(),
			ApplicantFirstName = "Asha", ApplicantDateOfBirth = new DateTime(2018, 5, 15),
			ApplicantGender = "Female", GuardianFirstName = "Ravi", GuardianMobile = "9876543210",
			ExistingFamilyId = Guid.NewGuid(), CreateSeparateFamily = true
		};

		var result = new StartAdmissionDirectRegistrationDtoValidator().Validate(input);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.PropertyName == nameof(input.CreateSeparateFamily));
		input.CreateSeparateFamily = false;
		new StartAdmissionDirectRegistrationDtoValidator().Validate(input).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Application_counsellor_assignment_requires_identity_and_concurrency()
	{
		var validator = new AssignAdmissionApplicationCounsellorDtoValidator();
		var input = new AssignAdmissionApplicationCounsellorDto
		{
			CounsellorId = Guid.Empty, ConcurrencyStamp = null
		};
		validator.Validate(input).IsValid.ShouldBeFalse();
		input.CounsellorId = Guid.NewGuid();
		input.ConcurrencyStamp = "current";
		validator.Validate(input).IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(nameof(AdmissionApplicationAppService.GetAssignableCounsellorsAsync),
		ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[InlineData(nameof(AdmissionApplicationAppService.AssignCounsellorAsync),
		ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public void Application_counsellor_operations_have_exact_permissions(string methodName, string permission)
	{
		var method = typeof(AdmissionApplicationAppService).GetMethods().Single(value => value.Name == methodName);
		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>().ShouldContain(attribute => attribute.Policy == permission);
	}

	[Theory]
	[InlineData(nameof(AdmissionApplicationAppService.StartDirectRegistrationAsync))]
	[InlineData(nameof(AdmissionApplicationAppService.FindDirectRegistrationFamiliesAsync))]
	public void Direct_registration_operations_require_application_start_permission(string methodName)
	{
		var method = typeof(AdmissionApplicationAppService).GetMethods().Single(value => value.Name == methodName);
		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy ==
				ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start);
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
