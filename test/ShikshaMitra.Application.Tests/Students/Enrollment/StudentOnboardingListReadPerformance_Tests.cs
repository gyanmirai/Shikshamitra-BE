using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_list_batches_page_dependencies_without_loading_student_aggregate_graphs() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await WithUnitOfWorkAsync(CreateOnboardingWorklistFixtureAsync);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			// This measures stable reads; adoption and readiness writes have their own regression below.
			await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
			var single = await MeasureOnboardingListAsync(fixture.Offering.StandardId, 1);
			var page = await MeasureOnboardingListAsync(fixture.Offering.StandardId, 4);
			page.Items.ShouldBe(4);
			single.Items.ShouldBe(1);
			page.Total.ShouldBe(4);
			single.Total.ShouldBe(4);
			var evidence = $"Synthetic in-memory SQLite; page 1 reads={single.Reads}, student reads={single.StudentReads}, tracked students={single.TrackedStudents}; page 4 reads={page.Reads}, student reads={page.StudentReads}, tracked students={page.TrackedStudents}.";
			Console.WriteLine(evidence);
			// Retained credential/readiness owners may still issue per-case reads. Lookup and
			// student aggregate materialization must not return to the previous N+1 graph.
			page.Reads.ShouldBeLessThanOrEqualTo(single.Reads + 24, evidence);
			single.StudentReads.ShouldBeGreaterThan(0, evidence);
			page.StudentReads.ShouldBeLessThanOrEqualTo(single.StudentReads, evidence);
			page.TrackedStudents.ShouldBe(0, evidence);
			single.TrackedStudents.ShouldBe(0, evidence);
		});

	[Fact]
	public Task Onboarding_list_rejects_multiple_reserved_identifiers_instead_of_choosing_an_arbitrary_value() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await WithUnitOfWorkAsync(CreateOnboardingWorklistFixtureAsync);
			await WithUnitOfWorkAsync(async () =>
			{
				var item = fixture.Cases[2];
				foreach (var type in new[] { StudentIdentifierType.GrNumber, StudentIdentifierType.AdmissionNumber })
				{
					var series = new StudentIdentifierSeries(Guid.NewGuid(), TestTenantId, item.AcademicYearId, item.IntendedStandardId,
						type, $"PERF_{type}", $"Reserved {type}", "RES-", "", 4, 1, null, DateTime.UtcNow.AddDays(-1), null);
					await GetRequiredService<IRepository<StudentIdentifierSeries, Guid>>().InsertAsync(series, true);
					await GetRequiredService<IRepository<StudentIdentifierAllocation, Guid>>().InsertAsync(
						new(Guid.NewGuid(), TestTenantId, item.Id, item.StudentId, series.Id, type, 1, $"RES-{type}-0001", DateTime.UtcNow), true);
				}
			});
			await Should.ThrowAsync<InvalidOperationException>(() => GetRequiredService<IStudentOnboardingAppService>()
				.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 }));
		});

	[Fact]
	public Task Onboarding_list_projection_preserves_detail_fields_steps_identifiers_and_owner_redaction() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await WithUnitOfWorkAsync(CreateOnboardingWorklistFixtureAsync);
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(fixture.Cases[0].StudentId);
				student.AssignGrNumber("PERF-1000", null, "Existing permanent identifier");
				student.SetIdentifierLocked("gr", true);
				await _studentRepository.UpdateAsync(student, true);
				var legacyStudent = await _studentRepository.GetAsync(fixture.Cases[1].StudentId);
				legacyStudent.AssignGrNo(314);
				await _studentRepository.UpdateAsync(legacyStudent, true);
				var divisions = GetRequiredService<IRepository<Division, Guid>>();
				var division = await divisions.GetAsync(fixture.DivisionB);
				division.ExtraProperties[$"IdentifierGroupLocked:{fixture.Cases[1].AcademicYearId:N}:gr"] = true;
				division.ExtraProperties[$"IdentifierGroupLocked:{Guid.NewGuid():N}:roll"] = true;
				await divisions.UpdateAsync(division, true);
				var item = await _onboardingCaseRepository.GetAsync(fixture.Cases[2].Id);
				item.SetIdentifierLocked("roll", true);
				await _onboardingCaseRepository.UpdateAsync(item, true);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var page = await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
			page.TotalCount.ShouldBe(4);
			page.Items.Single(row => row.Id == fixture.Cases[0].Id).GrNumber.ShouldBe("PERF-1000");
			page.Items.Single(row => row.Id == fixture.Cases[0].Id).GrNumberLocked.ShouldBeTrue();
			page.Items.Single(row => row.Id == fixture.Cases[1].Id).GrNumber.ShouldBe("314");
			page.Items.Single(row => row.Id == fixture.Cases[1].Id).GrNumberLocked.ShouldBeTrue();
			page.Items.Single(row => row.Id == fixture.Cases[1].Id).RollNumberLocked.ShouldBeFalse();
			page.Items.Single(row => row.Id == fixture.Cases[2].Id).RollNumberLocked.ShouldBeTrue();
			page.Items.Single(row => row.Id == fixture.Cases[3].Id).DivisionName.ShouldBeNull();
			foreach (var row in page.Items)
			{
				row.StudentName.ShouldNotBeNullOrWhiteSpace();
				row.AcademicYearName.ShouldNotBeNullOrWhiteSpace();
				row.IntendedBoardName.ShouldNotBeNullOrWhiteSpace();
				row.IntendedMediumName.ShouldNotBeNullOrWhiteSpace();
				row.IntendedStandardName.ShouldNotBeNullOrWhiteSpace();
				row.AssignedToUserId.ShouldBeNull();
				row.AssignedToUserName.ShouldBeNull();
				row.Events.ShouldBeEmpty();
				row.CapturedStudentFields.ShouldBeEmpty();
				row.PortalAccountOwners.ShouldBeNull();
				row.Steps.ShouldNotBeEmpty();
				row.Steps.ShouldContain(step => step.CompletedByUserId == TestUserId && !string.IsNullOrWhiteSpace(step.CompletedByName));
				var detail = await service.GetAsync(row.Id);
				// These three properties are deliberately detail-only. Every other serialized
				// field, including nullable readiness and audited steps, is the same contract.
				detail.Events = [];
				detail.CapturedStudentFields = [];
				detail.PortalAccountOwners = null;
				JsonSerializer.Serialize(row).ShouldBe(JsonSerializer.Serialize(detail));
			}
		});

	[Fact]
	public Task Onboarding_list_projection_preserves_activated_handoffs_and_required_credential_readiness() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var row = (await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId }))
				.Items.Single(item => item.Id == fixture.Case.Id);
			row.StudentEnrollmentId.ShouldNotBeNull();
			row.StudentAccountRequired.ShouldBeTrue();
			row.GuardianAccountRequired.ShouldBeTrue();
			row.CredentialsPending.ShouldBeTrue();
			row.RequiredCredentialDeliveryConfirmed.ShouldBe(false);
			row.RequiredServiceHandoffs.Select(item => item.ServiceKey).ShouldContain("student-account");
			row.RequiredServiceHandoffs.Select(item => item.ServiceKey).ShouldContain("guardian-account");
			row.RequiredServiceHandoffs.ShouldNotContain(item => item.ServiceKey == "finance-first-payment" || item.ServiceKey == "service-consent");
			var detail = await service.GetAsync(row.Id);
			detail.Events = [];
			detail.CapturedStudentFields = [];
			detail.PortalAccountOwners = null;
			JsonSerializer.Serialize(row).ShouldBe(JsonSerializer.Serialize(detail));
		});

	[Fact]
	public Task Onboarding_list_projection_preserves_completed_status_and_nullable_credential_delivery() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false, emailEnabled: false);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var row = (await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId }))
				.Items.Single(item => item.Id == fixture.Case.Id);
			row.Status.ShouldBe(StudentOnboardingStatus.Completed);
			row.ProgressPercent.ShouldBe(100);
			row.PrimaryBlocker.ShouldBeNull();
			row.StudentEnrollmentId.ShouldNotBeNull();
			row.CredentialsPending.ShouldBeFalse();
			row.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
			var detail = await service.GetAsync(row.Id);
			detail.Events = [];
			detail.CapturedStudentFields = [];
			detail.PortalAccountOwners = null;
			JsonSerializer.Serialize(row).ShouldBe(JsonSerializer.Serialize(detail));
		});

	[Fact]
	public Task Onboarding_list_preserves_visible_completed_case_and_academic_label_visibility_after_year_soft_delete() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false, emailEnabled: false);
			fixture.Case.Status.ShouldBe(StudentOnboardingStatus.Completed);
			await WithUnitOfWorkAsync(async () =>
			{
				var years = GetRequiredService<IRepository<AcademicYear, Guid>>();
				var year = await years.GetAsync(fixture.Case.AcademicYearId);
				year.IsDeleted = true;
				await years.UpdateAsync(year, true);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var row = (await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId }))
				.Items.Single(item => item.Id == fixture.Case.Id);
			row.AcademicYearName.ShouldBe(string.Empty);
			row.IntendedBoardName.ShouldBe(string.Empty);
			row.IntendedMediumName.ShouldBe(string.Empty);
			row.IntendedStandardName.ShouldBe(fixture.Case.IntendedStandardName);
			row.IntendedStandardName.ShouldNotBeNullOrWhiteSpace();
			row.DivisionName.ShouldNotBeNullOrWhiteSpace();
			row.Status.ShouldBe(StudentOnboardingStatus.Completed);
			var detail = await service.GetAsync(row.Id);
			detail.Events = [];
			detail.CapturedStudentFields = [];
			detail.PortalAccountOwners = null;
			JsonSerializer.Serialize(row).ShouldBe(JsonSerializer.Serialize(detail));
		});

	[Fact]
	public Task Onboarding_list_projection_keeps_tenant_and_division_scope_before_batching() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await WithUnitOfWorkAsync(CreateOnboardingWorklistFixtureAsync);
			var staff = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(staff, $"projection-staff-{staff:N}");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staff,
				academicYearId: fixture.Cases[0].AcademicYearId, educationBoardId: fixture.Offering.BoardId,
				educationMediumId: fixture.Offering.MediumId, educationMediumStageId: fixture.Offering.StageId,
				standardId: fixture.Offering.StandardId, divisionId: fixture.DivisionA), true);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			using (_principalAccessor.Change(ProfilePrincipal(staff)))
			{
				var page = await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
				page.TotalCount.ShouldBe(2);
				page.Items.Select(row => row.Id).OrderBy(id => id).ShouldBe(new[] { fixture.Cases[0].Id, fixture.Cases[2].Id }.OrderBy(id => id));
				page.Items.ShouldAllBe(row => row.DivisionId == fixture.DivisionA && row.DivisionName == "Worklist A");
				(await service.GetListAsync(new() { DivisionId = fixture.DivisionB })).Items.ShouldBeEmpty();
				using (_currentTenant.Change(Guid.NewGuid()))
					(await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId })).TotalCount.ShouldBe(0);
			}
		});

	[Fact]
	public Task Onboarding_list_projection_adopts_requirements_only_for_page_and_does_not_repeat_audit() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await WithUnitOfWorkAsync(CreateOnboardingWorklistFixtureAsync);
			await WithUnitOfWorkAsync(async () =>
			{
				foreach (var seed in fixture.Cases)
				{
					var item = await _onboardingCaseRepository.GetAsync(seed.Id);
					item.ExtraProperties.Remove("ReadinessRulesSnapshot");
					item.ExtraProperties.Remove(AutomaticOnboardingReadinessRules.SnapshotVersionKey);
					await _onboardingCaseRepository.UpdateAsync(item, true);
					var identity = await _onboardingStepRepository.GetAsync(step => step.OnboardingCaseId == item.Id && step.Key == StudentOnboardingStepKeys.Identity);
					identity.Reset("Outdated identity evidence before automatic requirements adoption.");
					await _onboardingStepRepository.UpdateAsync(identity, true);
				}
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var input = new StudentOnboardingListInput { StandardId = fixture.Offering.StandardId, Sorting = "StudentName asc", MaxResultCount = 1 };
			var first = (await service.GetListAsync(input)).Items.Single();
			first.HasDocumentRequirementsSnapshot.ShouldBeTrue();
			first.Steps.Single(step => step.Key == StudentOnboardingStepKeys.Identity).Status.ShouldBe(StudentOnboardingStepStatus.Completed);
			await WithUnitOfWorkAsync(async () =>
			{
				var cases = await _onboardingCaseRepository.GetListAsync(item => item.IntendedStandardId == fixture.Offering.StandardId);
				cases.Single(item => item.Id == first.Id).ExtraProperties.ShouldContainKey("ReadinessRulesSnapshot");
				cases.Where(item => item.Id != first.Id).ShouldAllBe(item => !item.ExtraProperties.ContainsKey("ReadinessRulesSnapshot"));
				(await _onboardingStepRepository.GetAsync(step => step.OnboardingCaseId == first.Id && step.Key == StudentOnboardingStepKeys.Identity))
					.Status.ShouldBe(StudentOnboardingStepStatus.Completed);
				var events = await GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>()
					.GetListAsync(item => item.OnboardingCaseId == first.Id && item.EventType == "AutomaticRequirementsAdopted");
				events.Count.ShouldBe(1);
			});
			var repeated = (await service.GetListAsync(input)).Items.Single();
			repeated.ConcurrencyStamp.ShouldBe(first.ConcurrencyStamp);
			JsonSerializer.Serialize(repeated.Steps).ShouldBe(JsonSerializer.Serialize(first.Steps));
			await WithUnitOfWorkAsync(async () =>
				(await GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>()
					.GetListAsync(item => item.OnboardingCaseId == first.Id && item.EventType == "AutomaticRequirementsAdopted")).Count.ShouldBe(1));
		});

	private async Task<(int Items, long Total, int Reads, int StudentReads, int TrackedStudents)> MeasureOnboardingListAsync(Guid standardId, int pageSize)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var page = await GetRequiredService<IStudentOnboardingAppService>().GetListAsync(new()
			{
				StandardId = standardId, Sorting = "StudentName asc", MaxResultCount = pageSize
			});
			return (page.Items.Count, page.TotalCount, capture.ReadCount,
				capture.ReadCommands.Count(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)),
				db.ChangeTracker.Entries<Student>().Count());
		});
	}
}
