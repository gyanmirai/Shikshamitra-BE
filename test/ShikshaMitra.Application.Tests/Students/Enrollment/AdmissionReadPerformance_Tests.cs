using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Inquiry_class_names_do_not_hydrate_academic_hierarchy_aggregates(bool detail) =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			var inquiryId = await WithUnitOfWorkAsync(async () =>
			{
				var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"PERF-INQ-{Guid.NewGuid():N}",
					offering.AdmissionCycleId, offering.Id, "Class lookup", "9876543210", null, AdmissionInquirySource.Manual, null);
				await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
				return inquiry.Id;
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var service = GetRequiredService<IAdmissionInquiryAppService>();
				var result = detail ? await service.GetAsync(inquiryId) :
					(await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 10 })).Items.Single();
				result.StandardId.ShouldBe(offering.StandardId);
				result.StandardName.ShouldNotBeNullOrWhiteSpace();
				db.ChangeTracker.Entries<Standard>().ShouldBeEmpty(
					"Inquiry class names must not recursively hydrate the academic hierarchy.");
			});
		});

	[Fact]
	public Task Inquiry_capture_catalogue_batches_cycles_and_preserves_complete_ordered_offering_choices() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var first = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			var yearId = await WithUnitOfWorkAsync(async () => (await _cycleRepository.GetAsync(first.AdmissionCycleId)).AcademicYearId);
			// Compare the same readiness branches at both scales: inactive choices require batched identity reads.
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(first.Id);
				stored.Deactivate();
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var singleReads = await ReadPerformanceCatalogueAsync(yearId, 1, 1);
			await WithUnitOfWorkAsync(async () =>
			{
				for (var index = 0; index < 5; index++)
				{
					var cycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, $"Catalogue {index}", $"CAT_{index}",
						yearId, new DateTime(2026, 1, 1), new DateTime(2027, 12, 31));
					cycle.Publish(TestUserId, DateTime.UtcNow);
					await _cycleRepository.InsertAsync(cycle, true);
					var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, cycle.Id, null, first.BoardId,
						first.MediumId, first.StageId, first.StandardId, $"CAT_OFFERING_{index}", true, null, null,
						entryMode: index == 4 ? AdmissionEntryMode.DirectRegistration : AdmissionEntryMode.Inquiry);
					offering.ConfigureReferences(workflow.Id, first.InquiryFormDefinitionId, first.ApplicationFormDefinitionId);
					if (index == 3) offering.Deactivate();
					await _offeringRepository.InsertAsync(offering, true);
				}
			});
			var manyReads = await ReadPerformanceCatalogueAsync(yearId, 6, 5);
			Console.WriteLine($"SQLite inquiry catalogue: 1 cycle={singleReads} reads; 6 cycles={manyReads} reads.");
			manyReads.ShouldBeLessThanOrEqualTo(singleReads,
				"The complete catalogue must batch offering validation/reads across published cycles, preserving direct-entry exclusion and inactive choices.");
		});

	private Task<int> ReadPerformanceCatalogueAsync(Guid yearId, int periods, int offerings) => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
		var result = await GetRequiredService<IAdmissionInquiryCaptureAppService>().GetCatalogueAsync(yearId);
		result.Periods.Count.ShouldBe(periods);
		result.Offerings.Count.ShouldBe(offerings);
		result.Offerings.ShouldAllBe(item => item.EntryMode != AdmissionEntryMode.DirectRegistration);
		result.Offerings.ShouldContain(item => !item.IsActive);
		var periodOrder = result.Periods.Select((period, index) => new { period.Id, Index = index }).ToDictionary(item => item.Id, item => item.Index);
		result.Offerings.Select(item => periodOrder[item.AdmissionCycleId]).ShouldBeInOrder();
		return capture.ReadCount;
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Cycle_counts_do_not_hydrate_linked_application_and_offering_aggregates(bool impact) =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			await StartFixtureApplicationAsync((await CreateApplicantAsync("CycleCounts")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				var service = GetRequiredService<IAdmissionCycleAppService>();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				if (impact)
				{
					var result = await service.GetImpactAsync(cycle.Id);
					result.LinkedOfferingCount.ShouldBe(1);
					result.ActiveOfferingCount.ShouldBe(1);
					result.ApplicationCount.ShouldBe(1);
					result.DraftApplicationCount.ShouldBe(1);
					result.SubmittedApplicationCount.ShouldBe(0);
				}
				else
				{
					var result = await service.GetListAsync(new() { Search = cycle.Code, MaxResultCount = 10 });
					result.TotalCount.ShouldBe(1);
					result.Items[0].LinkedOfferingCount.ShouldBe(1);
					result.Items[0].ActiveOfferingCount.ShouldBe(1);
					result.Items[0].ApplicationCount.ShouldBe(1);
				}
				db.ChangeTracker.Entries<AdmissionApplication>().ShouldBeEmpty(
					"Cycle counts must not materialize every linked application's snapshots and audit data.");
				db.ChangeTracker.Entries<AdmissionOffering>().ShouldBeEmpty(
					"Cycle counts must aggregate the same scoped offerings without materializing them.");
			});
		});

	[Fact]
	public Task Class_capacity_aggregates_distinct_occupancy_and_projects_allocated_rooms() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			var room = await CreateCapacityRoomAsync(10);
			await GetRequiredService<IAdmissionClassCapacityAppService>().UpdateForOfferingAsync(offering.Id, new() { RoomIds = [room.Id] });
			var student = await CreateExistingStudentAsync(offering, "Capacity", "Reader");
			var linked = await CreateApplicantAsync("AlreadyEnrolled");
			var pending = await CreateApplicantAsync("PendingAccepted");
			await WithUnitOfWorkAsync(async () =>
			{
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
					student.Id, cycle.AcademicYearId, offering.StandardId, null), true);
				var stored = await _applicantRepository.GetAsync(linked.Id);
				stored.LinkStudent(student.Id);
				await _applicantRepository.UpdateAsync(stored, true);
				foreach (var applicantId in new[] { linked.Id, pending.Id })
				{
					var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"CAP-{Guid.NewGuid():N}",
						applicantId, null, cycle.Id, offering.Id, workflow.Id, offering.ApplicationFormDefinitionId!.Value);
					application.Submit(); application.MarkInReview(TestUserId, null); application.Approve(TestUserId, null);
					application.IssueOffer(DateTime.UtcNow.AddDays(1), TestUserId, null); application.AcceptOffer(TestUserId, null);
					await _applicationRepository.InsertAsync(application, true);
				}
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await GetRequiredService<AdmissionClassCapacityReadService>().ReadAsync(offering, cycle);
				result.OccupiedCount.ShouldBe(2, "An accepted applicant linked to the active student must not consume a second seat.");
				result.Capacity.ShouldBe(10);
				result.RemainingSeats.ShouldBe(8);
				result.Rooms.Single().RoomId.ShouldBe(room.Id);
				var occupancyReads = capture.ReadCommands.Where(sql => sql.Contains("StudentEnrollments", StringComparison.Ordinal) ||
					sql.Contains("AdmissionApplications", StringComparison.Ordinal)).ToList();
				occupancyReads.Count.ShouldBe(2);
				occupancyReads.ShouldAllBe(sql => sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase),
					"Occupancy must be aggregated by the database instead of transferring every student's identity.");
				db.ChangeTracker.Entries<Room>().ShouldBeEmpty();
			});
		});

	[Fact]
	public Task Assessment_scope_names_do_not_hydrate_academic_hierarchy_aggregates() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var result = await GetRequiredService<IAdmissionAssessmentAppService>().GetScopesAsync();
				result.ShouldContain(item => item.AdmissionOfferingId == fixture.Offering.Id &&
					item.StandardId == fixture.Offering.StandardId && !string.IsNullOrWhiteSpace(item.ClassName));
				db.ChangeTracker.Entries<Standard>().ShouldBeEmpty(
					"Assessment scope names must not recursively load Institute Setup hierarchy graphs.");
			});
		});

	[Fact]
	public Task Admission_room_choices_use_scalar_projections() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			var room = await CreateCapacityRoomAsync(10);
			var service = GetRequiredService<IAdmissionClassCapacityAppService>();
			await service.UpdateForOfferingAsync(offering.Id, new() { RoomIds = [room.Id] });
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var result = await service.GetRoomChoicesAsync(offering.Id, room.RoomName);
				var choice = result.Single(item => item.RoomId == room.Id);
				choice.RoomName.ShouldBe(room.RoomName);
				choice.Capacity.ShouldBe(10);
				choice.IsAllocatedToAnotherClass.ShouldBeFalse();
				db.ChangeTracker.Entries<Room>().ShouldBeEmpty(
					"Room choices must not materialize room graphs for name and capacity fields.");
			});
		});

	[Fact]
	public Task Workflow_version_history_child_queries_do_not_grow_with_version_count() =>
		RunCertificationScenarioAsync(async () =>
		{
			var single = await WithUnitOfWorkAsync(() => SeedReadPerformanceVersionsAsync("PERF_SINGLE", 1));
			var many = await WithUnitOfWorkAsync(() => SeedReadPerformanceVersionsAsync("PERF_MANY", 6));
			var singleReads = await ReadPerformanceVersionsAsync(single, 1);
			var manyReads = await ReadPerformanceVersionsAsync(many, 6);
			Console.WriteLine($"SQLite workflow history: 1 version={singleReads} reads; 6 versions={manyReads} reads.");
			manyReads.ShouldBeLessThanOrEqualTo(singleReads,
				"Reading additional versions must batch their stages and transitions, rather than make two child reads per version.");
		});

	[Fact]
	public Task Workflow_usage_analytics_reads_scalar_runtime_data_without_tracking_runtime_entities() =>
		RunCertificationScenarioAsync(async () =>
		{
			var workflow = await CreateAndPublishCurrentWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId:
				AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
			var applicant = await CreateApplicantAsync("ReadPerformance");
			await StartFixtureApplicationAsync(applicant.Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var result = await _workflowAppService.GetUsageAsync(workflow.Id);
				result.RuntimeAnalytics.StartedCount.ShouldBe(1);
				result.RuntimeAnalytics.ActiveCount.ShouldBe(1);
				result.RuntimeAnalytics.CompletedCount.ShouldBe(0);
				result.RuntimeAnalytics.Stages.Count.ShouldBe(1);
				result.RuntimeAnalytics.Stages[0].EnteredCount.ShouldBe(1);
				result.RuntimeAnalytics.Stages[0].ActiveCount.ShouldBe(1);
				db.ChangeTracker.Entries<AdmissionApplicationWorkflowInstance>().ShouldBeEmpty(
					"Usage analytics must not hydrate the complete runtime workflow aggregates.");
				db.ChangeTracker.Entries<AdmissionApplicationStageInstance>().ShouldBeEmpty(
					"Usage analytics must not hydrate completion reasons and unrelated runtime stage data.");
			});
		});

	private async Task<Guid> SeedReadPerformanceVersionsAsync(string code, int count)
	{
		var workflows = GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
		var stages = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
		var transitions = GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>();
		Guid? parent = null;
		for (var version = 1; version <= count; version++)
		{
			var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, $"History {version}", code, version, parent);
			await workflows.InsertAsync(workflow, true);
			var entry = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "ENTRY", $"Entry {version}",
				AdmissionWorkflowStageType.Form, 10, true, false, null, "{\"version\":" + version + "}", true);
			var decision = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "DECISION", $"Decision {version}",
				AdmissionWorkflowStageType.Approval, 20, true, false, null, null, false, true);
			// Insert in reverse order to exercise the public response's ordering contract.
			await stages.InsertAsync(decision, true);
			await stages.InsertAsync(entry, true);
			await transitions.InsertAsync(new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow.Id, entry.Id, decision.Id, 20, isDefault: true), true);
			parent = workflow.Id;
		}
		return parent!.Value;
	}

	private Task<int> ReadPerformanceVersionsAsync(Guid id, int count) => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
		var versions = await _workflowAppService.GetVersionHistoryAsync(id);
		versions.Select(version => version.Version).ShouldBe(Enumerable.Range(1, count).Reverse());
		versions.ShouldAllBe(version => version.Stages.Count == 2 && version.Transitions.Count == 1);
		foreach (var version in versions)
		{
			version.Stages.Select(stage => stage.Code).ShouldBe(new[] { "ENTRY", "DECISION" });
			version.Stages[0].Name.ShouldBe($"Entry {version.Version}");
			version.Transitions[0].FromStageId.ShouldBe(version.Stages[0].Id);
			version.Transitions[0].ToStageId.ShouldBe(version.Stages[1].Id);
		}
		return capture.ReadCount;
	});
}
