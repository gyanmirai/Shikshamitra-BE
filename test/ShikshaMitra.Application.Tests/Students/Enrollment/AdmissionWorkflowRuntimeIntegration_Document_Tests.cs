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
	public async Task Managed_documents_gate_skip_and_override_and_require_replacement_review()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(new AdmissionDocumentRequirement() { Code = "BIRTH", Name = "Birth certificate", CollectionMode = "both" }));
		var service = GetRequiredService<IAdmissionDocumentVerificationAppService>();
		var state = await service.GetAsync(fixture.Application.Id, fixture.StageId);
		state.DocumentVerificationEnabled.ShouldBeTrue();
		state.CanComplete.ShouldBeFalse();
		foreach (var mode in new[] { "normal", "skip", "override" })
		{
			var exception = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, new()
			{
				ExpectedCurrentStageInstanceId = fixture.StageId, IdempotencyKey = Guid.NewGuid().ToString(), ConcurrencyStamp = state.ConcurrencyStamp,
				SkipCurrentStage = mode == "skip", AdminOverride = mode == "override", Reason = "Must not bypass evidence"
			}));
			exception.Code.ShouldBe(AdmissionDocumentErrorCodes.EvidenceRequired);
		}
		var originalStamp = state.ConcurrencyStamp;
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state.Requirements.Single().Status.ShouldBe("pendingReview");
		state.Requirements.Single().Evidence.Last().SubmittedAt.Kind.ShouldBe(DateTimeKind.Utc);
		(Math.Abs((DateTime.UtcNow - state.Requirements.Single().Evidence.Last().SubmittedAt).TotalMinutes) < 1).ShouldBeTrue();
		state.Requirements.Single().CanReview.ShouldBeTrue();
		state.Requirements.Single().CanAccept.ShouldBeFalse();
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "rejected", Reason = "Uploaded copy is unreadable" });
		state.Requirements.Single().Status.ShouldBe("rejected");
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		var stale = await Should.ThrowAsync<BusinessException>(() => service.RecordOriginalAsync(fixture.Application.Id, fixture.StageId, "BIRTH",
			new() { ConcurrencyStamp = originalStamp, Notes = "Saw original" }));
		stale.Code.ShouldBe(AdmissionDocumentErrorCodes.ConcurrencyConflict);
		state = await service.RecordOriginalAsync(fixture.Application.Id, fixture.StageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Compared original birth certificate with uploaded copy" });
		state.Requirements.Single().Evidence.Count.ShouldBe(3);
		state = await service.ReviewAsync(state.Requirements.Single().Evidence.Last().Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
		state.CanComplete.ShouldBeTrue();
		var acceptedId = state.Requirements.Single().Evidence.Last().Id;
		state = await UploadDocumentAsync(service, fixture.Application.Id, fixture.StageId, state);
		state.CanComplete.ShouldBeFalse();
		state.Requirements.Single().Evidence.Last().OriginalSeen.ShouldBeFalse();
		state.Requirements.Single().Evidence.Single(item => item.Id == acceptedId).Status.ShouldBe("accepted");
		await Should.ThrowAsync<BusinessException>(() => service.ReviewAsync(acceptedId, new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" }));
		state = await service.WaiveAsync(fixture.Application.Id, fixture.StageId, "BIRTH", new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Original unavailable after documented exceptional review" });
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
		var state = await GetRequiredService<IAdmissionDocumentVerificationAppService>().GetAsync(fixture.Application.Id, fixture.StageId);
		state.Requirements.Single().ConditionStatus.ShouldBe("undetermined");
		state.CanComplete.ShouldBeFalse();
		state.Requirements.Single().CanWaive.ShouldBeFalse();
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
