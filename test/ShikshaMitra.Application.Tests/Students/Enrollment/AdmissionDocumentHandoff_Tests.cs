using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Document_handoff_successful_retry_is_stable_when_finalization_changes_a_condition_fact() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement
			{
				Code = "LATER", Name = "Status-dependent evidence", Requirement = "conditional",
				ConditionJson = """{"field":"application.status","operator":"Equals","value":"AdmissionFinalized"}"""
			};
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
			await RestoreHistoricalDocumentPolicyAsync(fixture);
			var first = await FinalizeAsync(ready, true, "document-handoff-stable-retry");
			var retry = await FinalizeAsync(ready, true, "document-handoff-stable-retry");
			retry.StudentId.ShouldBe(first.StudentId);
			retry.OnboardingCaseId.ShouldBe(first.OnboardingCaseId);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Document_handoff_preserves_versions_reviews_custom_evidence_and_existing_student_uploads(bool existingStudent) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var mapped = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", DocumentTypeId = type.Id };
			var custom = new AdmissionDocumentRequirement { Code = "ORIGINAL", Name = "School-specific original", CollectionMode = "original" };
			var waived = new AdmissionDocumentRequirement { Code = "OPTIONAL", Name = "Additional evidence", Requirement = "optional" };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(mapped, custom, waived), existingStudent);
			var rejected = await AddHandoffEvidenceAsync(fixture, mapped, decision: "rejected");
			var accepted = await AddHandoffEvidenceAsync(fixture, mapped, version: 2);
			await AddHandoffEvidenceAsync(fixture, custom, kind: "original");
			await AddHandoffEvidenceAsync(fixture, waived, kind: "waiver");
			var documents = GetRequiredService<IRepository<StudentDocument, Guid>>();
			StudentDocument? previous = null;
			if (fixture.ExistingStudentId.HasValue)
				previous = await WithUnitOfWorkAsync(() => documents.InsertAsync(new StudentDocument(
					Guid.NewGuid(), TestTenantId, fixture.ExistingStudentId.Value, type.Id, "prior-student-blob",
					"prior.pdf", "application/pdf", 4), autoSave: true));
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>()
				.GetAsync(accepted.Id))).StudentId.ShouldBeNull();
			var studentCount = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
			var finalized = await FinalizeAsync(ready, !existingStudent, "document-handoff");
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCount + (existingStudent ? 0 : 1));
			var service = GetRequiredService<IStudentAdmissionEvidenceAppService>();
			var linked = await service.GetListAsync(finalized.StudentId);
			linked.Count.ShouldBe(4);
			linked.ShouldAllBe(item => item.StudentLinkedAt.HasValue && item.StudentLinkedBy == TestUserId);
			linked.Single(item => item.Id == accepted.Id).Reviews.Single().ReviewedBy.ShouldBe(HandoffReviewer);
			linked.Single(item => item.Id == rejected.Id).Status.ShouldBe("rejected");
			linked.Single(item => item.RequirementCode == "ORIGINAL").CanDownload.ShouldBeFalse();
			linked.Single(item => item.RequirementCode == "OPTIONAL").Status.ShouldBe("waived");
			(await service.DownloadAsync(finalized.StudentId, accepted.Id)).Content.ShouldBe(HandoffPdf);
			var again = await FinalizeAsync(ready, !existingStudent, "document-handoff");
			again.StudentId.ShouldBe(finalized.StudentId);
			(await service.GetListAsync(finalized.StudentId)).Select(item => item.StudentLinkedAt)
				.ShouldBe(linked.Select(item => item.StudentLinkedAt));
			(await WithUnitOfWorkAsync(() => documents.CountAsync(item => item.StudentId == finalized.StudentId)))
				.ShouldBe(existingStudent ? 1 : 0);
			if (previous != null)
				(await WithUnitOfWorkAsync(() => documents.GetAsync(previous.Id))).BlobName.ShouldBe("prior-student-blob");
		});

	[Fact]
	public Task Document_handoff_keeps_the_latest_rejected_revision_blocking_onboarding_readiness() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			await AddHandoffEvidenceAsync(fixture, requirement);
			var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
			// Model an inconsistent legacy/admin state: a newer revision exists after workflow completion.
			await AddHandoffEvidenceAsync(fixture, requirement, version: 2, decision: "rejected");
			var studentCount = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(ready.Id);
			preview.IsReady.ShouldBeTrue();
			var finalized = await FinalizeAsync(ready, true, "document-handoff-pending-review");
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCount + 1);
			var onboarding = GetRequiredService<IStudentOnboardingAppService>();
			var item = await onboarding.GetAsync(finalized.OnboardingCaseId);
			item = await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.Documents).Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
			item.StudentEnrollmentId.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(enrollment => enrollment.StudentId == finalized.StudentId))).ShouldBe(0);
		});

	[Fact]
	public Task Document_handoff_student_reads_and_downloads_reject_foreign_students_and_tenants() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			var evidence = await AddHandoffEvidenceAsync(fixture, requirement);
			var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
			var finalized = await FinalizeAsync(ready, true, "document-handoff-access");
			var other = await CreateExistingStudentAsync(fixture.Offering, "Other", "Student");
			var service = GetRequiredService<IStudentAdmissionEvidenceAppService>();
			(await service.GetListAsync(other.Id)).ShouldBeEmpty();
			await Should.ThrowAsync<EntityNotFoundException>(() => service.DownloadAsync(other.Id, evidence.Id));
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
				new Claim(AbpClaimTypes.UserName, "staff-without-student-scope")
			], "Test"))))
			{
				(await Should.ThrowAsync<BusinessException>(() => service.GetListAsync(finalized.StudentId)))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				(await Should.ThrowAsync<BusinessException>(() => service.DownloadAsync(finalized.StudentId, evidence.Id)))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
			using var foreignTenant = _currentTenant.Change(Guid.NewGuid());
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetListAsync(finalized.StudentId));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.DownloadAsync(finalized.StudentId, evidence.Id));
		});
}
