using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.Divisions;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private Task SetRecordsRollOwnerAsync(Guid year, string mode) => GetRequiredService<IStudentOnboardingAppService>().SaveSettingsAsync(new()
	{
		AcademicYearId = year, ConfigurationJson = JsonSerializer.Serialize(new
		{
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
			rollNumber = new { assignmentMode = mode, configurations = new[] { new { id = "records-roll", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all", startingNumber = 40, numberingScope = "class-division", arrangeBy = "studentName", arrangeType = "ascending", preserveExisting = true, midYearHandling = "next-available", requireApproval = true } } }
		})
	});
	private static StudentRecordTransitionInput RollCommand(Guid student, Guid year, Guid standard, Guid division, string? mode, int? roll) => new()
	{
		StudentId = student, Outcome = "Promote", TargetAcademicYearId = year, TargetStandardId = standard, TargetDivisionId = division,
		RollAssignmentMode = mode, RollNumber = roll, Reason = "Reviewed annual class placement", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow)
	};
	private static void AcceptRollPreview(StudentRecordTransitionBatchInput input, StudentRecordTransitionPreviewDto preview)
	{
		foreach (var command in input.Items)
		{
			var row = preview.Items.Single(x => x.StudentId == command.StudentId); row.Ready.ShouldBeTrue();
			command.StudentConcurrencyStamp = row.StudentConcurrencyStamp; command.EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
			command.RollNumber = row.ProposedRollNumber;
		}
	}
	[Fact] public Task Record_roll_automatic_cohort_uses_owner_order_and_distinct_frozen_reservations() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = "DirectEnrollment";
		var fixture = await TransitionFixtureAsync(policy); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		await SetRecordsRollOwnerAsync(target.Year, "automatic");
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var second = await CreateExistingStudentAsync(offering, "Aaron", "Record"); await GrantCompletedRecordAsync(second);
		second.AcademicYearId.ShouldBe(fixture.Input.AcademicYearId);
		fixture.Input.Items = [RollCommand(fixture.Student.Id, target.Year, target.Standard, target.Division, "automatic", null), RollCommand(second.Id, target.Year, target.Standard, target.Division, "automatic", null)];
		var preview = await fixture.Service.PreviewAsync(fixture.Input); preview.ReadyCount.ShouldBe(2);
		preview.Items.Single(x => x.StudentId == second.Id).ProposedRollNumber.ShouldBe(40);
		preview.Items.Single(x => x.StudentId == fixture.Student.Id).ProposedRollNumber.ShouldBe(41);
		preview.Items.ShouldAllBe(x => x.RollAssignmentMode == "automatic" && !x.OnboardingRollApprovalRequired);
		AcceptRollPreview(fixture.Input, preview);
		var result = await fixture.Service.SubmitBatchAsync(fixture.Input); result.Items.ShouldAllBe(x => x.Status == "Applied");
		result.Items.Select(x => x.RollNumber).Distinct().Count().ShouldBe(2);
		(await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(result.Items.Select(x => x.Id).OrderBy(x => x));
	});
	[Fact] public Task Record_roll_stale_automatic_proposal_requires_refresh_but_committed_replay_is_preserved() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = "DirectEnrollment";
		var fixture = await TransitionFixtureAsync(policy); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId); await SetRecordsRollOwnerAsync(target.Year, "both");
		fixture.Input.Items = [RollCommand(fixture.Student.Id, target.Year, target.Standard, target.Division, "automatic", null)];
		AcceptRollPreview(fixture.Input, await fixture.Service.PreviewAsync(fixture.Input)); fixture.Input.Items.Single().RollNumber.ShouldBe(40);
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var other = await CreateExistingStudentAsync(offering, "Other", "Student"); await GrantCompletedRecordAsync(other);
		var competing = new StudentRecordTransitionBatchInput { AcademicYearId = fixture.Input.AcademicYearId, IdempotencyKey = "other-roll", Items = [RollCommand(other.Id, target.Year, target.Standard, target.Division, "manual", 40)] };
		AcceptRollPreview(competing, await fixture.Service.PreviewAsync(competing)); (await fixture.Service.SubmitBatchAsync(competing)).Items.Single().Status.ShouldBe("Applied");
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.SubmitBatchAsync(fixture.Input))).Code.ShouldBe("Students:Records:RollProposalChanged");
		AcceptRollPreview(fixture.Input, await fixture.Service.PreviewAsync(fixture.Input)); fixture.Input.Items.Single().RollNumber.ShouldBe(41);
		var result = await fixture.Service.SubmitBatchAsync(fixture.Input); result.Items.Single().Status.ShouldBe("Applied");
		(await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single().Id.ShouldBe(result.Items.Single().Id);
	});
	[Theory][InlineData("automatic", "manual")][InlineData("manual", "automatic")][InlineData("both", null)]
	public Task Record_roll_school_mode_cannot_be_bypassed(string schoolMode, string? requested) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await TransitionFixtureAsync(); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId); await SetRecordsRollOwnerAsync(target.Year, schoolMode);
		fixture.Input.Items = [RollCommand(fixture.Student.Id, target.Year, target.Standard, target.Division, requested, 40)];
		await Should.ThrowAsync<BusinessException>(() => fixture.Service.PreviewAsync(fixture.Input));
	});
	[Fact] public Task Record_roll_legacy_manual_missing_number_is_blocked_before_immutable_renewal() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await TransitionFixtureAsync(); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		fixture.Input.Items = [RollCommand(fixture.Student.Id, target.Year, target.Standard, target.Division, null, null)];
		var preview = await fixture.Service.PreviewAsync(fixture.Input); preview.CanSubmit.ShouldBeFalse(); preview.Items.Single().RollAssignmentMode.ShouldBe("manual");
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.SubmitBatchAsync(fixture.Input))).Code.ShouldBe("Students:Records:BatchHeld");
		(await fixture.Service.GetRenewalsAsync(new() { StudentId = fixture.Student.Id })).TotalCount.ShouldBe(0);
	});
	[Theory][InlineData("class-division")][InlineData("class")]
	public Task Record_roll_cohort_orders_by_its_captured_scope_and_rejects_conflicting_class_rules(string numberingScope) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = "DirectEnrollment";
		var fixture = await TransitionFixtureAsync(policy); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		var otherDivision = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, "B", target.Standard, 30, "Target B"), true));
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var amy = await CreateExistingStudentAsync(offering, "Amy", "Record"); await GrantCompletedRecordAsync(amy);
		var ben = await CreateExistingStudentAsync(offering, "Ben", "Record"); await GrantCompletedRecordAsync(ben);
		var zoe = await CreateExistingStudentAsync(offering, "Zoe", "Record"); await GrantCompletedRecordAsync(zoe);
		await GetRequiredService<IStudentOnboardingAppService>().SaveSettingsAsync(new()
		{
			AcademicYearId = target.Year, ConfigurationJson = JsonSerializer.Serialize(new
			{
				readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
				rollNumber = new { assignmentMode = "automatic", configurations = new object[] {
					new { id = "base", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all", startingNumber = 10, numberingScope, arrangeBy = "studentName", arrangeType = "ascending" },
					new { id = "division-b", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "selected", divisionIds = new[] { otherDivision.Id }, startingNumber = 100, numberingScope, arrangeBy = "studentName", arrangeType = "descending" }
				} }
			})
		});
		fixture.Input.Items = [RollCommand(fixture.Student.Id, target.Year, target.Standard, target.Division, "automatic", null), RollCommand(ben.Id, target.Year, target.Standard, otherDivision.Id, "automatic", null), RollCommand(amy.Id, target.Year, target.Standard, target.Division, "automatic", null), RollCommand(zoe.Id, target.Year, target.Standard, otherDivision.Id, "automatic", null)];
		if (numberingScope == "class")
		{
			await Should.ThrowAsync<BusinessException>(() => fixture.Service.PreviewAsync(fixture.Input));
			return;
		}
		var preview = await fixture.Service.PreviewAsync(fixture.Input); preview.ReadyCount.ShouldBe(4);
		preview.Items.Single(x => x.StudentId == amy.Id).ProposedRollNumber.ShouldBe(10);
		preview.Items.Single(x => x.StudentId == fixture.Student.Id).ProposedRollNumber.ShouldBe(11);
		preview.Items.Single(x => x.StudentId == zoe.Id).ProposedRollNumber.ShouldBe(100);
		preview.Items.Single(x => x.StudentId == ben.Id).ProposedRollNumber.ShouldBe(101);
	});
}
