using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionEntryMode.DirectRegistration, false)]
	[InlineData(AdmissionEntryMode.Both, true)]
	public Task Journey_identity_direct_capture_guarantees_do_not_leak_into_an_inquiry_path(AdmissionEntryMode mode, bool identityMissing) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var registrationId = offering.ApplicationFormDefinitionId!.Value;
			var fields = await _formFieldRepository.GetListAsync(field => field.FormDefinitionId == registrationId);
			foreach (var field in fields.Where(field => field.BindingKey is "Applicant.FirstName" or "Applicant.LastName" or "Applicant.DateOfBirth" or "Applicant.Gender"))
				await _formFieldRepository.DeleteAsync(field, true);
			var sectionId = Guid.NewGuid();
			await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(sectionId, registrationId,
				"DIRECT_SURNAME", "Student surname", 99, null), true);
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), registrationId, sectionId,
				"DIRECT_LAST_NAME", "Surname", AdmissionFormFieldType.Text, AdmissionFormBindingType.System, "Applicant.LastName",
				true, false, 0, null, null, null, null, null), true);
			var inquiryFields = await _formFieldRepository.GetListAsync(field => field.FormDefinitionId == offering.InquiryFormDefinitionId!.Value);
			foreach (var field in inquiryFields.Where(field => field.BindingKey == "Applicant.Gender"))
				await _formFieldRepository.DeleteAsync(field, true);
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.UpdateRouting(AdmissionTrack.Management, mode, false);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var activation = await WithUnitOfWorkAsync(async () =>
			await _offeringValidator.ValidateForActivationAsync(await _offeringRepository.GetAsync(offering.Id)));
		activation.Errors.Any(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete).ShouldBe(identityMissing);
	});

	[Fact]
	public Task Journey_identity_is_checked_on_activation_without_disabling_existing_legacy_runtime() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var registrationFields = await _formFieldRepository.GetListAsync(field => field.FormDefinitionId == offering.ApplicationFormDefinitionId!.Value);
			foreach (var field in registrationFields.Where(field => field.BindingKey is "Applicant.LastName" or "Applicant.Gender"))
				await _formFieldRepository.DeleteAsync(field, true);
			var inquiryFields = await _formFieldRepository.GetListAsync(field => field.FormDefinitionId == offering.InquiryFormDefinitionId!.Value);
			foreach (var field in inquiryFields.Where(field => field.BindingKey is "Applicant.LastName" or "Applicant.Gender"))
				await _formFieldRepository.DeleteAsync(field, true);
		});
		var runtime = await WithUnitOfWorkAsync(async () =>
			await _offeringValidator.ValidateStoredAsync(await _offeringRepository.GetAsync(offering.Id)));
		runtime.Errors.ShouldNotContain(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
		var activation = await WithUnitOfWorkAsync(async () =>
			await _offeringValidator.ValidateForActivationAsync(await _offeringRepository.GetAsync(offering.Id)));
		var missing = activation.Errors.Single(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
		missing.Message.ShouldContain("Applicant.LastName");
		missing.Message.ShouldContain("Applicant.Gender");
		missing.ActivationOnly.ShouldBeTrue();
		AdmissionOfferingConfigurationValidator.GetBlockingErrors(new([missing]), isActive: false).ShouldBeEmpty();
		AdmissionOfferingConfigurationValidator.GetBlockingErrors(new([missing]), isActive: true).Count.ShouldBe(1);
	});
}
