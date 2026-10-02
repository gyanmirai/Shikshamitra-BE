using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(IStudentRecordTransitionAppService Service, Student Student, StudentRecordTransitionBatchInput Input)> TransitionFixtureAsync(StudentRecordsPolicy? policy = null)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			if (await users.FindByIdAsync(TestUserId.ToString()) == null) (await users.CreateAsync(new IdentityUser(TestUserId, "records-admin", "records-admin@example.test", TestTenantId))).Succeeded.ShouldBeTrue();
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("admin")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
			var user = (await users.FindByIdAsync(TestUserId.ToString()))!;
			if (!await users.IsInRoleAsync(user, "admin")) (await users.AddToRoleAsync(user, "admin")).Succeeded.ShouldBeTrue();
		});
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "Transition", "Student"); await GrantCompletedRecordAsync(student);
		policy ??= StudentRecordsPolicy.Starter();
		await PublishDirectoryPolicyAsync(policy);
		var service = GetRequiredService<IStudentRecordTransitionAppService>();
		var input = new StudentRecordTransitionBatchInput { AcademicYearId = student.AcademicYearId!.Value, IdempotencyKey = "transition-" + Guid.NewGuid().ToString("N"),
			Items = [new() { StudentId = student.Id, Outcome = "Withdraw", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), Reason = "Family relocated to another city" }] };
		var preview = await service.PreviewAsync(input); var row = preview.Items.Single();
		input.Items[0].StudentConcurrencyStamp = row.StudentConcurrencyStamp; input.Items[0].EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
		return (service, student, input);
	}
	[Fact] public Task Record_transition_exit_is_atomic_idempotent_and_preserves_permanent_profile() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy);
		var result = await fixture.Service.SubmitBatchAsync(fixture.Input); var item = result.Items.Single(); item.Status.ShouldBe("Applied");
		(await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single().Id.ShouldBe(item.Id);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id))).IsActive.ShouldBeFalse();
		(await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(fixture.Student.Id)).RecordsAccess.ShouldBe("Full");
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => fixture.Service.GetAsync(item.Id));
	});
	[Fact] public Task Record_transition_stale_preview_and_unavailable_finance_are_blocked() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); fixture.Input.Items[0].StudentConcurrencyStamp = "stale";
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.SubmitBatchAsync(fixture.Input))).Code.ShouldBe("Students:Records:BatchHeld");
		policy.Transitions.OutcomeChecks["Withdraw"].RequireFinanceClearance = true;
		var source = await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordTransitionReadinessService>().PreviewAsync(fixture.Input.AcademicYearId, fixture.Input.Items[0], policy, false));
		source.Ready.ShouldBeFalse(); source.Issues.ShouldContain(x => x.Code == "Students:Records:FinanceClearanceRequired");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
	});
	[Fact] public Task Record_transition_pending_approval_retains_policy_and_denies_requester() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await TransitionFixtureAsync();
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); item.Status.ShouldBe("Submitted"); item.CanApprove.ShouldBeFalse();
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => fixture.Service.ApproveAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, Remarks = "Requester cannot approve" }));
		var stored = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(item.Id));
		StudentRecordsPolicy.FromJson(stored.PolicyJson).Changes["Status"].Mode.ShouldBe("Approval");
		stored.ApprovalStageCount.ShouldBe(1); stored.CurrentApprovalStage.ShouldBe(0);
	});
	[Fact] public Task Record_transition_future_exit_keeps_roster_until_effective_date() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); fixture.Input.Items[0].EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow).AddDays(2);
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); item.Status.ShouldBe("Scheduled");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id))).IsActive.ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
	});
	[Fact] public Task Record_transition_privacy_masks_names_reasons_and_history() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		var fixture = await TransitionFixtureAsync(policy);
		(await fixture.Service.PreviewAsync(fixture.Input)).Items.Single().StudentName.ShouldBeNull();
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single();
		item.StudentName.ShouldBeNull(); item.Reason.ShouldBeEmpty(); item.History.ShouldAllBe(x => string.IsNullOrEmpty(x.Remarks));
		(await fixture.Service.GetAsync(item.Id)).StudentName.ShouldBeNull();
	});
	[Fact] public Task Record_transition_return_reuses_profile_with_governed_short_renewal_and_idempotent_link() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); await fixture.Service.SubmitBatchAsync(fixture.Input);
		var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		var input = new StudentRecordReturnInput { StudentId = fixture.Student.Id, TargetAcademicYearId = target.Year, TargetStandardId = target.Standard,
			TargetDivisionId = target.Division, RollNumber = 8, Route = "ReuseProfile", Reason = "Family returned to school", IdempotencyKey = "return-existing-profile" };
		var result = await fixture.Service.ReturnAsync(input); result.Status.ShouldBe("AwaitingRenewal"); result.RenewalCaseId.ShouldNotBeNull();
		(await fixture.Service.ReturnAsync(input)).TransitionId.ShouldBe(result.TransitionId);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
		(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync(x => x.Id == fixture.Student.Id))).ShouldBe(1);
		var renewal = await fixture.Service.GetRenewalAsync(result.RenewalCaseId!.Value); renewal.Status.ShouldBe("PendingChecklist");
		(await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(fixture.Student.Id)).RecordsAccess.ShouldBe("Full");
	});
	[Fact] public Task Record_transition_correction_preserves_original_and_restores_exit_without_financial_mutation() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Changes["Correction"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single();
		var correction = await fixture.Service.CorrectAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "correction-test", Reason = "Family cancelled relocation", PreviewOnly = false });
		correction.Ready.ShouldBeTrue(); correction.TransitionId.ShouldNotBeNull();
		(await fixture.Service.GetAsync(correction.TransitionId!.Value)).Status.ShouldBe("Applied");
		(await fixture.Service.GetAsync(item.Id)).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
	});
	private async Task<(Guid Year, Guid Standard, Guid Division)> NextRecordYearAsync(Guid sourceYearId)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var source = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(sourceYearId);
			var year = new AcademicYear(Guid.NewGuid(), TestTenantId, "Next records year", source.StartDate.AddYears(1), source.EndDate.AddYears(1));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var board = new EducationBoard(Guid.NewGuid(), TestTenantId, EducationBoardType.CBSE, year.Id);
			await _boardRepository.InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), TestTenantId, "Next English", board.Id); await _mediumRepository.InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), TestTenantId, "Next Primary", null, medium.Id); await _stageRepository.InsertAsync(stage, true);
			var standard = new Standard(Guid.NewGuid(), TestTenantId, "Class 2", 2, stage.Id, null); await _standardRepository.InsertAsync(standard, true);
			var division = new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "Next A"); await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			return (year.Id, standard.Id, division.Id);
		});
	}
	[Theory][InlineData("DirectEnrollment")][InlineData("ShortChecklist")]
	public Task Record_transition_annual_modes_reuse_identity_and_renewal_keeps_profile_visible(string mode) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = mode;
		var fixture = await TransitionFixtureAsync(policy); var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		var command = fixture.Input.Items[0]; command.Outcome = "Promote"; command.TargetAcademicYearId = target.Year; command.TargetStandardId = target.Standard; command.TargetDivisionId = target.Division; command.RollNumber = 4;
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); item.Status.ShouldBe(mode == "DirectEnrollment" ? "Applied" : "AwaitingRenewal");
		(await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(fixture.Student.Id)).RecordsAccess.ShouldBe("Full");
		(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync(x => x.Id == fixture.Student.Id))).ShouldBe(1);
		if (mode == "DirectEnrollment") (await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id))).ShouldBe(2);
		else
		{
			item.RenewalCaseId.ShouldNotBeNull();
			var renewal = await fixture.Service.GetRenewalAsync(item.RenewalCaseId!.Value);
			renewal.Status.ShouldBe("PendingChecklist");
			renewal.GuardianReviewRequired.ShouldBeTrue(); renewal.GuardianReviewConfirmed.ShouldBeFalse();
			renewal.Issues.ShouldContain(x => x.Code == "Students:Records:RenewalChecklistRequired");
			(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id,
				new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Attempt before owner checks" }))).Code.ShouldBe("Students:Records:RenewalChecklistRequired");
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).AcademicYearId.ShouldBe(fixture.Input.AcademicYearId);
		}
	});
}
