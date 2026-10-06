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
using ShikshaMitra.Students.Portal;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
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
	[Theory][InlineData("Withdraw")][InlineData("Transfer")][InlineData("Graduate")]
	public Task Record_transition_exit_revokes_only_departing_child_and_preserves_parent_sibling(string outcome) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); fixture.Input.Items[0].Outcome = outcome;
		var parentId = Guid.NewGuid(); var selfId = Guid.NewGuid(); var siblingId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			var parent = new IdentityUser(parentId, "exit-parent", "exit-parent@example.test", TestTenantId); parent.SetEmailConfirmed(true);
			(await users.CreateAsync(parent)).Succeeded.ShouldBeTrue();
			var self = new IdentityUser(selfId, "exit-self", "exit-self@example.test", TestTenantId); self.SetEmailConfirmed(true);
			(await users.CreateAsync(self)).Succeeded.ShouldBeTrue();
			var source = await _studentRepository.GetAsync(fixture.Student.Id); source.LinkUser(selfId); await _studentRepository.UpdateAsync(source, true);
			var sibling = new Student(siblingId, TestTenantId, "Remaining", "", "Sibling", source.AcademicYearId!.Value,
				source.AcademicBoardId!.Value, source.AcademicMediumId, source.AcademicMediumStageId!.Value, source.StandardId, source.DivisionId,
				status: StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.InsertAsync(sibling, true);
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			var departingLinkId = Guid.NewGuid(); var siblingLinkId = Guid.NewGuid();
			await links.InsertAsync(new(departingLinkId, TestTenantId, source.Id, parentId, "guardian", "father", TestUserId), true);
			await links.InsertAsync(new(siblingLinkId, TestTenantId, siblingId, parentId, "guardian", "father", TestUserId), true);
			var emails = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			foreach (var state in new[] { "Pending", "Processing", "Failed" })
			{
				var request = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), source.Id,
					departingLinkId, parentId, "guardian", parent.Email!, TestUserId, DateTime.UtcNow);
				request.SetInitialPassword("synthetic-encrypted-retry-secret");
				if (state == "Processing") request.Claim(DateTime.UtcNow.AddSeconds(1));
				if (state == "Failed") request.Failed(DateTime.UtcNow);
				await emails.InsertAsync(request, true);
			}
			await emails.InsertAsync(new(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), siblingId, siblingLinkId,
				parentId, "guardian", parent.Email!, TestUserId, DateTime.UtcNow), true);
		});
		// Refresh the preview after the legacy identity association changed its source stamp.
		var preview = (await fixture.Service.PreviewAsync(fixture.Input)).Items.Single();
		fixture.Input.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp;
		fixture.Input.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); item.Status.ShouldBe("Applied");
		await WithUnitOfWorkAsync(async () =>
		{
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			(await links.CountAsync(x => x.StudentId == fixture.Student.Id)).ShouldBe(0);
			(await links.CountAsync(x => x.StudentId == siblingId && x.UserId == parentId)).ShouldBe(1);
			var emails = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			var cancelled = await emails.GetListAsync(x => x.StudentId == fixture.Student.Id);
			cancelled.Count.ShouldBe(3);
			cancelled.ShouldAllBe(x => x.Status == "Cancelled" && x.EncryptedInitialPassword == null && x.ClaimedAt == null);
			(await emails.GetAsync(x => x.StudentId == siblingId)).Status.ShouldBe("Pending");
			using (GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
			{
				var history = await links.GetListAsync(x => x.StudentId == fixture.Student.Id);
				history.Count.ShouldBe(2); history.ShouldAllBe(x => x.IsDeleted);
			}
			(await GetRequiredService<IdentityUserManager>().GetByIdAsync(parentId)).IsActive.ShouldBeTrue();
		});
		using (_principalAccessor.Change(EligibilityPrincipal(parentId, "exit-parent@example.test", "Parent")))
			(await WithUnitOfWorkAsync(() => GetRequiredService<StudentPortalAccessService>().GetMyStudentsAsync())).Single().StudentId.ShouldBe(siblingId);
		using (_principalAccessor.Change(EligibilityPrincipal(selfId, "exit-self@example.test", "Student")))
			(await WithUnitOfWorkAsync(() => GetRequiredService<StudentPortalAccessService>().GetMyStudentsAsync())).ShouldBeEmpty();
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
	[Fact]
	public Task Record_exit_correction_restores_exact_verified_portal_link() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Changes["Correction"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); var ownerId = Guid.NewGuid(); var linkId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>(); var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Student")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", TestTenantId))).Succeeded.ShouldBeTrue();
			var user = new IdentityUser(ownerId, "corrected-exit", "corrected-exit@example.test", TestTenantId); user.SetEmailConfirmed(true);
			(await users.CreateAsync(user)).Succeeded.ShouldBeTrue(); (await users.AddToRoleAsync(user, "Student")).Succeeded.ShouldBeTrue();
			var contactRepo = GetRequiredService<IRepository<StudentContactDetail, Guid>>();
			var contact = await contactRepo.FindAsync(fixture.Student.Id);
			if (contact == null) await contactRepo.InsertAsync(new(fixture.Student.Id, TestTenantId, "9988776600", email: user.Email), true);
			else { contact.UpdateContactInfo("9988776600", null, user.Email, null, null, null, null, null, null, null); await contactRepo.UpdateAsync(contact, true); }
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(linkId, TestTenantId, fixture.Student.Id, ownerId, "student", "self", TestUserId), true);
		});
		var item = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); item.Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(x => x.StudentId == fixture.Student.Id))).ShouldBe(0);
		var correction = await fixture.Service.CorrectAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "correct-portal-exit", Reason = "Exit entered for wrong date", PreviewOnly = false });
		(await fixture.Service.GetAsync(correction.TransitionId!.Value)).Status.ShouldBe("Applied");
		var restored = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetAsync(linkId));
		restored.UserId.ShouldBe(ownerId); restored.IsDeleted.ShouldBeFalse();
		using (_principalAccessor.Change(EligibilityPrincipal(ownerId, "corrected-exit@example.test", "Student")))
			(await WithUnitOfWorkAsync(() => GetRequiredService<StudentPortalAccessService>().GetMyStudentsAsync())).Single().StudentId.ShouldBe(fixture.Student.Id);
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
