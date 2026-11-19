using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Document_onboarding_upload_rejects_stale_state_outside_scope_and_foreign_tenant() =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var fixture = await CreateCertificationFixtureAsync("ScopedOnboardingUpload");
			var finalized = await FinalizeAsync(fixture.Application, true, "scoped-onboarding-upload");
			var service = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var workspace = await service.GetAsync(finalized.OnboardingCaseId);
			using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "birth.pdf", "application/pdf", HandoffPdf.Length);
			var input = new StudentOnboardingDocumentUploadInput
			{
				DocumentTypeId = type.Id, ConcurrencyStamp = "stale", File = file
			};
			(await Should.ThrowAsync<BusinessException>(() => service.UploadAsync(workspace.CaseId, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
			input.ConcurrencyStamp = workspace.ConcurrencyStamp;
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
				new Claim(AbpClaimTypes.UserName, "staff-without-onboarding-scope")
			], "Test"))))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(workspace.CaseId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.UploadAsync(workspace.CaseId, input));
			}
			using (_currentTenant.Change(Guid.NewGuid()))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(workspace.CaseId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.UploadAsync(workspace.CaseId, input));
			}
			(await service.GetAsync(workspace.CaseId)).Documents.Single().HasStudentUpload.ShouldBeFalse();
		});

	[Theory]
	[InlineData(StudentLifeCycleStatusEnum.InActive)]
	[InlineData(StudentLifeCycleStatusEnum.Left)]
	public Task Document_onboarding_upload_does_not_unlock_later_student_lifecycle_states(StudentLifeCycleStatusEnum status) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var fixture = await CreateCertificationFixtureAsync("LaterLifecycle");
			var finalized = await FinalizeAsync(fixture.Application, true, "later-lifecycle-upload");
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(finalized.StudentId);
				student.ChangeStatus(status);
				await _studentRepository.UpdateAsync(student, autoSave: true);
			});
			var service = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var workspace = await service.GetAsync(finalized.OnboardingCaseId);
			workspace.CanUpload.ShouldBeFalse();
			using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "birth.pdf", "application/pdf", HandoffPdf.Length);
			(await Should.ThrowAsync<BusinessException>(() => service.UploadAsync(workspace.CaseId,
				new() { DocumentTypeId = type.Id, ConcurrencyStamp = workspace.ConcurrencyStamp, File = file })))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Document_onboarding_requires_verified_workflow_evidence_after_legacy_waiver_for_new_and_returning_students(bool existingStudent) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", DocumentTypeId = type.Id };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement), existingStudent);
			await AddHandoffEvidenceAsync(fixture, requirement, kind: "waiver");
			var application = await CompleteDocumentHandoffApplicationAsync(fixture);
			var finalized = await FinalizeAsync(application, !existingStudent, "onboarding-document-upload");
			var service = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var workspace = await service.GetAsync(finalized.OnboardingCaseId);
			workspace.CanUpload.ShouldBeTrue();
			workspace.Documents.Single().HasAcceptedAdmissionEvidence.ShouldBeFalse();
			workspace.Documents.Single().HasStudentUpload.ShouldBeFalse();
			using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "birth.pdf", "application/pdf", HandoffPdf.Length);
			var result = await service.UploadAsync(workspace.CaseId, new()
			{
				DocumentTypeId = type.Id, ConcurrencyStamp = workspace.ConcurrencyStamp, File = file
			});
			result.Documents.Single().HasStudentUpload.ShouldBeTrue();
			result.Documents.Single().HasAcceptedAdmissionEvidence.ShouldBeFalse();
			var onboarding = GetRequiredService<IStudentOnboardingAppService>();
			var current = await onboarding.GetAsync(finalized.OnboardingCaseId);
			current.Steps.Single(item => item.Key == StudentOnboardingStepKeys.Documents).Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
			// A validated legacy Student upload does not accept the frozen workflow checklist.
			result.Documents.Single().IsValidated.ShouldBeTrue();
			var workflowDocuments = GetRequiredService<IOnboardingWorkflowDocumentsAppService>();
			var state = (await workflowDocuments.GetAsync(current.Id)).Single();
			using var verifiedFile = new RemoteStreamContent(new MemoryStream(HandoffPdf), "verified-birth.pdf", "application/pdf", HandoffPdf.Length);
			state = await workflowDocuments.UploadAsync(current.Id, fixture.StageId, "BIRTH", new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp, File = verifiedFile
			});
			state.Requirements.Single().Status.ShouldBe("pendingReview");
			await workflowDocuments.ReviewAsync(current.Id, state.Requirements.Single().Evidence.Last().Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted" });
			current = await onboarding.GetAsync(current.Id);
			current.Steps.Single(item => item.Key == StudentOnboardingStepKeys.Documents).Status.ShouldBe(StudentOnboardingStepStatus.Completed);
			// Registration endpoints keep their original lifecycle restriction even while onboarding is active.
			(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<StudentDocumentManager>()
				.UploadDocumentAsync(finalized.StudentId, type.Id, "birth.pdf", "application/pdf", HandoffPdf.Length, HandoffPdf))))
				.Code.ShouldBe("Student:InvalidStatusForDocumentUpload");
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Document_onboarding_upload_rejects_cancelled_and_completed_cases(bool completed) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var fixture = await CreateCertificationFixtureAsync("InactiveDocumentCase");
			var finalized = await FinalizeAsync(fixture.Application, true, "inactive-onboarding-upload");
			await WithUnitOfWorkAsync(async () =>
			{
				var onboarding = await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId);
				if (completed) onboarding.CompleteHistoricalImport(Guid.NewGuid(), null, null, DateTime.UtcNow);
				else onboarding.Cancel("Case cancelled", DateTime.UtcNow);
				await _onboardingCaseRepository.UpdateAsync(onboarding, autoSave: true);
			});
			var service = GetRequiredService<IStudentOnboardingDocumentAppService>();
			var workspace = await service.GetAsync(finalized.OnboardingCaseId);
			workspace.CanUpload.ShouldBeFalse();
			using var file = new RemoteStreamContent(new MemoryStream(HandoffPdf), "birth.pdf", "application/pdf", HandoffPdf.Length);
			(await Should.ThrowAsync<BusinessException>(() => service.UploadAsync(workspace.CaseId,
				new() { DocumentTypeId = type.Id, ConcurrencyStamp = workspace.ConcurrencyStamp, File = file })))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
			(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<StudentDocumentManager>()
				.UploadOnboardingDocumentAsync(finalized.StudentId, workspace.CaseId, type.Id, "birth.pdf", "application/pdf", HandoffPdf.Length, HandoffPdf))))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
		});
}
