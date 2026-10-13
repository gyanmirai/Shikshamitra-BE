using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_worklist_filters_count_and_sort_before_paging_and_preserve_duplicate_names() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var matches = fixture.Cases.Where(item => item.DivisionId == fixture.DivisionA).OrderBy(item => item.Id).ToArray();
			var page = await service.GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, PlacementDivisionId = fixture.DivisionA,
				StudentNameFilter = "ALICE PATEL", StudentNameOperator = "equals",
				OwnerNameFilter = "mira", OwnerNameOperator = "startsWith",
				RollNumberOperator = "between", RollNumberMin = 1, RollNumberMax = 3,
				Sorting = "StudentName asc", SkipCount = 1, MaxResultCount = 1
			});
			page.TotalCount.ShouldBe(2); page.Items.Single().Id.ShouldBe(matches[1].Id);
			var first = await service.GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, StudentNameFilter = "patel", StudentNameOperator = "endsWith",
				RollNumberFilter = 3, RollNumberOperator = "greaterThan", Sorting = "RollNumber desc"
			});
			first.TotalCount.ShouldBe(1); first.Items.Single().RollNumber.ShouldBe(5);
			var unassigned = await service.GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, OwnerNameFilter = "not assigned", OwnerNameOperator = "equals"
			});
			unassigned.TotalCount.ShouldBe(2);
			(await service.GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, StudentNameFilter = "ob pa", StudentNameOperator = "contains"
			})).Items.Single().StudentName.ShouldBe("Bob Patel");
			foreach (var sorting in new[] { "Placement asc", "DivisionName desc", "OwnerName asc", "AssignedToUserName desc", "Status asc" })
				(await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, Sorting = sorting })).TotalCount.ShouldBe(4);
			(await service.GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, PendingTask = "division", StudentNameFilter = "cara", StudentNameOperator = "startsWith"
			})).Items.Single().Id.ShouldBe(fixture.Cases[3].Id);
		});

	[Fact]
	public Task Onboarding_worklist_custom_filters_cannot_broaden_academic_or_tenant_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingWorklistFixtureAsync();
			var staff = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staff, $"worklist-staff-{staff:N}");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staff,
				academicYearId: fixture.Cases[0].AcademicYearId, educationBoardId: fixture.Offering.BoardId,
				educationMediumId: fixture.Offering.MediumId, educationMediumStageId: fixture.Offering.StageId,
				standardId: fixture.Offering.StandardId, divisionId: fixture.DivisionA), true);
			using var principal = _principalAccessor.Change(ProfilePrincipal(staff));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var allowed = await service.GetListAsync(new() { StudentNameFilter = "patel", Sorting = "StudentName desc" });
			allowed.TotalCount.ShouldBe(2); allowed.Items.ShouldAllBe(item => item.DivisionId == fixture.DivisionA);
			(await service.GetListAsync(new() { PlacementDivisionId = fixture.DivisionB, StudentNameFilter = "bob" })).TotalCount.ShouldBe(0);
			using (_currentTenant.Change(Guid.NewGuid()))
				(await service.GetListAsync(new() { StudentNameFilter = "alice" })).TotalCount.ShouldBe(0);
		});

	[Fact]
	public Task Onboarding_worklist_rejects_unknown_operators_ranges_and_untrusted_sorts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var service = GetRequiredService<IStudentOnboardingAppService>();
			foreach (var input in new[]
			{
				new StudentOnboardingListInput { StudentNameFilter = "Alice", StudentNameOperator = "or" },
				new StudentOnboardingListInput { OwnerNameFilter = "Mira", OwnerNameOperator = "or" },
				new StudentOnboardingListInput { RollNumberFilter = 1, RollNumberOperator = "or" },
				new StudentOnboardingListInput { RollNumberMin = 5, RollNumberMax = 1, RollNumberOperator = "between" },
				new StudentOnboardingListInput { Sorting = "StudentName desc, Id asc" },
				new StudentOnboardingListInput { Sorting = "StudentId asc" }
			})
				(await Should.ThrowAsync<AbpValidationException>(() => service.GetListAsync(input))).ValidationErrors.ShouldNotBeEmpty();
		});

	private async Task<OnboardingWorklistFixture> CreateOnboardingWorklistFixtureAsync()
	{
		var seed = await CreateAcademicFilterFixtureAsync();
		var manager = GetRequiredService<DivisionManager>();
		var divisions = GetRequiredService<IRepository<Division, Guid>>();
		var divisionA = await divisions.InsertAsync(await manager.CreateAsync(seed.Offering.StandardId, "Worklist A", 30), true);
		var divisionB = await divisions.InsertAsync(await manager.CreateAsync(seed.Offering.StandardId, "Worklist B", 30), true);
		var ownerId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(ownerId, $"worklist-owner-{ownerId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var owner = await repository.GetAsync(ownerId); owner.Name = "Mira"; owner.Surname = "Staff";
			await repository.UpdateAsync(owner, true);
		});
		var cases = new StudentOnboardingCase[4];
		for (var index = 0; index < cases.Length; index++)
		{
			var applicant = await CreateApplicantAsync($"Worklist{index}");
			var student = await CreateExistingStudentAsync(seed.Offering, index is 0 or 2 ? "Alice" : index == 1 ? "Bob" : "Cara", index == 3 ? "Shah" : "Patel");
			cases[index] = await WithUnitOfWorkAsync(async () =>
			{
				var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"WORKLIST-{Guid.NewGuid():N}",
					applicant.Id, null, seed.Offering.AdmissionCycleId, seed.Offering.Id,
					seed.Offering.WorkflowDefinitionId!.Value, seed.Offering.ApplicationFormDefinitionId!.Value);
				await _applicationRepository.InsertAsync(application, true);
				var year = (await _cycleRepository.GetAsync(seed.Offering.AdmissionCycleId)).AcademicYearId;
				var item = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id, student.Id,
					seed.Offering.Id, year, seed.Offering.BoardId, seed.Offering.MediumId, seed.Offering.StageId,
					seed.Offering.StandardId, $"worklist-{application.Id:N}", null, DateTime.UtcNow.AddDays(7));
				await _onboardingCaseRepository.InsertAsync(item, true);
				if (index != 3)
				{
					var reservation = new StudentSeatReservation(Guid.NewGuid(), TestTenantId, item.Id, year, seed.Offering.StandardId,
						index == 1 ? divisionB.Id : divisionA.Id, index == 2 ? 2 : 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
					await GetRequiredService<IRepository<StudentSeatReservation, Guid>>().InsertAsync(reservation, true);
					item.RecordPlacement(reservation.DivisionId, reservation.Id, DateTime.UtcNow);
					item.RecordIdentifier(null, index == 0 ? 1 : index == 1 ? 5 : 3, DateTime.UtcNow);
				}
				if (index is 0 or 2) item.AssignOwner(ownerId);
				await SeedOnboardingWorklistRequirementsAsync(item, application, student);
				await _onboardingCaseRepository.UpdateAsync(item, true); return item;
			});
		}
		return new(seed.Offering, divisionA.Id, divisionB.Id, cases);
	}

	private async Task SeedOnboardingWorklistRequirementsAsync(StudentOnboardingCase item, AdmissionApplication application, Student student)
	{
		var snapshots = GetRequiredService<OnboardingRequirementSnapshotService>();
		await snapshots.CaptureAsync(item);
		var documentsReady = await snapshots.DocumentsReadyAsync(item, application);
		// Mirror AdmissionApplicationDecisionService.CreateDefaultStepsAsync for an Offering-derived case.
		for (var index = 0; index < StudentOnboardingStepKeys.Defaults.Length; index++)
		{
			var definition = StudentOnboardingStepKeys.Defaults[index];
			var required = definition.Key is not (StudentOnboardingStepKeys.Family or StudentOnboardingStepKeys.Communication or StudentOnboardingStepKeys.MedicalAndPreviousSchool);
			var step = new StudentOnboardingStep(Guid.NewGuid(), TestTenantId, item.Id, definition.Key, definition.Label,
				index + 1, required, required ? StudentOnboardingStepStatus.Pending : StudentOnboardingStepStatus.NotRequired);
			if (definition.Key == StudentOnboardingStepKeys.Identity)
			{
				if (AutomaticOnboardingReadinessRules.HasCompleteIdentity(student)) step.Complete(TestUserId, DateTime.UtcNow, "Core identity verified in the fixture.");
				else step.Block("Complete the Student name, date of birth, and gender.");
			}
			if (definition.Key == StudentOnboardingStepKeys.Documents && documentsReady)
				step.Complete(TestUserId, DateTime.UtcNow, "Applicable document requirements evaluated by the snapshot owner.");
			if (definition.Key == StudentOnboardingStepKeys.PlacementAndSeat && item.SeatReservationId.HasValue)
				step.Complete(TestUserId, DateTime.UtcNow, "Fixture placement has a live capacity reservation.");
			await _onboardingStepRepository.InsertAsync(step, true);
		}
	}

	private sealed record OnboardingWorklistFixture(AdmissionOffering Offering, Guid DivisionA, Guid DivisionB, StudentOnboardingCase[] Cases);
}
