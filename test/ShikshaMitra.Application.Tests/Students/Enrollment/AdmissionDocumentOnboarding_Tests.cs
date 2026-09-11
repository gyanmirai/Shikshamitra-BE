using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("upload", true)]
	[InlineData("both", true)]
	[InlineData("original", false)]
	[InlineData("waiver", false)]
	[InlineData("custom", false)]
	public Task Document_handoff_only_accepted_mapped_files_satisfy_independent_onboarding_requirements(string mode, bool ready) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var requirement = new AdmissionDocumentRequirement
			{
				Code = "CERTIFICATE", Name = "Certificate", DocumentTypeId = mode == "custom" ? null : type.Id,
				CollectionMode = mode is "waiver" or "custom" ? "upload" : mode
			};
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			var kind = mode is "original" or "both" ? "original" : mode == "waiver" ? "waiver" : "upload";
			await AddHandoffEvidenceAsync(fixture, requirement, kind: kind);
			var application = await CompleteDocumentHandoffApplicationAsync(fixture);
			var finalized = await FinalizeAsync(application, true, $"document-readiness-{mode}");
			var reader = GetRequiredService<AdmissionStudentEvidenceReader>();
			(await WithUnitOfWorkAsync(() => reader.GetAcceptedUploadDocumentTypeIdsAsync(finalized.StudentId)))
				.Contains(type.Id).ShouldBe(ready);
			var onboarding = GetRequiredService<IStudentOnboardingAppService>();
			var current = await onboarding.GetAsync(finalized.OnboardingCaseId);
			var refreshed = await onboarding.RefreshReadinessAsync(current.Id, current.ConcurrencyStamp);
			refreshed.Steps.Single(item => item.Key == StudentOnboardingStepKeys.Documents).Status
				.ShouldBe(ready ? StudentOnboardingStepStatus.Completed : StudentOnboardingStepStatus.Blocked);
		});

	[Theory]
	[InlineData(null)]
	[InlineData("rejected")]
	public Task Document_handoff_readiness_does_not_reuse_an_older_accepted_revision(string? latestDecision) =>
		RunCertificationScenarioAsync(async () =>
		{
			var type = await CreateHandoffDocumentTypeAsync();
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", DocumentTypeId = type.Id };
			var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
			await AddHandoffEvidenceAsync(fixture, requirement);
			var application = await CompleteDocumentHandoffApplicationAsync(fixture);
			var finalized = await FinalizeAsync(application, true, "document-readiness-latest");
			// A legacy/imported later revision must not let an older acceptance satisfy current readiness.
			var newer = await AddHandoffEvidenceAsync(fixture, requirement, version: 2, decision: latestDecision);
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>();
				var stored = await repository.GetAsync(newer.Id);
				stored.LinkToStudent(finalized.StudentId, TestUserId, DateTime.UtcNow);
				await repository.UpdateAsync(stored, autoSave: true);
			});
			(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionStudentEvidenceReader>()
				.GetAcceptedUploadDocumentTypeIdsAsync(finalized.StudentId))).ShouldBeEmpty();
		});

	[Fact]
	public Task Document_handoff_preserves_finalization_for_legacy_workflows_without_managed_documents() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("LegacyDocuments");
			var result = await FinalizeAsync(fixture.Application, true, "legacy-document-finalization");
			result.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			(await GetRequiredService<IStudentAdmissionEvidenceAppService>().GetListAsync(result.StudentId)).ShouldBeEmpty();
			(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionStudentEvidenceReader>()
				.GetAcceptedUploadDocumentTypeIdsAsync(result.StudentId))).ShouldBeEmpty();
		});
}
