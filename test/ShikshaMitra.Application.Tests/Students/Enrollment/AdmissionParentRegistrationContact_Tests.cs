using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task ParentRegistration_email_change_updates_saved_contact_and_draft_answer_revokes_link_and_replays_without_sending()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var jobs = Substitute.For<IBackgroundJobManager>(); var service = ParentContactService(jobs);
		var context = await GetRequiredService<IAdmissionParentRegistrationAppService>().GetAsync(fixture.Application.Id);
		context.Recipients.Single(item => item.Relationship == "mother").CanChangeEmail.ShouldBeTrue();
		var input = new AdmissionParentEmailInput { RecipientRelationship = "mother", Email = "NEW@example.test", ExpectedEmail = "mother@example.test",
			ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "change-email" };
		context = await service.ChangeEmailAsync(fixture.Application.Id, input);
		context.Recipients.Single(item => item.Relationship == "mother").Email.ShouldBe("new@example.test");
		context.ConcurrencyStamp.ShouldNotBe(input.ConcurrencyStamp);
		context.Request!.Id.ShouldBe(fixture.GrantId); context.Request.Status.ShouldBe("Revoked");
		(await _applicationAppService.GetFormAsync(fixture.Application.Id)).Values.ShouldContain(item => item.FieldCode == "MOTHER_EMAIL" && item.ValueJson == "\"new@example.test\"");
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionParentRegistrationPublicAppService>().GetAsync(fixture.Token))).Code.ShouldBe(AdmissionParentRegistrationErrors.Unavailable);
		await WithUnitOfWorkAsync(async () =>
		{
			var grants = await GetRequiredService<IRepository<AdmissionParentRegistration, Guid>>().GetListAsync(item => item.ApplicationId == fixture.Application.Id);
			grants.Count.ShouldBe(1); grants.Single().RecipientJson.ShouldContain("mother@example.test");
			var histories = await GetRequiredService<IRepository<AdmissionFormChangeHistory, Guid>>().GetListAsync(item => item.ApplicationId == fixture.Application.Id && item.ChangeType == "ParentEmailChanged");
			histories.Count.ShouldBe(1); histories.Single().ChangedBy.ShouldBe(TestUserId);
		});
		// An exact successful retry never restores its old value after a later staff edit.
		context = await service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", ExpectedEmail = "new@example.test", Email = "later@example.test", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "later-email" });
		(await service.ChangeEmailAsync(fixture.Application.Id, input)).Recipients.Single(item => item.Relationship == "mother").Email.ShouldBe("later@example.test");
		input.Email = "different@example.test";
		(await Should.ThrowAsync<BusinessException>(() => service.ChangeEmailAsync(fixture.Application.Id, input))).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
		jobs.ReceivedCalls().ShouldBeEmpty();
	}

	[Theory]
	[InlineData("not-an-email")]
	[InlineData("A <valid@example.test>")]
	[InlineData("valid@example.test\r\nBcc: other@example.test")]
	public async Task ParentRegistration_email_change_rejects_malformed_contact(string email)
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var context = await service.GetAsync(fixture.Application.Id);
		await Should.ThrowAsync<Exception>(() => service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", Email = email, ExpectedEmail = "mother@example.test", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "invalid-email" }));
		(await service.GetAsync(fixture.Application.Id)).Recipients.Single(item => item.Relationship == "mother").Email.ShouldBe("mother@example.test");
	}

	[Fact]
	public async Task ParentRegistration_email_change_rejects_stale_application_and_saved_contact()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		var context = await service.GetAsync(fixture.Application.Id);
		foreach (var stale in new[] { true, false })
			(await Should.ThrowAsync<BusinessException>(() => service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", Email = "new@example.test", ExpectedEmail = stale ? "mother@example.test" : "other@example.test", ConcurrencyStamp = stale ? "stale" : context.ConcurrencyStamp, IdempotencyKey = "stale-" + stale }))).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
	}

	[Theory]
	[InlineData("father")]
	[InlineData("guardian")]
	public async Task ParentRegistration_email_change_owns_capture_without_mutating_shared_guardian(string relationship)
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
			if (relationship == "father")
			{
				AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Father Test", "9999999999", "father@example.test");
				var form = await _applicationAppService.GetFormAsync(fixture.Application.Id);
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.FormDefinitionId, form.Definition.Sections.First().Id,
					"PARENT_EMAIL", "Primary parent email", AdmissionFormFieldType.Email, AdmissionFormBindingType.System, "Guardian.Email", false, false, 20, null, null, null, null, null), true);
			}
			else { applicant.SetProperty("AdmissionStudentField.Guardian.Name", "Aunt Test"); applicant.SetProperty("AdmissionStudentField.Guardian.Email", "guardian@example.test"); applicant.SetProperty(StudentFamilyContactSelection.GuardianRelationshipKey, "Aunt"); }
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var guardians = GetRequiredService<IRepository<AdmissionGuardian, Guid>>();
		var before = await WithUnitOfWorkAsync(async () => (await guardians.GetListAsync()).ToDictionary(item => item.Id, item => item.Email));
		var service = GetRequiredService<IAdmissionParentRegistrationAppService>(); var context = await service.GetAsync(fixture.Application.Id);
		context = await service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = relationship, ExpectedEmail = relationship + "@example.test", Email = "changed@example.test", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "change-" + relationship });
		context.Recipients.Single(item => item.Relationship == relationship).Email.ShouldBe("changed@example.test");
		if (relationship == "father") (await _applicationAppService.GetFormAsync(fixture.Application.Id)).Values.ShouldContain(item => item.FieldCode == "PARENT_EMAIL" && item.ValueJson == "\"changed@example.test\"");
		await WithUnitOfWorkAsync(async () => { foreach (var row in await guardians.GetListAsync()) row.Email.ShouldBe(before[row.Id]); });
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = context.ConcurrencyStamp, Values = [Value("NOTE", "Unrelated edit")] });
		(await service.GetAsync(fixture.Application.Id)).Recipients.Single(item => item.Relationship == relationship).Email.ShouldBe("changed@example.test");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParentRegistration_email_change_refuses_linked_contacts(bool sibling)
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
			if (sibling) AdmissionSiblingParentSelection.Confirm(row, new() { SourceApplicantId = Guid.NewGuid(), Relationship = "mother", Name = "Mother Test", FirstName = "Mother", Mobile = "9999999999", Email = "mother@example.test" }, TestUserId, DateTime.UtcNow, "9999999999", "mother@example.test");
			else row.LinkStudent(Guid.NewGuid());
			await _applicantRepository.UpdateAsync(row, true);
		});
		var context = await service.GetAsync(fixture.Application.Id);
		context.Recipients.Single(item => item.Relationship == "mother").CanChangeEmail.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", Email = "new@example.test", ExpectedEmail = "mother@example.test", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "linked" }));
	}

	[Fact]
	public async Task ParentRegistration_email_change_can_add_a_missing_saved_adult_email_without_a_live_grant()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await WithUnitOfWorkAsync(async () => { var row = await _applicantRepository.GetAsync(fixture.Application.ApplicantId); row.SetProperty("AdmissionStudentField.Mother.Email", null); await _applicantRepository.UpdateAsync(row, true); });
		var context = await service.GetAsync(fixture.Application.Id); context.Recipients.Single(item => item.Relationship == "mother").CanChangeEmail.ShouldBeTrue();
		context = await service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", Email = "added@example.test", ExpectedEmail = null, ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "add-email" });
		context.Recipients.Single(item => item.Relationship == "mother").Email.ShouldBe("added@example.test");
	}

	[Fact]
	public async Task ParentRegistration_email_change_refuses_submitted_contacts_and_keeps_audited_form_owner()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await _applicationAppService.UpdateFormValuesAsync(fixture.Application.Id, new() { ConcurrencyStamp = (await service.GetAsync(fixture.Application.Id)).ConcurrencyStamp, Values = [Value("NOTE", "Complete"), Value("FATHER", "Father")] });
		(await _applicationAppService.SubmitAsync(fixture.Application.Id)).ValidationErrors.ShouldBeEmpty();
		var context = await service.GetAsync(fixture.Application.Id); var recipient = context.Recipients.Single(item => item.Relationship == "mother");
		recipient.CanChangeEmail.ShouldBeFalse(); recipient.EmailChangeBlockedReason!.ShouldContain("audited correction");
		await Should.ThrowAsync<BusinessException>(() => service.ChangeEmailAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", Email = "new@example.test", ExpectedEmail = "mother@example.test", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "submitted" }));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParentRegistration_document_only_correction_at_current_documents_has_no_editable_form_answers_and_rejects_completed_stage(bool submitted)
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await ParentFixtureAsync(withDocuments: true); var service = GetRequiredService<IAdmissionParentRegistrationAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(fixture.Application.Id);
			if (submitted) application.Submit();
			var stages = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
			var current = await stages.GetAsync(application.CurrentStageInstanceId!.Value); current.Complete(TestUserId); await stages.UpdateAsync(current);
			var documents = (await stages.GetListAsync(item => item.WorkflowInstanceId == current.WorkflowInstanceId)).Single(item => item.StageType == AdmissionWorkflowStageType.DocumentVerification);
			documents.Enter(); await stages.UpdateAsync(documents); application.SetCurrentStage(documents.Id); await _applicationRepository.UpdateAsync(application, true);
		});
		var context = await service.GetAsync(fixture.Application.Id); context.CanRequestDocumentCorrections.ShouldBeTrue();
		var document = context.AvailableDocuments.Single();
		context = await service.RequestCorrectionsAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "documents-current", Notes = "Upload the proof.", Documents = [new() { StageInstanceId = document.StageInstanceId, RequirementCode = document.Code }] });
		var token = await ParentTokenAsync(context.Request!.Id);
		var parent = await GetRequiredService<IAdmissionParentRegistrationPublicAppService>().GetAsync(token);
		parent.EditableFieldCodes.ShouldBeEmpty(); parent.Documents.Single().CanUpload.ShouldBeTrue();
		if (!submitted)
			await Should.ThrowAsync<BusinessException>(() => service.RequestCorrectionsAsync(fixture.Application.Id, new() { RecipientRelationship = "mother", ConcurrencyStamp = context.ConcurrencyStamp, IdempotencyKey = "fields-wrong-stage", Notes = "Do not allow fields.", FieldCodes = ["NOTE"], Documents = [new() { StageInstanceId = document.StageInstanceId, RequirementCode = document.Code }] }));
		await WithUnitOfWorkAsync(async () => { var stages = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>(); var stage = await stages.GetAsync(document.StageInstanceId); stage.Complete(TestUserId); await stages.UpdateAsync(stage, true); });
		var closed = await service.GetAsync(fixture.Application.Id);
		closed.CanRequestDocumentCorrections.ShouldBeFalse();
		closed.Request!.Status.ShouldBe("Closed");
		closed.CanRetryDelivery.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionParentRegistrationPublicAppService>().GetAsync(token));
	}

	private AdmissionParentRegistrationAppService ParentContactService(IBackgroundJobManager jobs) => new(
		GetRequiredService<AdmissionParentRegistrationData>(), GetRequiredService<AdmissionParentRegistrationAccess>(),
		GetRequiredService<AdmissionParentRegistrationDocuments>(), GetRequiredService<IDataProtectionProvider>(), jobs,
		GetRequiredService<AdmissionApplicationFormService>()) { LazyServiceProvider = GetRequiredService<AdmissionParentRegistrationData>().LazyServiceProvider };
}
