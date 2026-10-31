using System;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_qualification_email_reuses_one_communication_campaign_and_delivery_attempt() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "INQ-EMAIL-QA",
				offering.AdmissionCycleId, offering.Id, "Email QA", "9876543222", "qa@example.invalid",
				AdmissionInquirySource.WalkIn, null);
			var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var providers = GetRequiredService<IRepository<ChannelProviderSetting, Guid>>();
			await WithUnitOfWorkAsync(async () =>
			{
				inquiry.SetStatus(AdmissionInquiryStatus.Interested);
				await inquiries.InsertAsync(inquiry, true);
				await providers.InsertAsync(new ChannelProviderSetting(Guid.NewGuid(), TestTenantId)
				{
					Channel = CommunicationChannelType.Email, ProviderName = "QAQueueOnly",
					IsDefault = true, IsEnabled = true, Status = CommunicationProviderStatus.Enabled,
					ConsentRequired = false, TemplateApprovalRequired = false
				}, true);
			});
			var dispatcher = GetRequiredService<AdmissionInquiryQualificationEmailDispatcher>();
			await dispatcher.DispatchAsync(inquiry.Id);
			await dispatcher.DispatchAsync(inquiry.Id);
			var campaigns = GetRequiredService<IRepository<CommunicationCampaign, Guid>>();
			var campaign = await WithUnitOfWorkAsync(async () =>
				(await campaigns.GetListAsync(item => item.SourceEntityType == "AdmissionInquiryQualification" && item.SourceEntityId == inquiry.Id)).ShouldHaveSingleItem());
			campaign.Status.ShouldBe(CommunicationCampaignStatus.Queued);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DeliveryAttempt, Guid>>().CountAsync(item => item.CampaignId == campaign.Id))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() => inquiries.GetAsync(inquiry.Id))).GetProperty<string>("QualificationEmailStatus").ShouldBe("Queued");
		});

	[Fact]
	public Task Inquiry_qualification_respects_withdrawn_consent_even_if_the_provider_does_not_require_opt_in() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "INQ-OPT-OUT-QA",
				offering.AdmissionCycleId, offering.Id, "Consent QA", "9876543224", "consent@example.invalid",
				AdmissionInquirySource.WalkIn, null);
			inquiry.SetStatus(AdmissionInquiryStatus.Interested);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
				await GetRequiredService<IRepository<ChannelProviderSetting, Guid>>().InsertAsync(new ChannelProviderSetting(Guid.NewGuid(), TestTenantId)
				{
					Channel = CommunicationChannelType.Email, ProviderName = "QAQueueOnly",
					IsDefault = true, IsEnabled = true, Status = CommunicationProviderStatus.Enabled,
					ConsentRequired = false, TemplateApprovalRequired = false
				}, true);
				await GetRequiredService<IRepository<ConsentRecord, Guid>>().InsertAsync(new ConsentRecord(Guid.NewGuid(), TestTenantId)
				{
					RecipientType = "AdmissionInquiry", RecipientId = inquiry.Id,
					Channel = CommunicationChannelType.Email, Destination = inquiry.Email!,
					Status = CommunicationConsentStatus.OptedOut
				}, true);
			});
			await GetRequiredService<AdmissionInquiryQualificationEmailDispatcher>().DispatchAsync(inquiry.Id);
			var campaign = await WithUnitOfWorkAsync(async () =>
				(await GetRequiredService<IRepository<CommunicationCampaign, Guid>>().GetListAsync(item =>
					item.SourceEntityType == "AdmissionInquiryQualification" && item.SourceEntityId == inquiry.Id)).ShouldHaveSingleItem());
			var attempt = await WithUnitOfWorkAsync(async () =>
				(await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().GetListAsync(item => item.CampaignId == campaign.Id)).ShouldHaveSingleItem());
			attempt.Status.ShouldBe(CommunicationDeliveryStatus.Skipped);
			attempt.ErrorCode.ShouldBe("CONSENT_WITHDRAWN");
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id))).Status.ShouldBe(AdmissionInquiryStatus.Interested);
		});

	[Fact]
	public Task Inquiry_qualification_without_email_is_skipped_without_reversing_interest() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "INQ-NO-EMAIL",
				offering.AdmissionCycleId, offering.Id, "No email QA", "9876543223", null, AdmissionInquirySource.WalkIn, null);
			inquiry.SetStatus(AdmissionInquiryStatus.Interested);
			await WithUnitOfWorkAsync(() => inquiries.InsertAsync(inquiry, true));
			await GetRequiredService<AdmissionInquiryQualificationEmailDispatcher>().DispatchAsync(inquiry.Id);
			var saved = await WithUnitOfWorkAsync(() => inquiries.GetAsync(inquiry.Id));
			saved.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			saved.GetProperty<string>("QualificationEmailStatus").ShouldBe("Skipped");
		});

	[Fact]
	public Task Inquiry_capture_denies_full_seats_when_the_workflow_has_no_waitlist() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var pool = new AdmissionSeatPool(Guid.NewGuid(), TestTenantId, offering.Id, 1);
			pool.Reserve(false);
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionSeatPool, Guid>>().InsertAsync(pool, true));
			var availability = GetRequiredService<AdmissionInquiryAvailabilityService>();
			var result = await WithUnitOfWorkAsync(() => availability.ReadAsync(offering));
			result.CanCreateInquiry.ShouldBeFalse();
			result.WaitlistAllowed.ShouldBeFalse();
			result.Reason.ShouldNotBeNull().ShouldContain("filled");
			await WithUnitOfWorkAsync(async () => await Should.ThrowAsync<Volo.Abp.BusinessException>(() => availability.EnsureAvailableAsync(offering)));
		});
}
