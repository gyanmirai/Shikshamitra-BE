using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task ParentRegistration_initial_draft_is_shared_handin_is_not_submit_and_retries_are_exact()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(staffRequired: true);
		var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var context = await parent.GetAsync(fixture.Token);
		context.EditableFieldCodes.ShouldContain("FATHER"); context.EditableFieldCodes.ShouldNotContain("MOTHER_EMAIL");
		context.Definition.Sections.SelectMany(section => section.Fields).ShouldNotContain(field => field.Code == "STAFF_ID");
		(await Should.ThrowAsync<BusinessException>(() => parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "early" }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Incomplete);
		var save = new AdmissionParentDraftInput { SnapshotToken = context.SnapshotToken, IdempotencyKey = "save-one", Values = [Value("NOTE", "Parent answer"), Value("FATHER", "Father Captured")] };
		context = await parent.SaveAsync(fixture.Token, save);
		(await parent.SaveAsync(fixture.Token, save)).Values.ShouldContain(value => value.FieldCode == "NOTE" && value.ValueJson == "\"Parent answer\"");
		var staffForm = await _applicationAppService.GetFormAsync(fixture.Application.Id);
		staffForm.Values.ShouldContain(value => value.FieldCode == "FATHER" && value.ValueJson == "\"Father Captured\"");
		staffForm.ConcurrencyStamp.ShouldBe((await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp);
		var handIn = new AdmissionParentPublicCommand { SnapshotToken = context.SnapshotToken, IdempotencyKey = "hand-in" };
		context = await parent.HandInAsync(fixture.Token, handIn);
		context.Status.ShouldBe("ReadyForReview"); context.CanEdit.ShouldBeFalse();
		(await parent.HandInAsync(fixture.Token, handIn)).Status.ShouldBe("ReadyForReview");
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft); application.SubmittedAt.ShouldBeNull();
		(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(fixture.Application.CurrentStageInstanceId);
		await WithUnitOfWorkAsync(async () =>
		{
			var history = await GetRequiredService<IRepository<AdmissionFormChangeHistory, Guid>>().GetListAsync(row => row.ApplicationId == application.Id && row.ChangeType == "ParentDraftSaved");
			history.Count.ShouldBe(1); history.Single().ChangedBy.ShouldBeNull(); history.Single().ChangedFieldsJson!.ShouldContain("ParentRegistrationId");
		});
	}
	[Fact]
	public async Task ParentRegistration_stale_snapshots_hidden_fields_contact_changes_and_revocation_fail_closed()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var context = await parent.GetAsync(fixture.Token);
		foreach (var code in new[] { "HIDDEN", "MOTHER_EMAIL", "STAFF_ID", "SIBLINGS" })
			await Should.ThrowAsync<BusinessException>(() => parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "attack-" + code, Values = [Value(code, "tampered")] }));
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, Values = [Value("NOTE", "Staff answer")] });
		(await Should.ThrowAsync<BusinessException>(() => parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "stale", Values = [Value("NOTE", "Stale parent answer")] }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
		(await _applicationAppService.GetFormAsync(fixture.Application.Id)).Values.ShouldContain(value => value.FieldCode == "NOTE" && value.ValueJson == "\"Staff answer\"");
		await WithUnitOfWorkAsync(async () => { var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId); applicant.SetProperty("AdmissionStudentField.Mother.Email", "changed@example.test"); await _applicantRepository.UpdateAsync(applicant, true); });
		(await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await staff.RevokeAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, IdempotencyKey = "revoke" });
		await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token));
		await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(new string('A', 64)));
	}
	[Fact]
	public async Task ParentRegistration_required_upload_is_private_idempotent_and_truthfully_attributed()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(withDocuments: true); var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var context = await parent.GetAsync(fixture.Token);
		context = await parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "answers", Values = [Value("NOTE", "Complete"), Value("FATHER", "Father")] });
		(await Should.ThrowAsync<BusinessException>(() => parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "missing-upload" }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Incomplete);
		var document = context.Documents.Single(); var snapshot = context.SnapshotToken;
		using (var bad = new RemoteStreamContent(new MemoryStream("not a pdf"u8.ToArray()), "proof.pdf", "application/pdf"))
			await Should.ThrowAsync<BusinessException>(() => parent.UploadAsync(fixture.Token, document.StageInstanceId, document.Code, new() { File = bad, SnapshotToken = snapshot, IdempotencyKey = "bad-file" }));
		for (var retry = 0; retry < 2; retry++)
		{
			using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "proof.pdf", "application/pdf", HandoffPdf.Length);
			context = await parent.UploadAsync(fixture.Token, document.StageInstanceId, document.Code, new() { File = file, SnapshotToken = snapshot, IdempotencyKey = "upload" });
		}
		var evidence = context.Documents.Single().Evidence.Single();
		(await parent.DownloadAsync(fixture.Token, evidence.Id)).Content.ShouldBe(HandoffPdf);
		await Should.ThrowAsync<BusinessException>(() => parent.DownloadAsync(fixture.Token, Guid.NewGuid()));
		await WithUnitOfWorkAsync(async () => { var row = await GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>().GetAsync(evidence.Id); row.SubmittedBy.ShouldBeNull(); row.FileSubmittedBy.ShouldBeNull(); row.ParentRegistrationId.ShouldBe(fixture.GrantId); row.ParentContactJson!.ShouldContain("Mother Test"); });
		(await parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "complete" })).Status.ShouldBe("ReadyForReview");
		(await _applicationAppService.SubmitAsync(fixture.Application.Id)).ValidationErrors.ShouldBeEmpty();
		var staffDocuments = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var checklist = await staffDocuments.GetAsync(fixture.Application.Id, document.StageInstanceId);
		checklist.Requirements.Single().Evidence.Single().ParentRegistrationId.ShouldBe(fixture.GrantId);
		checklist.Requirements.Single().CanReview.ShouldBeTrue();
		checklist = await staffDocuments.ReviewAsync(evidence.Id, new() { ConcurrencyStamp = checklist.ConcurrencyStamp, Decision = "accepted" });
		checklist.Requirements.Single().Status.ShouldBe("accepted");
		checklist.Requirements.Single().Evidence.Single().ParentContactJson!.ShouldContain("Mother Test");
	}
	[Fact]
	public async Task ParentRegistration_submitted_corrections_remain_proposals_through_repeated_saves_until_staff_applies()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, Values = [Value("NOTE", "Original"), Value("FATHER", "Father")] });
		(await _applicationAppService.SubmitAsync(fixture.Application.Id)).ValidationErrors.ShouldBeEmpty();
		var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token));
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var state = await staff.RequestCorrectionsAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!,
			IdempotencyKey = "correction-request", RecipientRelationship = "mother", FieldCodes = ["NOTE"], Notes = "Please clarify the note." });
		var token = await ParentTokenAsync(state.Request!.Id); var context = await parent.GetAsync(token);
		context.ProposalOnly.ShouldBeTrue(); context.EditableFieldCodes.ShouldBe(["NOTE"]);
		context = await parent.SaveAsync(token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "proposal-one", Values = [Value("NOTE", "First proposal")] });
		context = await parent.SaveAsync(token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "proposal-two", Values = [Value("NOTE", "Final proposal")] });
		(await _applicationAppService.GetFormAsync(fixture.Application.Id)).Values.ShouldContain(value => value.FieldCode == "NOTE" && value.ValueJson == "\"Original\"");
		await parent.HandInAsync(token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "proposed-hand-in" });
		await WithUnitOfWorkAsync(async () => { var row = await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetAsync(state.Request.Id); typeof(AdmissionParentRegistration).GetProperty(nameof(AdmissionParentRegistration.ExpiresAt))!.SetValue(row, DateTime.UtcNow.AddDays(-1)); await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().UpdateAsync(row, true); });
		await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(token));
		var apply = new AdmissionParentApplyInput { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, IdempotencyKey = "apply-parent-proposal", Reason = "Verified the parent's correction." };
		(await staff.ApplyAsync(fixture.Application.Id, apply)).Request!.Status.ShouldBe("Applied");
		(await staff.ApplyAsync(fixture.Application.Id, apply)).Request!.Status.ShouldBe("Applied");
		(await _applicationAppService.GetFormAsync(fixture.Application.Id)).Values.ShouldContain(value => value.FieldCode == "NOTE" && value.ValueJson == "\"Final proposal\"");
		(await _applicationAppService.GetAsync(fixture.Application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Submitted);
	}
	private sealed record ParentFixture(AdmissionApplicationDto Application, Guid GrantId, string Token);
	[Fact]
	public async Task ParentRegistration_newly_applicable_documents_wait_for_a_later_staff_request()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(withDocuments: true, documentBranch: true); var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var context = await parent.GetAsync(fixture.Token); context.Documents.ShouldBeEmpty();
		context = await parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "answers", Values = [Value("NOTE", "Complete"), Value("FATHER", "Father")] });
		context.Documents.ShouldBeEmpty();
		(await GetRequiredService<IAdmissionParentRegistrationAppService>().GetAsync(fixture.Application.Id)).AvailableDocuments.Count.ShouldBe(1);
		(await parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "handin" })).Status.ShouldBe("ReadyForReview");
	}
	[Fact]
	public async Task ParentRegistration_reopened_drafts_retain_the_existing_staff_save_workflow()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, Values = [Value("NOTE", "Complete"), Value("FATHER", "Father")] });
		(await _applicationAppService.SubmitAsync(fixture.Application.Id)).ValidationErrors.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () => { var row = await _applicationRepository.GetAsync(fixture.Application.Id); row.Reopen(); await _applicationRepository.UpdateAsync(row, true);
			var instances = GetRequiredService<IRepository<AdmissionFormInstance, Guid>>(); var instance = await instances.GetAsync(item => item.ApplicationId == row.Id); instance.Reopen(); await instances.UpdateAsync(instance, true); });
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>(); var context = await staff.GetAsync(fixture.Application.Id);
		context.CanRequestCorrections.ShouldBeFalse(); context.BlockedReason!.ShouldContain("reopened");
		(await Should.ThrowAsync<BusinessException>(() => staff.RequestCorrectionsAsync(fixture.Application.Id, new() { ConcurrencyStamp = context.ConcurrencyStamp!, IdempotencyKey = "reopened",
			RecipientRelationship = "mother", Notes = "Correction", FieldCodes = ["NOTE"] }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = context.ConcurrencyStamp!, Values = [Value("NOTE", "Staff can still save")] });
	}
	[Fact]
	public async Task ParentRegistration_corrections_cannot_open_a_future_registration_stage()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, _, _, _) = await CreateCurrentJourneyApplicationAsync(AdmissionWorkflowStageType.Form, formIsFuture: true);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var context = await staff.GetAsync(application.Id); context.CanSend.ShouldBeFalse(); context.CanRequestCorrections.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => staff.RequestCorrectionsAsync(application.Id, new() { ConcurrencyStamp = application.ConcurrencyStamp!,
			IdempotencyKey = "future-stage", RecipientRelationship = "mother", Notes = "Registration must remain unavailable.", FieldCodes = ["NAME"] }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
		await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetListAsync(row => row.ApplicationId == application.Id)).ShouldBeEmpty());
	}
	[Fact]
	public async Task ParentRegistration_current_sender_revocation_rotation_and_staff_tenant_boundaries_are_enforced()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await staff.SendAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, IdempotencyKey = "rotate", RecipientRelationship = "mother" });
		await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(fixture.Token));
		var replacement = (await staff.GetAsync(fixture.Application.Id)).Request!; var token = await ParentTokenAsync(replacement.Id);
		await parent.GetAsync(token);
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => staff.GetAsync(fixture.Application.Id));
		await WithUnitOfWorkAsync(async () => { var users = GetRequiredService<IRepository<IdentityUser, Guid>>(); var sender = await users.GetAsync(TestUserId); sender.SetIsActive(false); await users.UpdateAsync(sender, true); });
		(await Should.ThrowAsync<BusinessException>(() => parent.GetAsync(token))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
	}
	[Fact]
	public async Task ParentRegistration_email_history_is_scoped_to_its_grants_and_excludes_private_link_data()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		Guid campaignId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var campaign = new CommunicationCampaign(campaignId, TestTenantId)
			{
				SourceEntityType = "AdmissionParentRegistration", SourceEntityId = fixture.GrantId,
				PublishedAt = DateTimeOffset.UtcNow
			};
			await GetRequiredService<IRepository<CommunicationCampaign, Guid>>().InsertAsync(campaign, true);
			await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
			{
				CampaignId = campaignId, Destination = "mother@example.test", Status = CommunicationDeliveryStatus.Sent,
				SentAt = DateTimeOffset.UtcNow
			}, true);
			await GetRequiredService<IRepository<DeliveryAttempt, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId)
			{
				CampaignId = campaignId, Destination = "mother@example.test", Status = CommunicationDeliveryStatus.Failed,
				ErrorCode = "SMTP_SEND_FAILED", ErrorMessage = "Raw provider diagnostics must stay private."
			}, true);
		});

		var history = await GetRequiredService<IAdmissionParentRegistrationAppService>().GetEmailHistoryAsync(fixture.Application.Id);
		var request = history.ShouldHaveSingleItem();
		request.RecipientEmail.ShouldBe("mother@example.test");
		request.Attempts.Count.ShouldBe(2);
		request.Attempts.ShouldContain(row => row.Status == "Sent");
		request.Attempts.ShouldContain(row => row.Status == "Failed" && row.SafeMessage!.Contains("sender rejected"));
		JsonSerializer.Serialize(history).ShouldNotContain("ProtectedToken");
		JsonSerializer.Serialize(history).ShouldNotContain(fixture.Token);
		JsonSerializer.Serialize(history).ShouldNotContain("Raw provider diagnostics");
	}

	[Fact]
	public async Task ParentRegistration_campaign_audience_is_immutable_and_skipped_retry_retains_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var communication = GetRequiredService<CommunicationAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var grants = GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>(); var grant = await grants.GetAsync(fixture.GrantId);
			var application = await _applicationRepository.GetAsync(fixture.Application.Id); var cycle = await _cycleRepository.GetAsync(application.AdmissionCycleId);
			var recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!;
			var campaigns = GetRequiredService<IRepository<CommunicationCampaign, Guid>>(); var attempts = GetRequiredService<IRepository<DeliveryAttempt, Guid>>();
			var first = await communication.PrepareParentRegistrationAsync(grant, application, cycle, recipient);
			(await Should.ThrowAsync<BusinessException>(() => communication.UpdateCampaignAsync(first, new() { Title = "Tampered", Body = "Tampered" }))).Code.ShouldBe("Communication:IntegrationCampaignReadOnly");
			var campaign = await campaigns.GetAsync(first); campaign.PublishedAt = DateTimeOffset.UtcNow; await campaigns.UpdateAsync(campaign, true);
			await attempts.InsertAsync(new(Guid.NewGuid(), TestTenantId) { CampaignId = first, Status = CommunicationDeliveryStatus.Skipped, Destination = recipient.Email!, ErrorCode = "CONSENT_REQUIRED" }, true);
			grant.SetDelivery("Failed", campaignId: first); await grants.UpdateAsync(grant, true);
			var retry = await communication.PrepareParentRegistrationAsync(grant, application, cycle, recipient);
			retry.ShouldNotBe(first); (await attempts.GetListAsync(row => row.CampaignId == first)).Count.ShouldBe(1);
			var fresh = await campaigns.GetAsync(retry); fresh.PublishedAt.ShouldBeNull(); fresh.SourceEntityType.ShouldBe("AdmissionParentRegistration"); fresh.SourceEntityId.ShouldBe(grant.Id);
			fresh.PublishedAt = DateTimeOffset.UtcNow; await campaigns.UpdateAsync(fresh, true);
			await attempts.InsertAsync(new(Guid.NewGuid(), TestTenantId) { CampaignId = retry, Status = CommunicationDeliveryStatus.Sent, Destination = recipient.Email! }, true);
			grant.SetDelivery("Unknown", campaignId: retry); await grants.UpdateAsync(grant, true);
			(await communication.PrepareParentRegistrationAsync(grant, application, cycle, recipient)).ShouldBe(retry);
		});
	}
	[Fact]
	public async Task ParentRegistration_cannot_change_a_hidden_cell_in_another_visible_repeat_row()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var formDto = await _applicationAppService.GetFormAsync(fixture.Application.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var form = await _formRepository.GetAsync(formDto.FormDefinitionId);
			var rows = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "ROWS", "Rows", 50, null), true);
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), form.Id, rows.Id, "CTRL", "Control", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 0, null, null, null, null, null), true);
			await _formFieldRepository.InsertAsync(new(Guid.NewGuid(), form.Id, rows.Id, "DETAIL", "Detail", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 1, null, null, null, null, "{\"operator\":\"Equals\",\"field\":\"CTRL\",\"value\":\"yes\"}"), true);
			AdmissionFormMetadataStore.WriteExperience(form, new() { Sections = [new() { Code = "ROWS", IsRepeatable = true, MinEntries = 0, MaxEntries = 3 }] });
			await _formRepository.UpdateAsync(form, true);
		});
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!,
			Values = [new() { FieldCode = "CTRL", ValueJson = "[\"yes\",\"no\"]" }, new() { FieldCode = "DETAIL", ValueJson = "[\"shown\",\"hidden\"]" }] });
		var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>(); var context = await parent.GetAsync(fixture.Token);
		(await Should.ThrowAsync<BusinessException>(() => parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "hidden-row-attack",
			Values = [new() { FieldCode = "DETAIL", ValueJson = "[\"shown\",\"tampered hidden\"]" }] }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
	}
	[Fact]
	public async Task ParentRegistration_conditional_correction_includes_safe_controller()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		var state = await GetRequiredService<IAdmissionParentRegistrationAppService>().RequestCorrectionsAsync(fixture.Application.Id,
			new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!, IdempotencyKey = "hidden-correction",
				RecipientRelationship = "mother", Notes = "Please complete the conditional answer.", FieldCodes = ["HIDDEN"] });
		var context = await GetRequiredService<IAdmissionParentRegistrationPublicAppService>().GetAsync(await ParentTokenAsync(state.Request!.Id));
		context.EditableFieldCodes.ShouldContain("NOTE"); context.EditableFieldCodes.ShouldContain("HIDDEN"); context.EditableFieldCodes.ShouldNotContain("FATHER");
	}
	[Fact]
	public async Task ParentRegistration_explicit_document_correction_requires_a_fresh_revision()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(withDocuments: true, optionalDocument: true); var parent = GetRequiredService<IAdmissionParentRegistrationPublicAppService>();
		var context = await parent.GetAsync(fixture.Token);
		context = await parent.SaveAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "answers", Values = [Value("NOTE", "Complete"), Value("FATHER", "Father")] });
		var document = context.Documents.Single();
		using (var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "proof.pdf", "application/pdf", HandoffPdf.Length))
			context = await parent.UploadAsync(fixture.Token, document.StageInstanceId, document.Code, new() { File = file, SnapshotToken = context.SnapshotToken, IdempotencyKey = "old-upload" });
		await parent.HandInAsync(fixture.Token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "old-handin" });
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var state = await staff.RequestCorrectionsAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.Application.Id)).ConcurrencyStamp!,
			IdempotencyKey = "replacement", RecipientRelationship = "mother", Notes = "Replace the document.", Documents = [new() { StageInstanceId = document.StageInstanceId, RequirementCode = document.Code }] });
		var token = await ParentTokenAsync(state.Request!.Id); context = await parent.GetAsync(token);
		context.Documents.Single().Status.ShouldBe("missing");
		(await Should.ThrowAsync<BusinessException>(() => parent.HandInAsync(token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "unchanged" }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Incomplete);
		using (var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "replacement.pdf", "application/pdf", HandoffPdf.Length))
			context = await parent.UploadAsync(token, document.StageInstanceId, document.Code, new() { File = file, SnapshotToken = context.SnapshotToken, IdempotencyKey = "new-upload" });
		context.Documents.Single().Evidence.Count.ShouldBe(2);
		(await parent.HandInAsync(token, new() { SnapshotToken = context.SnapshotToken, IdempotencyKey = "replacement-complete" })).Status.ShouldBe("ReadyForReview");
		(await staff.GetAsync(fixture.Application.Id)).CanApply.ShouldBeFalse();
	}
	private async Task<ParentFixture> ParentFixtureAsync(bool staffRequired = false, bool withDocuments = false, bool optionalDocument = false, bool documentBranch = false)
	{
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: TestUserId), true);
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		if (withDocuments)
		{
			var draft = await _workflowAppService.CreateNewVersionAsync(workflow.Id); var input = ToUpdateInput(draft);
			var entry = input.Stages.Single(stage => stage.IsEntryStage); var terminal = input.Stages.Single(stage => stage.IsTerminalStage); var id = Guid.NewGuid(); terminal.DisplayOrder = 2;
			input.Stages.Add(new() { Id = id, Code = "DOCUMENTS", Name = "Documents", StageType = AdmissionWorkflowStageType.DocumentVerification, DisplayOrder = 1, IsRequired = true,
				ConfigurationJson = JsonSerializer.Serialize(new { documentVerification = HandoffPolicy(new AdmissionDocumentRequirement { Code = "PROOF", Name = "Proof",
					Requirement = optionalDocument ? "optional" : "required" }) }) });
			input.Transitions = [new() { FromStageId = entry.Id!.Value, ToStageId = id, IsDefault = true }, new() { FromStageId = id, ToStageId = terminal.Id!.Value, IsDefault = true }];
			if (documentBranch)
				input.Transitions = [new() { FromStageId = entry.Id!.Value, ToStageId = id, Priority = 1, ConditionRuleJson = "{\"operator\":\"Equals\",\"field\":\"application.NOTE\",\"value\":\"Complete\"}" },
					new() { FromStageId = entry.Id!.Value, ToStageId = terminal.Id!.Value, IsDefault = true, Priority = 2 }, new() { FromStageId = id, ToStageId = terminal.Id!.Value, IsDefault = true }];
			draft = await _workflowAppService.UpdateAsync(draft.Id, input); workflow = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		}
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), formId, "DETAILS", "Details", 0, null);
			await _formSectionRepository.InsertAsync(section, true);
			foreach (var field in new[] {
				new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, "NOTE", "Note", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, true, false, 0, null, null, null, null, null),
				new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, "FATHER", "Father", AdmissionFormFieldType.Text, AdmissionFormBindingType.System, "Father.Name", true, false, 1, null, null, null, null, null),
				new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, "MOTHER_EMAIL", "Mother email", AdmissionFormFieldType.Email, AdmissionFormBindingType.System, "Mother.Email", false, false, 2, null, null, null, null, null),
				new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, "STAFF_ID", "Staff identity capture", AdmissionFormFieldType.Text, AdmissionFormBindingType.System, "Identity.ApaarId", staffRequired, false, 3, null, null, null, null, null),
				new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, "HIDDEN", "Conditional answer", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 4, null, null, null, null, "{\"operator\":\"Equals\",\"field\":\"NOTE\",\"value\":\"show\"}") })
				await _formFieldRepository.InsertAsync(field, true);
		});
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId); var applicant = await CreateApplicantAsync("ParentChild");
		await WithUnitOfWorkAsync(async () => { var row = await _applicantRepository.GetAsync(applicant.Id); row.SetProperty("AdmissionStudentField.Mother.Name", "Mother Test"); row.SetProperty("AdmissionStudentField.Mother.Email", "mother@example.test"); row.SetProperty(StudentFamilyContactSelection.SelectionKey, "mother"); await _applicantRepository.UpdateAsync(row, true); });
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		var staff = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var result = await staff.SendAsync(application.Id, new() { ConcurrencyStamp = application.ConcurrencyStamp!, IdempotencyKey = "initial", RecipientRelationship = "mother" });
		return new(application, result.Request!.Id, await ParentTokenAsync(result.Request.Id));
	}
	private Task<string> ParentTokenAsync(Guid id) => WithUnitOfWorkAsync(async () => GetRequiredService<IDataProtectionProvider>().CreateProtector("AdmissionParentRegistration.v1")
		.Unprotect((await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetAsync(id)).ProtectedToken));
	private static AdmissionFormValueDto Value(string code, string value) => new() { FieldCode = code, ValueJson = JsonSerializer.Serialize(value) };
}
