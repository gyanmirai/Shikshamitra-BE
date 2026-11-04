using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Archive_availability_catalogue_and_capture_agree_on_unapproved_inquiry_version() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var registration = await _formRepository.GetAsync(offering.ApplicationFormDefinitionId!.Value);
			registration.SetProperty("AdmissionFormInheritanceV1", JsonSerializer.Serialize(new AdmissionFormInheritancePolicyDto { Enabled = true }));
			await _formRepository.UpdateAsync(registration, autoSave: true);
		});
		var reader = GetRequiredService<AdmissionOfferingReadService>();
		var catalogue = await WithUnitOfWorkAsync(() => reader.GetCatalogueAsync([offering.AdmissionCycleId]));
		catalogue.Single(item => item.Row.Id == offering.Id).Row.ConfigurationStatus.ShouldBe(AdmissionOfferingConfigurationStatus.Incomplete);
		var ready = await WithUnitOfWorkAsync(() => reader.GetListAsync(new()
		{
			AdmissionCycleId = offering.AdmissionCycleId, ConfigurationStatus = AdmissionOfferingConfigurationStatus.Ready
		}));
		ready.Items.ShouldBeEmpty();
		var context = await WithUnitOfWorkAsync(() => GetRequiredService<IAdmissionInquiryCaptureAppService>().GetContextAsync(offering.Id));
		context.Availability.CanCreateInquiry.ShouldBeFalse();
		context.Availability.ReasonMessage.ShouldNotBeNull().ShouldContain("approve", Case.Insensitive);
		context.Form.ShouldBeNull();
	});

	[Fact]
	public Task Archive_availability_catalogue_detects_workflow_registration_form_mismatch() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(new(
				Guid.NewGuid(), offering.WorkflowDefinitionId!.Value, "OTHER_REGISTRATION", "Other registration form",
				AdmissionWorkflowStageType.Form, 9, true, false, null,
				JsonSerializer.Serialize(new { formDefinitionId = Guid.NewGuid() })), autoSave: true);
		});
		var reader = GetRequiredService<AdmissionOfferingReadService>();
		var catalogue = await WithUnitOfWorkAsync(() => reader.GetCatalogueAsync([offering.AdmissionCycleId]));
		catalogue.Single(item => item.Row.Id == offering.Id).Row.AvailabilityStatus.ShouldBe(AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete);
		var incomplete = await WithUnitOfWorkAsync(() => reader.GetListAsync(new()
		{
			AdmissionCycleId = offering.AdmissionCycleId, ConfigurationStatus = AdmissionOfferingConfigurationStatus.Incomplete
		}));
		incomplete.Items.ShouldContain(item => item.Id == offering.Id);
	});

	[Fact]
	public Task Archive_availability_retains_inactive_reason_before_form_configuration_blocker() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = await _formRepository.GetAsync(offering.InquiryFormDefinitionId!.Value);
			inquiry.Archive();
			await _formRepository.UpdateAsync(inquiry, autoSave: true);
			offering.Deactivate();
			await _offeringRepository.UpdateAsync(offering, autoSave: true);
		});
		var available = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionOfferingReadService>().GetAvailabilityAsync(offering.Id));
		available.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.Inactive);
		available.ReasonCode.ShouldBe(AdmissionErrorCodes.OfferingInactive);
		available.ReasonMessage.ShouldNotBeNull().ShouldContain("inactive", Case.Insensitive);
	});

	[Fact]
	public Task Archive_availability_public_capture_does_not_expose_staff_configuration_metadata() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var form = await _formRepository.GetAsync(offering.ApplicationFormDefinitionId!.Value);
			form.Archive();
			await _formRepository.UpdateAsync(form, autoSave: true);
		});
		var available = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAvailabilityService>().ReadForPublicAsync(offering));
		available.CanCreateInquiry.ShouldBeFalse();
		available.ConfigurationBlocker.ShouldBeNull();
		available.Reason.ShouldNotBeNull().ShouldContain("Contact the school");
	});
}
