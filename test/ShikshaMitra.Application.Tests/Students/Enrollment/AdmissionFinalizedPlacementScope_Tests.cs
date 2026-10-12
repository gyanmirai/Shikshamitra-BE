using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public Task Finalized_admissions_obey_placement_scope_for_lists_identity_notes_and_documents(bool activated, bool classWide) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var manager = GetRequiredService<DivisionManager>();
			var divisions = GetRequiredService<IRepository<Division, Guid>>();
			var assigned = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Finalized A", 30), true);
			var outside = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Finalized B", 30), true);
			var alpha = await CreateFinalizedPlacementFixtureAsync(offering, assigned.Id, activated);
			var beta = await CreateFinalizedPlacementFixtureAsync(offering, outside.Id, activated, assigned.Id);
			var unplaced = await _applicationRepository.InsertAsync(new AdmissionApplication(Guid.NewGuid(), TestTenantId,
				$"UNPLACED-{Guid.NewGuid():N}", (await CreateApplicantAsync("Unplaced")).Id, null,
				offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value), true);
			var staffId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(staffId, $"finalized-scope-{staffId:N}");
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var assignment = await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId,
				academicYearId: year, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId, divisionId: classWide ? null : assigned.Id), true);
			using var principal = _principalAccessor.Change(ProfilePrincipal(staffId));
			(await _applicationAppService.GetAsync(alpha.ApplicationId)).Id.ShouldBe(alpha.ApplicationId);
			(await _applicationAppService.GetAsync(unplaced.Id)).Id.ShouldBe(unplaced.Id);
			var applicants = GetRequiredService<IAdmissionApplicantAppService>();
			var documents = GetRequiredService<IAdmissionDocumentVerificationAppService>();
			(await applicants.GetAsync(alpha.ApplicantId)).Applicant.Id.ShouldBe(alpha.ApplicantId);
			(await documents.DownloadAsync(alpha.EvidenceId)).Content.ShouldBe(HandoffPdf);
			var list = await _applicationAppService.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 });
			list.Items.ShouldContain(item => item.Id == alpha.ApplicationId);
			if (classWide)
			{
				list.Items.ShouldContain(item => item.Id == beta.ApplicationId);
				(await applicants.GetAsync(beta.ApplicantId)).Applicant.Id.ShouldBe(beta.ApplicantId);
				(await documents.DownloadAsync(beta.EvidenceId)).Content.ShouldBe(HandoffPdf);
			}
			else
			{
				list.Items.ShouldNotContain(item => item.Id == beta.ApplicationId);
				await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetAsync(beta.ApplicationId));
				await Should.ThrowAsync<EntityNotFoundException>(() => applicants.GetAsync(beta.ApplicantId));
				await Should.ThrowAsync<EntityNotFoundException>(() => applicants.GetNotesAsync(beta.ApplicantId));
				await Should.ThrowAsync<EntityNotFoundException>(() => applicants.CreateNoteAsync(beta.ApplicantId, new() { Text = "Must not persist" }));
				await Should.ThrowAsync<EntityNotFoundException>(() => documents.DownloadAsync(beta.EvidenceId));
			}
			await WithUnitOfWorkAsync(() => _scopeRepository.DeleteAsync(assignment.Id, true));
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetAsync(alpha.ApplicationId));
		});

	[Fact]
	public Task Admission_event_instants_keep_utc_kind_after_database_reload() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateUserScopeOfferingAsync();
		var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(offering.StandardId, "UTC fixture", 30), true);
		var fixture = await CreateFinalizedPlacementFixtureAsync(offering, division.Id, true);
		var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
		application.DecisionAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
		application.FinalizedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
		application.FinalizedAt.Value.ShouldBeGreaterThanOrEqualTo(application.DecisionAt.Value);
		var evidence = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>().GetAsync(fixture.EvidenceId));
		evidence.SubmittedAt.Kind.ShouldBe(DateTimeKind.Utc);
	});
}
