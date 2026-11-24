using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task ParentRegistration_reactivation_restores_the_same_url_and_saved_draft_without_another_email()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var applicationId = fixture.Application.Id;
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var config = GetRequiredService<IConfiguration>();
		var previousClientUrl = config["App:ClientUrl"];
		config["App:ClientUrl"] = "https://school.example.test";
		try
		{
			var sender = Substitute.For<IEmailSender>();
			var job = ParentDeliveryJob(sender);
			await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
			var context = await parent.GetAsync(fixture.Token);
			context = await parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "parent-save-before-expiry",
				Values = [Value("NOTE", "Saved before expiry"), Value("FATHER", "Father")] });
			var staffContext = await staff.GetAsync(applicationId);
			var lastSavedAt = staffContext.Request!.LastSavedAt;
			var copy = new AdmissionParentCopyLinkInput { RequestId = fixture.GrantId, ConcurrencyStamp = staffContext.ConcurrencyStamp, IdempotencyKey = "copy-original" };
			var originalLink = await staff.CopyLinkAsync(applicationId, copy);
			await ExpireParentRegistrationAsync(fixture.GrantId);
			staffContext = await staff.GetAsync(applicationId);
			staffContext.Request!.Status.ShouldBe("Expired");
			staffContext.CanReactivate.ShouldBeTrue();
			staffContext.CanCopyLink.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
			(await Should.ThrowAsync<BusinessException>(() => parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "expired-hand-in" })))
				.Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
			var reactivation = new AdmissionParentReactivateInput { RequestId = fixture.GrantId, ValidityDays = 45,
				ConcurrencyStamp = staffContext.ConcurrencyStamp, IdempotencyKey = "reactivate-original" };
			var activate = DateTime.UtcNow;
			staffContext = await staff.ReactivateAsync(applicationId, reactivation);
			staffContext.Request!.ExpiresAt.ShouldBeInRange(activate.AddDays(45), DateTime.UtcNow.AddDays(45));
			staffContext.Request.Id.ShouldBe(fixture.GrantId);
			staffContext.Request.Status.ShouldBe("InProgress");
			staffContext.Request.LastSavedAt.ShouldBe(lastSavedAt);
			staffContext.Request.DeliveryStatus.ShouldBe("Sent");
			staffContext.ConcurrencyStamp.ShouldNotBe(reactivation.ConcurrencyStamp);
			staffContext.CanReactivate.ShouldBeFalse();
			staffContext.CanCopyLink.ShouldBeTrue();
			(await staff.CopyLinkAsync(applicationId, copy)).ShouldBe(originalLink);
			var restored = await parent.GetAsync(fixture.Token);
			restored.ExpiresAt.ShouldBe(staffContext.Request.ExpiresAt);
			restored.Values.ShouldContain(value => value.FieldCode == "NOTE" && value.ValueJson == "\"Saved before expiry\"");
			restored.SnapshotToken.ShouldNotBe(context.SnapshotToken);
			(await staff.ReactivateAsync(applicationId, reactivation)).Request!.ExpiresAt.ShouldBe(restored.ExpiresAt);
			reactivation.ValidityDays = 14;
			(await Should.ThrowAsync<BusinessException>(() => staff.ReactivateAsync(applicationId, reactivation))).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
			var history = (await staff.GetEmailHistoryAsync(applicationId)).Single();
			history.RegistrationId.ShouldBe(fixture.GrantId);
			history.ExpiresAt.ShouldBe(restored.ExpiresAt);
			history.Attempts.Count.ShouldBe(1);
			await sender.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true);
			(await parent.HandInAsync(fixture.Token, new() { SnapshotToken = restored.SnapshotToken, IdempotencyKey = "restored-hand-in" })).Status.ShouldBe("ReadyForReview");
			var handedIn = (await staff.GetAsync(applicationId)).Request!;
			handedIn.HandedInAt.ShouldNotBeNull();
			handedIn.ExpiresAt.ShouldBe(restored.ExpiresAt);
			(await _applicationAppService.GetAsync(applicationId)).Status.ShouldBe(AdmissionApplicationStatus.Draft);
			(await _applicationAppService.SubmitAsync(applicationId)).ValidationErrors.ShouldBeEmpty();
			(await _applicationAppService.GetAsync(applicationId)).SubmittedAt.ShouldNotBeNull();
			(await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
		}
		finally
		{
			config["App:ClientUrl"] = previousClientUrl;
		}
	}

	[Fact]
	public async Task ParentRegistration_reactivation_uses_the_default_validity_and_stops_an_expired_pending_email()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await ExpireParentRegistrationAsync(fixture.GrantId);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var context = await staff.GetAsync(fixture.Application.Id);
		var input = new AdmissionParentReactivateInput { RequestId = fixture.GrantId, ConcurrencyStamp = context.ConcurrencyStamp,
			IdempotencyKey = "reactivate-pending" };
		var sender = Substitute.For<IEmailSender>();
		var job = ParentDeliveryJob(sender);
		var activate = DateTime.UtcNow;
		context = await staff.ReactivateAsync(fixture.Application.Id, input);
		context.Request!.Status.ShouldBe("Invited");
		context.Request.ExpiresAt.ShouldBeInRange(activate.AddDays(7), DateTime.UtcNow.AddDays(7));
		await job.ExecuteAsync(new() { TenantId = TestTenantId, RegistrationId = fixture.GrantId });
		sender.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task ParentRegistration_reactivation_restores_legacy_urls_without_recovering_the_cleared_copy_token()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await ExpireParentRegistrationAsync(fixture.GrantId, legacySent: true);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var context = await staff.GetAsync(fixture.Application.Id);
		context.CanReactivate.ShouldBeTrue();
		context = await staff.ReactivateAsync(fixture.Application.Id, new() { RequestId = fixture.GrantId,
			ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "reactivate-legacy-link" });
		context.CanCopyLink.ShouldBeFalse();
		context.Request!.DeliveryStatus.ShouldBe("Sent");
		var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		(await parent.GetAsync(fixture.Token)).CanEdit.ShouldBeTrue();
	}

	[Fact]
	public async Task ParentRegistration_refuses_to_reactivate_an_expired_link_for_a_changed_recipient_or_superseded_request()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var applicationId = fixture.Application.Id;
		await ExpireParentRegistrationAsync(fixture.GrantId);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
			applicant.SetProperty("AdmissionStudentField.Mother.Email", "new@example.test");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var context = await staff.GetAsync(applicationId);
		context.CanReactivate.ShouldBeFalse();
		var input = new AdmissionParentReactivateInput { RequestId = fixture.GrantId, ConcurrencyStamp = context.ConcurrencyStamp,
			IdempotencyKey = "reactivate-old-recipient" };
		(await Should.ThrowAsync<BusinessException>(() => staff.ReactivateAsync(applicationId, input))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
		var stamp = context.ConcurrencyStamp;
		var issue = DateTime.UtcNow;
		context = await staff.SendAsync(applicationId, new() { RecipientRelationship = "mother", ConcurrencyStamp = stamp,
			IdempotencyKey = "send-replacement", ValidityDays = 1 });
		context.Request!.ExpiresAt.ShouldBeInRange(issue.AddDays(1), DateTime.UtcNow.AddDays(1));
		context.Request.Id.ShouldNotBe(fixture.GrantId);
		input.ConcurrencyStamp = context.ConcurrencyStamp;
		(await Should.ThrowAsync<BusinessException>(() => staff.ReactivateAsync(applicationId, input))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
	}

	private Task ExpireParentRegistrationAsync(Guid id, bool legacySent = false) => WithUnitOfWorkAsync(async () =>
	{
		var repository = GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>();
		var row = await repository.GetAsync(id);
		typeof(AdmissionParentRegistration).GetProperty(nameof(AdmissionParentRegistration.ExpiresAt))!.SetValue(row, DateTime.UtcNow.AddMinutes(-1));
		if (legacySent)
		{
			row.SetDelivery("Sent");
			typeof(AdmissionParentRegistration).GetProperty(nameof(AdmissionParentRegistration.ProtectedToken))!.SetValue(row, string.Empty);
		}
		await repository.UpdateAsync(row, true);
	});
}
