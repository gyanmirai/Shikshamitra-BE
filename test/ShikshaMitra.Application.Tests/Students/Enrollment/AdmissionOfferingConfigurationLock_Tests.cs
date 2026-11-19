using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("workflow")]
	[InlineData("inquiry")]
	[InlineData("registration")]
	public Task Offering_configuration_rejects_reference_changes_while_active(string reference) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var before = await service.GetAsync(offering.Id);
		before.CanEditConfiguration.ShouldBeFalse();
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(before);
		update.ConcurrencyStamp = before.ConcurrencyStamp;
		if (reference == "workflow") update.WorkflowDefinitionId = Guid.NewGuid();
		if (reference == "inquiry") update.InquiryFormDefinitionId = Guid.NewGuid();
		if (reference == "registration") update.ApplicationFormDefinitionId = Guid.NewGuid();

		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(offering.Id, update)))
			.Code.ShouldBe("Admissions.Offering.ConfigurationLocked");
		var after = await service.GetAsync(offering.Id);
		after.WorkflowDefinitionId.ShouldBe(before.WorkflowDefinitionId);
		after.InquiryFormDefinitionId.ShouldBe(before.InquiryFormDefinitionId);
		after.ApplicationFormDefinitionId.ShouldBe(before.ApplicationFormDefinitionId);
	});

	[Fact]
	public Task Offering_configuration_rejects_changes_after_a_deleted_legacy_inquiry() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var inquiryRepository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "LOCK-LEGACY", offering.AdmissionCycleId,
				offering.Id, "Historical Inquiry", "9876543210", null, AdmissionInquirySource.Manual, null);
			await inquiryRepository.InsertAsync(inquiry, true);
			await inquiryRepository.DeleteAsync(inquiry, true);
		});
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var before = await service.GetAsync(offering.Id);
		before = await service.DeactivateAsync(offering.Id, before.ConcurrencyStamp);
		before.InquiryCount.ShouldBe(0);
		before.HasConfigurationUsage.ShouldBeTrue();
		before.CanEditConfiguration.ShouldBeFalse();
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(before);
		update.ConcurrencyStamp = before.ConcurrencyStamp;
		update.InquiryFormDefinitionId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(offering.Id, update)))
			.Code.ShouldBe("Admissions.Offering.ConfigurationLocked");
	});

	[Fact]
	public Task Offering_configuration_stays_locked_when_its_first_captured_inquiry_moves_away() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var target = new AdmissionOffering(Guid.NewGuid(), TestTenantId, offering.AdmissionCycleId, offering.CampusId,
			offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, "LOCK-TARGET", true, null, null,
			track: AdmissionTrack.Management);
		target.ConfigureReferences(offering.WorkflowDefinitionId, offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId);
		await WithUnitOfWorkAsync(() => _offeringRepository.InsertAsync(target, true));
		var inquiry = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>()
			.CreateFromPublicLinkAsync(ManagedInquiryInput(offering), null, offering.InquiryFormDefinitionId));
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var stored = await repository.GetAsync(inquiry.Id);
			stored.ConfirmOffering(target.Id);
			await repository.UpdateAsync(stored, true);
		});
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var before = await service.GetAsync(offering.Id);
		before = await service.DeactivateAsync(offering.Id, before.ConcurrencyStamp);
		before.InquiryCount.ShouldBe(0);
		before.HasConfigurationUsage.ShouldBeTrue();
		before.CanEditConfiguration.ShouldBeFalse();
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(before);
		update.ConcurrencyStamp = before.ConcurrencyStamp;
		update.WorkflowDefinitionId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(offering.Id, update)))
			.Code.ShouldBe("Admissions.Offering.ConfigurationLocked");
	});

	[Fact]
	public Task Offering_configuration_allows_inactive_unused_replacement_and_derives_the_registration_form() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var replacement = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var before = await service.GetAsync(offering.Id);
		before = await service.DeactivateAsync(offering.Id, before.ConcurrencyStamp);
		before.CanEditConfiguration.ShouldBeTrue();
		before.HasConfigurationUsage.ShouldBeFalse();
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(before);
		update.ConcurrencyStamp = before.ConcurrencyStamp;
		update.WorkflowDefinitionId = replacement.WorkflowDefinitionId;
		update.InquiryFormDefinitionId = replacement.InquiryFormDefinitionId;
		update.ApplicationFormDefinitionId = null;
		var after = await service.UpdateAsync(offering.Id, update);
		after.IsActive.ShouldBeFalse();
		after.WorkflowDefinitionId.ShouldBe(replacement.WorkflowDefinitionId);
		after.ApplicationFormDefinitionId.ShouldBe(replacement.ApplicationFormDefinitionId);
	});

	[Fact]
	public Task Offering_configuration_allows_active_operational_edits_with_an_omitted_derived_form() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var before = await service.GetAsync(offering.Id);
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(before);
		update.ConcurrencyStamp = before.ConcurrencyStamp;
		update.Name = "Updated operational name";
		update.ApplicationFormDefinitionId = null;
		update.Track = AdmissionTrack.Management;
		update.EntryMode = AdmissionEntryMode.DirectRegistration;
		var after = await service.UpdateAsync(offering.Id, update);
		after.Name.ShouldBe(update.Name);
		after.IsActive.ShouldBeTrue();
		after.WorkflowDefinitionId.ShouldBe(before.WorkflowDefinitionId);
		after.InquiryFormDefinitionId.ShouldBe(before.InquiryFormDefinitionId);
		after.ApplicationFormDefinitionId.ShouldBe(before.ApplicationFormDefinitionId);
		after.Track.ShouldBe(before.Track);
		after.EntryMode.ShouldBe(before.EntryMode);
	});

	[Fact]
	public Task Offering_configuration_includes_deleted_legacy_applications() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var applicant = await CreateApplicantAsync("Historical Applicant");
		await WithUnitOfWorkAsync(async () =>
		{
			var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, "APP-LOCK-LEGACY", applicant.Id, null,
				offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value);
			await _applicationRepository.InsertAsync(application, true);
			await _applicationRepository.DeleteAsync(application, true);
		});
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var detail = await service.GetAsync(offering.Id);
		detail = await service.DeactivateAsync(offering.Id, detail.ConcurrencyStamp);
		detail.ApplicationCount.ShouldBe(0);
		detail.HasConfigurationUsage.ShouldBeTrue();
		detail.CanEditConfiguration.ShouldBeFalse();
	});

	[Fact]
	public Task Offering_configuration_usage_keeps_the_tenant_filter_enabled() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var otherTenant = Guid.NewGuid();
		using (_currentTenant.Change(otherTenant))
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(
				new AdmissionInquiry(Guid.NewGuid(), otherTenant, "OTHER-TENANT-LOCK", offering.AdmissionCycleId,
					offering.Id, "Other Tenant Inquiry", "9876543210", null, AdmissionInquirySource.Manual, null), true));
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var detail = await service.GetAsync(offering.Id);
		detail = await service.DeactivateAsync(offering.Id, detail.ConcurrencyStamp);
		detail.InquiryCount.ShouldBe(0);
		detail.HasConfigurationUsage.ShouldBeFalse();
		detail.CanEditConfiguration.ShouldBeTrue();
	});
}
