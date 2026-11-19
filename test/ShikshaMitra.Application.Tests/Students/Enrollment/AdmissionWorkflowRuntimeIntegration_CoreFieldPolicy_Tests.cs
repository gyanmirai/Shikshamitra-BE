using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Malformed_linked_form_metadata_blocks_its_offering_and_keeps_other_rows_readable() => RunCertificationScenarioAsync(async () =>
	{
		var invalid = await CreateOptionalIntakeOfferingAsync();
		var valid = await CreateOptionalIntakeOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var form = await _formRepository.GetAsync(invalid.ApplicationFormDefinitionId!.Value);
			form.SetProperty("AdmissionFormInheritanceV1", "{invalid-json");
			await _formRepository.UpdateAsync(form, true);
			var offering = await _offeringRepository.GetAsync(invalid.Id);
			offering.Deactivate();
			await _offeringRepository.UpdateAsync(offering, true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<AdmissionOfferingReadService>();
			var rows = await reader.GetListAsync(new() { MaxResultCount = 1000 });
			rows.Items.Single(row => row.Id == invalid.Id).ConfigurationStatus.ShouldBe(AdmissionOfferingConfigurationStatus.Incomplete);
			rows.Items.ShouldContain(row => row.Id == valid.Id);
			var detail = await reader.GetAsync(invalid.Id);
			detail.Configuration.Errors.ShouldContain(error => error.Code == AdmissionFormInheritanceErrorCodes.IncompatibleSource);
			detail.CanActivate.ShouldBeFalse();
			(await reader.GetAsync(valid.Id)).Id.ShouldBe(valid.Id);
		});
	});

	[Fact]
	public Task Core_field_policy_is_server_owned_on_create_update_and_publication() => RunCertificationScenarioAsync(async () =>
	{
		var service = GetRequiredService<IAdmissionFormAppService>();
		var form = await service.CreateAsync(new() { Name = "Core inquiry", Purpose = AdmissionFormPurpose.Inquiry });
		form.CoreFieldPolicyVersion.ShouldBe(1);
		form.Sections.SelectMany(section => section.Fields).Count().ShouldBe(6);
		(await service.ValidateAsync(form.Id)).Errors.ShouldBeEmpty();
		var edit = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(form))!;
		edit.CoreFieldPolicyVersion = null;
		var email = edit.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Guardian.Email");
		email.Required = false;
		var blocked = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(form.Id, edit));
		blocked.Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		email.Required = true; email.Label = "School contact email";
		var saved = await service.UpdateAsync(form.Id, edit);
		saved.CoreFieldPolicyVersion.ShouldBe(1);
		saved.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Guardian.Email").Label.ShouldBe("School contact email");
		var stale = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(form.Id, edit));
		stale.Code.ShouldBe("Admissions.FormConcurrencyConflict");
		var published = await service.PublishAsync(saved.Id, saved.ConcurrencyStamp);
		var next = await service.CreateNewVersionAsync(published.Id);
		next.CoreFieldPolicyVersion.ShouldBe(1);
		next.ParentVersionId.ShouldBe(published.Id);
		(await service.GetAsync(published.Id)).Status.ShouldBe(AdmissionFormStatus.Published);
	});

	[Fact]
	public Task Core_field_policy_clone_upgrades_only_the_new_definition_and_keeps_historical_full_name_schema() => RunCertificationScenarioAsync(async () =>
	{
		var sourceId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
		var forms = GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			var source = new AdmissionFormDefinition(sourceId, TestTenantId, "Legacy full name", "LEGACY_" + sourceId.ToString("N"), AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
			source.Publish(TestUserId);
			await forms.InsertAsync(source, false);
			await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(new(sectionId, sourceId, "STUDENT", "Student", 0, null), false);
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), sourceId, sectionId, "STUDENT_NAME", "Student name", AdmissionFormFieldType.Text,
				AdmissionFormBindingType.System, "Applicant.Name", true, false, 0, null, null, null, null, null), false);
		});
		var service = GetRequiredService<IAdmissionFormAppService>();
		var original = await service.GetAsync(sourceId);
		original.CoreFieldPolicyVersion.ShouldBeNull();
		var clone = await service.CloneAsync(sourceId, new() { Name = "New inquiry" });
		clone.CoreFieldPolicyVersion.ShouldBe(1);
		clone.Sections.SelectMany(section => section.Fields).ShouldNotContain(field => field.BindingKey == "Applicant.Name");
		clone.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Applicant.LastName").Required.ShouldBeFalse();
		(await service.ValidateAsync(clone.Id)).Errors.ShouldBeEmpty();
		var retained = await service.GetAsync(sourceId);
		JsonSerializer.Serialize(retained).ShouldBe(JsonSerializer.Serialize(original));
	});
}
