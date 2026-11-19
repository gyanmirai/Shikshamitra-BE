using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task ParentRegistration_delivery_reuses_onboarding_sender_without_a_second_email_setup()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var sender = Substitute.For<IEmailSender>();
		var job = ParentDeliveryJob(sender);
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.Received(1).SendAsync("mother@example.test", Arg.Any<string>(), Arg.Is<string>(body =>
			body.Contains("/admission-registration#token=" + fixture.Token) &&
			body.Contains("Before you start") && body.Contains("CAPITAL LETTERS where requested") &&
			body.Contains("Complete Registration") && body.Contains("<a href=")), true);
		await WithUnitOfWorkAsync(async () =>
		{
			var request = await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetAsync(fixture.GrantId);
			request.DeliveryStatus.ShouldBe("Sent");
			var campaign = await GetRequiredService<IRepository<CommunicationCampaign, Guid>>().GetAsync(request.CampaignId!.Value);
			campaign.Body.ShouldNotContain(fixture.Token);
			campaign.LinkUrl.ShouldBeNullOrWhiteSpace();
			var attempts = await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().GetListAsync(row => row.CampaignId == campaign.Id);
			attempts.Single().Status.ShouldBe(CommunicationDeliveryStatus.Sent);
			attempts.Single().ProviderName.ShouldBe("SMTP");
			(await GetRequiredService<IRepository<ChannelProviderSetting, Guid>>().GetListAsync(row => row.Channel == CommunicationChannelType.Email)).ShouldBeEmpty();
		});
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true);
	}

	[Fact]
	public async Task ParentRegistration_transport_failure_can_be_retried_without_losing_delivery_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var sender = Substitute.For<IEmailSender>();
		var sends = 0;
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true)
			.Returns(_ => ++sends == 1 ? Task.FromException(new SmtpException(SmtpStatusCode.MailboxUnavailable, "private transport configuration")) : Task.CompletedTask);
		var job = ParentDeliveryJob(sender);
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var failed = await staff.GetAsync(fixture.Application.Id);
		failed.Request!.DeliveryStatus.ShouldBe("Failed");
		failed.Request.DeliveryError.ShouldNotBeNull().ShouldContain("email settings");
		failed.Request.DeliveryError.ShouldNotContain("private transport");
		failed.CanRetryDelivery.ShouldBeTrue();
		await staff.RetryDeliveryAsync(fixture.Application.Id, new() { ConcurrencyStamp = failed.ConcurrencyStamp, IdempotencyKey = "retry-known-smtp-failure" });
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		(await staff.GetAsync(fixture.Application.Id)).Request!.DeliveryStatus.ShouldBe("Sent");
		await WithUnitOfWorkAsync(async () =>
		{
			var campaigns = await GetRequiredService<IRepository<CommunicationCampaign, Guid>>().GetListAsync(row => row.SourceEntityId == fixture.GrantId);
			campaigns.Count.ShouldBe(2);
			var ids = campaigns.Select(row => row.Id).ToArray();
			var attempts = await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().GetListAsync(row => Enumerable.Contains(ids, row.CampaignId));
			attempts.Count.ShouldBe(2);
			attempts.Count(row => row.Status == CommunicationDeliveryStatus.Failed).ShouldBe(1);
			attempts.Count(row => row.Status == CommunicationDeliveryStatus.Sent).ShouldBe(1);
		});
		await sender.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParentRegistration_disabled_email_provider_is_respected_instead_of_using_school_sender(bool removed)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ChannelProviderSetting, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
		{ Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = false, Status = CommunicationProviderStatus.Disabled, IsDeleted = removed }, true));
		var sender = Substitute.For<IEmailSender>();
		await ParentDeliveryJob(sender).ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
		var state = await GetRequiredService<IAdmissionParentRegistrationAppService>().GetAsync(fixture.Application.Id);
		state.Request!.DeliveryStatus.ShouldBe("Failed");
		state.Request.DeliveryError.ShouldNotBeNull().ShouldContain("Email provider");
		state.EmailDeliveryBlockedReason.ShouldNotBeNull().ShouldContain("Email provider");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParentRegistration_school_sender_retains_explicit_consent_and_approval_controls(bool approvalRequired)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			if (approvalRequired)
				await GetRequiredService<IRepository<CommunicationApprovalRule, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
				{ RuleName = "Parent email approval", Channel = CommunicationChannelType.Email, RequiresApproval = true, IsEnabled = true }, true);
			else
				await GetRequiredService<IRepository<ConsentRecord, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
				{ RecipientType = "AdmissionInquiry", RecipientId = fixture.Application.ApplicantId, Channel = CommunicationChannelType.Email, Destination = "mother@example.test", Status = CommunicationConsentStatus.Revoked }, true);
		});
		var sender = Substitute.For<IEmailSender>();
		await ParentDeliveryJob(sender).ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
		var state = await GetRequiredService<IAdmissionParentRegistrationAppService>().GetAsync(fixture.Application.Id);
		state.Request!.DeliveryStatus.ShouldBe(approvalRequired ? "PendingApproval" : "Failed");
		state.Request.DeliveryError.ShouldNotBeNull().ShouldContain(approvalRequired ? "approval" : "consent");
		if (!approvalRequired)
			await WithUnitOfWorkAsync(async () =>
			{
				var request = await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetAsync(fixture.GrantId);
				var attempts = await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().GetListAsync(row => row.CampaignId == request.CampaignId);
				attempts.Single().ErrorCode.ShouldBe("CONSENT_WITHDRAWN");
			});
	}

	private AdmissionParentRegistrationDeliveryJob ParentDeliveryJob(IEmailSender sender)
	{
		var provider = GetRequiredService<IServiceProvider>();
		var communication = ActivatorUtilities.CreateInstance<CommunicationAppService>(provider,
			(IEnumerable<ICommunicationChannelGateway>)[new SmtpCommunicationChannelGateway(sender)]);
		communication.LazyServiceProvider = GetRequiredService<CommunicationAppService>().LazyServiceProvider;
		var configuration = new ConfigurationBuilder().AddConfiguration(GetRequiredService<IConfiguration>())
			.AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example.test" }).Build();
		return ActivatorUtilities.CreateInstance<AdmissionParentRegistrationDeliveryJob>(provider, communication, configuration);
	}

	[Fact]
	public async Task ParentRegistration_configured_email_provider_keeps_its_opt_in_requirement()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ChannelProviderSetting, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
		{ Channel = CommunicationChannelType.Email, ProviderName = "SMTP", IsEnabled = true, Status = CommunicationProviderStatus.Enabled, ConsentRequired = true }, true));
		var sender = Substitute.For<IEmailSender>();
		await ParentDeliveryJob(sender).ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
		var state = await GetRequiredService<IAdmissionParentRegistrationAppService>().GetAsync(fixture.Application.Id);
		state.Request!.DeliveryStatus.ShouldBe("Failed");
		state.Request.DeliveryError.ShouldNotBeNull().ShouldContain("opt-in consent");
	}

	[Fact]
	public async Task ParentRegistration_unconfirmed_transport_acceptance_never_enables_retry()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true)
			.Returns(Task.FromException(new TimeoutException("private transport details")));
		var job = ParentDeliveryJob(sender);
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var state = await staff.GetAsync(fixture.Application.Id);
		state.Request!.DeliveryStatus.ShouldBe("Unknown");
		state.Request.DeliveryError.ShouldNotBeNull().ShouldNotContain("private transport");
		state.CanRetryDelivery.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => staff.RetryDeliveryAsync(fixture.Application.Id,
			new() { ConcurrencyStamp = state.ConcurrencyStamp, IdempotencyKey = "refuse-ambiguous-retry" }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		await sender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true);
	}
}
