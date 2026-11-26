using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionFormPurpose.Inquiry)]
	[InlineData(AdmissionFormPurpose.Registration)]
	public Task Form_address_links_are_validated_independently_on_publication(AdmissionFormPurpose purpose) => RunCertificationScenarioAsync(async () =>
	{
		var draft = await _formAppService.CreateAsync(new()
		{
			Name = $"{purpose} address publication",
			Purpose = purpose,
			Sections =
			[
				AddressValidationSection("ADDRESS", 0, null,
					AddressValidationField("CURRENT_COUNTRY", "Address.Country", "country"),
					AddressValidationField("CURRENT_STATE", "Address.State", "state", "CURRENT_COUNTRY"),
					AddressValidationField("CURRENT_CITY", "Address.City", "city", "CURRENT_COUNTRY", "CURRENT_STATE"),
					AddressValidationField("PERMANENT_STATE", "PermanentAddress.State", "state", "CURRENT_COUNTRY"),
					AddressValidationField("PERMANENT_CITY", "PermanentAddress.City", "city", "REMOVED_COUNTRY", "CURRENT_STATE"))
			]
		});
		var errors = new[] { "Admissions.FormAddressMissingCountry:PERMANENT_STATE", "Admissions.FormAddressMissingCityDependencies:PERMANENT_CITY" };
		(await _formAppService.ValidateAsync(draft.Id)).Errors.ShouldBe(errors);
		var failure = await Should.ThrowAsync<BusinessException>(() => _formAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp));
		failure.Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		failure.Data["Errors"].ShouldBe(string.Join(';', errors));
		(await _formAppService.GetAsync(draft.Id)).Status.ShouldBe(AdmissionFormStatus.Draft);

		var input = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(draft))!;
		var address = input.Sections.Single(section => section.Code == "ADDRESS");
		address.Fields.RemoveAll(field => field.Code.StartsWith("PERMANENT_", StringComparison.Ordinal));
		draft = await _formAppService.UpdateAsync(draft.Id, input);
		(await _formAppService.ValidateAsync(draft.Id)).IsValid.ShouldBeTrue();
		(await _formAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp)).Status.ShouldBe(AdmissionFormStatus.Published);
	});

	[Fact]
	public Task Form_address_order_follows_steps_before_sections_and_fields() => RunCertificationScenarioAsync(async () =>
	{
		var draft = await _formAppService.CreateAsync(new()
		{
			Name = "Stepper address publication",
			Purpose = AdmissionFormPurpose.Registration,
			LayoutType = AdmissionFormLayoutType.Stepper,
			Sections =
			[
				AddressValidationSection("COUNTRY_STEP", 0, 2, AddressValidationField("COUNTRY", null, "country")),
				AddressValidationSection("STATE_STEP", 1, 1, AddressValidationField("STATE", null, "state", "COUNTRY")),
				AddressValidationSection("CITY_STEP", 2, 3, AddressValidationField("CITY", null, "city", "COUNTRY", "STATE"))
			]
		});
		(await _formAppService.ValidateAsync(draft.Id)).Errors.ShouldBe(["Admissions.FormAddressOrder:STATE"]);

		var input = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(draft))!;
		input.Sections.Single(section => section.Code == "COUNTRY_STEP").StepDisplayOrder = 0;
		draft = await _formAppService.UpdateAsync(draft.Id, input);
		(await _formAppService.ValidateAsync(draft.Id)).IsValid.ShouldBeTrue();
	});

	[Fact]
	public Task Form_address_dependency_validation_uses_the_effective_registration() => RunCertificationScenarioAsync(async () =>
	{
		var inquiry = await _formAppService.CreateAsync(new()
		{
			Name = "Legacy Inquiry with out-of-order address selectors",
			Purpose = AdmissionFormPurpose.Inquiry,
			Sections =
			[
				AddressValidationSection("ADDRESS", 0, null,
					AddressValidationField("STATE", "Address.State", "state", "COUNTRY"),
					AddressValidationField("COUNTRY", "Address.Country", "country"))
			]
		});
		// Simulate an Inquiry already published before address order became a publication rule.
		await WithUnitOfWorkAsync(async () =>
		{
			var source = await _formRepository.GetAsync(inquiry.Id);
			source.Publish(null);
			await _formRepository.UpdateAsync(source, autoSave: true);
		});
		var registration = await _formAppService.CreateAsync(new()
		{
			Name = "Registration with inherited address parents",
			Purpose = AdmissionFormPurpose.Registration,
			Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = inquiry.Id }] }
		});
		var preview = await _formAppService.PreviewInheritanceAsync(new()
		{
			RegistrationFormDefinitionId = registration.Id,
			InquiryFormDefinitionId = inquiry.Id
		});
		var issue = preview.Issues.ShouldHaveSingleItem();
		issue.Code.ShouldBe("Admissions.FormAddressOrder:STATE");
		issue.Message.ShouldBe("Country must come before State, and State before City.");
		preview.Definition.Sections.SelectMany(section => section.Fields).ShouldContain(field => field.Code == "COUNTRY" && field.InheritedFrom != null);
		(await _formAppService.ValidateAsync(registration.Id)).Errors.ShouldBe([issue.Code]);
		AdmissionFormInheritanceService.Compose(registration, await _formAppService.GetAsync(inquiry.Id), true).IsValid.ShouldBeTrue();
	});

	private static CreateUpdateAdmissionFormSectionDto AddressValidationSection(
		string code, int displayOrder, int? stepOrder, params CreateUpdateAdmissionFormFieldDto[] fields)
	{
		for (var index = 0; index < fields.Length; index++) fields[index].DisplayOrder = index;
		return new()
		{
			Code = code, Name = code, DisplayOrder = displayOrder, StepDisplayOrder = stepOrder,
			StepId = stepOrder.HasValue ? Guid.NewGuid() : null, Fields = fields.ToList()
		};
	}

	private static CreateUpdateAdmissionFormFieldDto AddressValidationField(
		string code, string? binding, string format, string? countryField = null, string? stateField = null) => new()
	{
		Code = code, Label = code == "PERMANENT_CITY" ? "Permanent city" : format,
		FieldType = AdmissionFormFieldType.Text,
		BindingType = binding == null ? AdmissionFormBindingType.Custom : AdmissionFormBindingType.System,
		BindingKey = binding ?? code, ValidationJson = JsonSerializer.Serialize(new { format, countryField, stateField })
	};
}
