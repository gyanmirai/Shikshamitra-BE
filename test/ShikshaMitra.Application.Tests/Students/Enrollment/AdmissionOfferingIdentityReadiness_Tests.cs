using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inactive_offering_identity_readiness_matches_activation_before_the_user_confirms() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var ids = new[] { offering.ApplicationFormDefinitionId!.Value, offering.InquiryFormDefinitionId!.Value };
			foreach (var field in await _formFieldRepository.GetListAsync(field => Enumerable.Contains(ids, field.FormDefinitionId) && field.BindingKey == "Applicant.Gender"))
				await _formFieldRepository.DeleteAsync(field, true);
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.Deactivate();
			await _offeringRepository.UpdateAsync(stored, true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<AdmissionOfferingReadService>();
			var detail = await reader.GetAsync(offering.Id);
			detail.CanActivate.ShouldBeFalse();
			var error = detail.Configuration.Errors.Single(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
			error.Message.ShouldContain("Gender");
			error.Message.ShouldNotContain("Applicant.");
			error.FormDefinitionId.ShouldBe(offering.ApplicationFormDefinitionId);
			var rows = await reader.GetListAsync(new() { AdmissionCycleId = offering.AdmissionCycleId, MaxResultCount = 1000 });
			rows.Items.Single(row => row.Id == offering.Id).CanActivate.ShouldBeFalse();
			var ready = await reader.GetListAsync(new() { AdmissionCycleId = offering.AdmissionCycleId, ConfigurationStatus = AdmissionOfferingConfigurationStatus.Ready, MaxResultCount = 1000 });
			ready.Items.ShouldNotContain(row => row.Id == offering.Id);
		});
		// A previously active legacy route must not become unavailable just because the
		// activation prerequisite is now surfaced earlier for inactive offerings.
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.Activate();
			await _offeringRepository.UpdateAsync(stored, true);
			var validation = await _offeringValidator.ValidateStoredAsync(stored);
			validation.Errors.ShouldNotContain(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
			var detail = await GetRequiredService<AdmissionOfferingReadService>().GetAsync(offering.Id);
			detail.Configuration.Errors.ShouldNotContain(error => error.Code == AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete);
		});
	});
}
