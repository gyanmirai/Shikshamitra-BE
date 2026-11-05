using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("father", "Father")]
	[InlineData("mother", "Mother")]
	[InlineData("guardian", "Guardian")]
	public Task Registration_relationship_prefill_preserves_saved_answers_and_clears_without_changing_the_source_role(string role, string display) => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var section = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), formId, "CONTACT", "Contact", 0, null), true);
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id,
				"GUARDIAN_RELATION", "Relationship to student", AdmissionFormFieldType.Text, AdmissionFormBindingType.System,
				"Guardian.Relationship", false, false, 0, null, null, null, null, null), true);
		});
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		offering = await WithUnitOfWorkAsync(async () =>
		{
			var current = await _offeringRepository.GetAsync(offering.Id);
			current.ConfigureReferences(workflow.Id, current.InquiryFormDefinitionId, formId);
			return await _offeringRepository.UpdateAsync(current, true);
		});
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = PrimaryContactInquiryInput(offering, role);
		input.GuardianResponsibilityConfirmed = true;
		var inquiry = await inquiries.CreateAsync(input);
		inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		var applicantId = inquiry.ApplicantId!.Value;
		var application = await _applicationAppService.StartAsync(applicantId, offering.Id, inquiry.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(value => value.FieldCode == "GUARDIAN_RELATION").ValueJson).ShouldBe(display);
		var values = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
		var histories = GetRequiredService<IRepository<AdmissionFormChangeHistory, Guid>>();
		// Model a pre-fix draft which never had this value. Reads may suggest it but must not write it.
		await WithUnitOfWorkAsync(() => values.DeleteAsync(value => value.FormInstanceId == form.FormInstanceId && value.FieldCode == "GUARDIAN_RELATION", true));
		var valueCount = await WithUnitOfWorkAsync(() => values.CountAsync());
		var historyCount = await WithUnitOfWorkAsync(() => histories.CountAsync());
		form = await _applicationAppService.GetFormAsync(application.Id);
		form.AutoFilledFieldCodes.ShouldContain("GUARDIAN_RELATION");
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(value => value.FieldCode == "GUARDIAN_RELATION").ValueJson).ShouldBe(display);
		(await WithUnitOfWorkAsync(() => values.CountAsync())).ShouldBe(valueCount);
		(await WithUnitOfWorkAsync(() => histories.CountAsync())).ShouldBe(historyCount);
		await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
			AutoFilledFieldCodes = ["GUARDIAN_RELATION"],
			Values = [new() { FieldCode = "GUARDIAN_RELATION", ValueJson = System.Text.Json.JsonSerializer.Serialize(display) }]
		});
		foreach (var saved in new[] { "Aunt", "" })
		{
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
				Values = [new() { FieldCode = "GUARDIAN_RELATION", ValueJson = System.Text.Json.JsonSerializer.Serialize(saved) }]
			});
			var reloaded = await _applicationAppService.GetFormAsync(application.Id);
			AdmissionApplicationValueConverter.Unwrap(reloaded.Values.Single(value => value.FieldCode == "GUARDIAN_RELATION").ValueJson).ShouldBe(saved);
			reloaded.AutoFilledFieldCodes.ShouldNotContain("GUARDIAN_RELATION");
		}
		var unchanged = await _applicationAppService.GetAsync(application.Id);
		unchanged.ApplicationNumber.ShouldBe(application.ApplicationNumber);
		unchanged.Status.ShouldBe(application.Status);
		AdmissionPrimaryContactSelection.ReadRelationship(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicantId))).ShouldBe(role);
		(await inquiries.GetAsync(inquiry.Id)).PrimaryContactRelationship.ShouldBe(role);
	});
}
