using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Document_class_scope_uses_the_offering_and_not_an_applicant_answer()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" }));
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(fixture.Application.Id);
			var form = await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().GetAsync(item => item.ApplicationId == application.Id);
			var differentClass = Guid.NewGuid();
			await GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, form.Id, "class", $"\"{differentClass}\""), true);
			var policy = HandoffPolicy(
				new() { Code = "COMMON", Name = "Common document" },
				new() { Code = "OWN_CLASS", Name = "Selected class document", StandardIds = [fixture.Offering.StandardId] },
				new() { Code = "OTHER_CLASS", Name = "Other class document", StandardIds = [differentClass] });
			var result = await GetRequiredService<AdmissionDocumentEvaluationService>().EvaluateAsync(application, fixture.StageId, policy);
			result.Requirements.Single(item => item.Requirement.Code == "COMMON").IsRequired.ShouldBeTrue();
			result.Requirements.Single(item => item.Requirement.Code == "OWN_CLASS").IsRequired.ShouldBeTrue();
			var other = result.Requirements.Single(item => item.Requirement.Code == "OTHER_CLASS");
			other.IsRequired.ShouldBeFalse();
			other.Status.ShouldBe("notRequired");
			other.Blockers.ShouldBeEmpty();
		});
	}
	[Fact]
	public async Task Managed_documents_preserve_required_skip_denial_and_replacement_review_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement() { Code = "BIRTH", Name = "Birth certificate" }));
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		state.DocumentVerificationEnabled.ShouldBeTrue();
		// Outstanding evidence can continue to Onboarding, but a required stage cannot be skipped.
		state.CanComplete.ShouldBeTrue();
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, new()
		{
			ExpectedCurrentStageInstanceId = fixture.StageId, IdempotencyKey = Guid.NewGuid().ToString(), ConcurrencyStamp = state.ConcurrencyStamp,
			SkipCurrentStage = true, Reason = "Required stage cannot be skipped"
		}))).Code.ShouldBe(AdmissionApplicationErrorCodes.ManualSkipNotAllowed);
		var originalStamp = state.ConcurrencyStamp;
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state.Requirements.Single().Status.ShouldBe("pendingReview");
		state.Requirements.Single().Evidence.Last().SubmittedAt.Kind.ShouldBe(DateTimeKind.Utc);
		(Math.Abs((DateTime.UtcNow - state.Requirements.Single().Evidence.Last().SubmittedAt).TotalMinutes) < 1).ShouldBeTrue();
		state.Requirements.Single().CanReview.ShouldBeTrue();
		state.Requirements.Single().CanAccept.ShouldBeTrue();
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "rejected", Reason = "Uploaded copy is unreadable" });
		state.Requirements.Single().Status.ShouldBe("rejected");
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		var stale = await Should.ThrowAsync<BusinessException>(() => service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id,
			new() { ConcurrencyStamp = originalStamp, Decision = "accepted" }));
		stale.Code.ShouldBe(AdmissionDocumentErrorCodes.ConcurrencyConflict);
		state.Requirements.Single().Evidence.Count.ShouldBe(2);
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeTrue();
		var acceptedId = state.Requirements.Single().Evidence.Last().Id;
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state.CanComplete.ShouldBeTrue();
		state.Requirements.Single().Evidence.Last().OriginalSeen.ShouldBeFalse();
		state.Requirements.Single().Evidence.Single(item => item.Id == acceptedId).Status.ShouldBe("accepted");
		await Should.ThrowAsync<BusinessException>(() => service.ReviewAsync(acceptedId, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" }));
		await Should.ThrowAsync<BusinessException>(() => service.WaiveAsync(fixture.Application.Id, fixture.StageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "A waiver cannot replace current verified evidence" }));
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeTrue();
		await AdvanceAssessmentAsync(fixture.Application.Id, fixture.StageId);
		(await service.GetAsync(fixture.Application.Id, fixture.StageId)).IsActive.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.RecordOriginalAsync(fixture.Application.Id, fixture.StageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Cannot mutate history" }));
	}
	[Fact]
	public async Task Managed_documents_private_download_validates_content_and_scope()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement() { Code = "BIRTH", Name = "Birth certificate" }));
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		using var malformed = new RemoteStreamContent(new MemoryStream("not a pdf"u8.ToArray()), "proof.pdf", "application/pdf");
		await Should.ThrowAsync<BusinessException>(() => service.UploadAsync(fixture.Application.Id, fixture.StageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, File = malformed }));
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		var id = state.Requirements.Single().Evidence.Single().Id;
		(await service.DownloadAsync(id)).Content.ShouldBe(HandoffPdf);
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(Guid.NewGuid(), fixture.StageId));
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			await Should.ThrowAsync<EntityNotFoundException>(() => service.DownloadAsync(id));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.Application.Id, fixture.StageId));
		}
	}
	[Fact]
	public async Task Managed_documents_condition_missing_facts_block_and_authoritative_status_cannot_be_spoofed()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement()
		{
			Code = "BIRTH", Name = "Birth certificate", Requirement = "conditional",
			ConditionJson = "{\"operator\":\"Equals\",\"field\":\"application.missing\",\"value\":\"yes\"}"
		}));
		var evaluation = await WithUnitOfWorkAsync(async () => await GetRequiredService<AdmissionDocumentEvaluationService>()
			.EvaluateAsync(await _applicationRepository.GetAsync(fixture.Application.Id), fixture.StageId, fixture.Policy));
		evaluation.Requirements.Single().ConditionStatus.ShouldBe("undetermined");
		evaluation.Blockers.ShouldNotBeEmpty();
		await RestoreHistoricalDocumentPolicyAsync(fixture);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
			await GetRequiredService<ShikshaMitra.Students.StudentEnrollments.Onboarding.OnboardingRequirementSnapshotService>()
				.PrepareDocumentPoliciesAsync(await _applicationRepository.GetAsync(fixture.Application.Id)))))
			.Code.ShouldBe(AdmissionDocumentErrorCodes.EvidenceRequired);
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(fixture.Application.Id);
			var form = await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().FindAsync(item => item.ApplicationId == application.Id);
			form.ShouldNotBeNull();
			var values = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
			await values.InsertAsync(new(Guid.NewGuid(), TestTenantId, form.Id, "status", "\"AdmissionFinalized\""), true);
			await values.InsertAsync(new(Guid.NewGuid(), TestTenantId, form.Id, "assessment.outcome", "\"passed\""), true);
			var facts = await GetRequiredService<AdmissionWorkflowRuntimeValueService>().ReadAsync(application, protectManagedFacts: true);
			facts["application.status"].ShouldBe(application.Status.ToString());
			facts.ContainsKey("assessment.outcome").ShouldBeFalse();
		});
	}
	[Fact]
	public async Task Managed_documents_conditions_use_prior_real_assessment_result()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, assessmentId, _) = await CreateManagedAssessmentApplicationAsync();
		var policy = HandoffPolicy(new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", Requirement = "conditional",
			ConditionJson = "{\"operator\":\"Equals\",\"field\":\"assessment.outcome\",\"value\":\"passed\"}" });
		var documentId = Guid.NewGuid();
		async Task<string> EvaluateAsync() => await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<AdmissionDocumentEvaluationService>().EvaluateAsync(await _applicationRepository.GetAsync(application.Id), documentId, policy)).Requirements.Single().ConditionStatus);
		(await EvaluateAsync()).ShouldBe("undetermined");
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var summary = await service.GetAsync(application.Id, assessmentId);
		summary = await service.BookAsync(application.Id, assessmentId, new() { ConcurrencyStamp = summary.ConcurrencyStamp });
		var attempt = summary.Attempts.Single();
		summary = await service.RecordAttendanceAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Attendance = "present" });
		await service.RecordResultAsync(attempt.Id, new() { ConcurrencyStamp = summary.Attempts.Single().ConcurrencyStamp, Marks = 90 });
		(await EvaluateAsync()).ShouldBe("matched");
	}
	[Fact]
	public async Task Managed_documents_optional_evidence_does_not_block_completion()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement() { Code = "BIRTH", Name = "Birth certificate", Requirement = "optional" }));
		var state = await GetRequiredService<IAdmissionDocumentVerificationAppService>().GetAsync(fixture.Application.Id, fixture.StageId);
		state.Requirements.Single().IsRequired.ShouldBeFalse();
		state.CanComplete.ShouldBeTrue();
		await AdvanceAssessmentAsync(fixture.Application.Id, fixture.StageId);
	}
	private static async Task<AdmissionDocumentVerificationDto> UploadDocumentAsync(IAdmissionDocumentVerificationAppService service,
		Guid applicationId, Guid stageId, AdmissionDocumentVerificationDto state)
	{
		using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "birth.pdf", "application/pdf", HandoffPdf.Length);
		return await service.UploadAsync(applicationId, stageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, File = file });
	}
}
