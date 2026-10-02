using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Record_ID_issuance_is_idempotent_snapshot_is_frozen_and_reprint_and_void_are_audited() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "IDOriginal", "Student"); await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentIdCardAppService>();
		var input = new IssueStudentIdCardsDto { StudentIds = [student.Id], IdempotencyKey = "id-stable-request" };
		var issued = (await service.IssueAsync(input)).Single();
		(await service.IssueAsync(input)).Single().Id.ShouldBe(issued.Id);
		await WithUnitOfWorkAsync(async () => { var current = await _studentRepository.GetAsync(student.Id); current.UpdateGeneralInformation("Changed", null, "Identity"); await _studentRepository.UpdateAsync(current, true); });
		(await service.GetHistoryAsync(student.Id)).Single().StudentSnapshot["StudentName"].ShouldBe("IDOriginal Student");
		(await service.ReprintAsync(issued.Id!.Value, new() { Reason = "Damaged original card" })).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
		(await service.GetHistoryAsync(student.Id)).Single().CardNumber.ShouldBe(issued.CardNumber);
		(await service.VoidAsync(issued.Id.Value, new() { Reason = "Incorrect original issue" })).Status.ShouldBe("Voided");
		(await Should.ThrowAsync<BusinessException>(() => service.GetPdfAsync(issued.Id.Value))).Code.ShouldBe("Students:Records:DocumentVoided");
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => service.GetHistoryAsync(student.Id));
	});

	[Fact]
	public Task Record_certificate_snapshots_idempotency_and_leaving_exit_are_enforced() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "CertificateOriginal", "Student"); await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentCertificateAppService>();
		var input = new CreateStudentCertificateDto { StudentId = student.Id, CertificateType = StudentCertificateType.Bonafide, IdempotencyKey = "certificate-stable-request" };
		var generated = await service.GenerateAsync(input); generated.CertificateId.ShouldNotBeNull();
		(await service.GenerateAsync(input)).CertificateId.ShouldBe(generated.CertificateId);
		await WithUnitOfWorkAsync(async () => { var current = await _studentRepository.GetAsync(student.Id); current.UpdateGeneralInformation("Changed", null, "Identity"); await _studentRepository.UpdateAsync(current, true); });
		(await service.GetAsync(generated.CertificateId!.Value)).Student.StudentName.ShouldBe("CertificateOriginal Student");
		(await Should.ThrowAsync<BusinessException>(() => service.DuplicateAsync(generated.CertificateId.Value))).Code.ShouldBe("Students:Records:DocumentReasonRequired");
		var duplicateInput = new StudentDocumentActionDto { Reason = "Additional certified copy", IdempotencyKey = "duplicate-stable-request" };
		var duplicate = await service.DuplicateAsync(generated.CertificateId.Value, duplicateInput);
		(await service.DuplicateAsync(generated.CertificateId.Value, duplicateInput)).CertificateId.ShouldBe(duplicate.CertificateId);
		duplicate.CertificateNo.ShouldNotBe(generated.CertificateNo); duplicate.Student.StudentName.ShouldBe(generated.Student.StudentName);
		(await service.ReprintAsync(generated.CertificateId.Value, new() { Reason = "Original copy was damaged" })).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
		(await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(new() { StudentId = student.Id, CertificateType = StudentCertificateType.Leaving, IdempotencyKey = "leaving-no-exit" })))
			.Code.ShouldBe("Students:Records:LeavingExitRequired");
		(await service.VoidAsync(generated.CertificateId.Value, new() { Reason = "Duplicate issued in error" })).Status.ShouldBe("Cancelled");
	});

	[Theory]
	[InlineData("ApprovedExit")]
	[InlineData("EffectiveExit")]
	public Task Record_certificate_leaving_requires_configured_stage_and_preserves_linked_evidence(string stage) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "LinkedLeaving", "Student"); await GrantCompletedRecordAsync(student);
		var policy = StudentRecordsPolicy.Starter(); policy.Documents.LeavingCertificateStage = stage; await PublishDirectoryPolicyAsync(policy);
		var exitId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var batch = new StudentRecordTransitionBatch(Guid.NewGuid(), TestTenantId, student.AcademicYearId!.Value, Guid.NewGuid().ToString("N"), "{}", "[]");
			await GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().InsertAsync(batch, true);
			var enrollment = await GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(e => e.TenantId == TestTenantId && e.StudentId == student.Id);
			await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().InsertAsync(new StudentRecordTransition(exitId, TestTenantId, batch.Id, student.Id, student.AcademicYearId.Value,
				enrollment.Id, "Withdraw", "Family relocation verified", AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), "{}", "{}", null, policy.ToJson(), "{}", TestUserId, 1), true);
		});
		var service = GetRequiredService<IStudentCertificateAppService>();
		var input = new CreateStudentCertificateDto { StudentId = student.Id, CertificateType = StudentCertificateType.Leaving, TransitionId = exitId, IdempotencyKey = Guid.NewGuid().ToString("N") };
		(await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(input))).Code.ShouldBe("Students:Records:LeavingExitNotReady");
		await WithUnitOfWorkAsync(async () => { var repository = GetRequiredService<IRepository<StudentRecordTransition, Guid>>(); var exit = await repository.GetAsync(exitId); exit.Approve(Guid.NewGuid()); await repository.UpdateAsync(exit, true); });
		if (stage == "EffectiveExit")
		{
			(await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(input))).Code.ShouldBe("Students:Records:LeavingExitNotReady");
			await WithUnitOfWorkAsync(async () => { var repository = GetRequiredService<IRepository<StudentRecordTransition, Guid>>(); var exit = await repository.GetAsync(exitId); exit.Apply(null, "{}", DateTime.UtcNow); await repository.UpdateAsync(exit, true); });
		}
		var certificate = await service.GenerateAsync(input);
		certificate.TransitionId.ShouldBe(exitId);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
		var frozen = (await service.GetAsync(certificate.CertificateId!.Value)).CertificateData["_ExitTransition"];
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordTransition, Guid>>(); var exit = await repository.GetAsync(exitId);
			if (exit.Status != "Applied") { exit.Apply(null, "{}", DateTime.UtcNow); await repository.UpdateAsync(exit, true); }
			var batch = new StudentRecordTransitionBatch(Guid.NewGuid(), TestTenantId, student.AcademicYearId!.Value, Guid.NewGuid().ToString("N"), "{}", "[]");
			await GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().InsertAsync(batch, true);
			var correction = new StudentRecordTransition(Guid.NewGuid(), TestTenantId, batch.Id, student.Id, exit.AcademicYearId, exit.SourceEnrollmentId!.Value,
				"Correction", "Withdraw entry corrected", exit.EffectiveDate, "{}", "{}", null, policy.ToJson(), "{}", TestUserId, 0, exit.Id);
			correction.Apply(null, "{}", DateTime.UtcNow); await repository.InsertAsync(correction, true);
		});
		(await service.GetAsync(certificate.CertificateId.Value)).CertificateData["_ExitTransition"].ShouldBe(frozen);
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		(await Should.ThrowAsync<BusinessException>(() => service.GenerateAsync(input))).Code.ShouldBe("Students:Records:LeavingExitInvalid");
	});

	[Theory]
	[InlineData("ApprovedExit")]
	[InlineData("EffectiveExit")]
	public Task Record_certificate_graduation_requires_completed_transition_and_preserves_evidence_after_correction(string stage) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Documents.LeavingCertificateStage = stage; policy.Changes["Correction"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); fixture.Input.Items[0].Outcome = "Graduate"; fixture.Input.Items[0].Reason = "Student completed the school programme";
		(await fixture.Service.PreviewAsync(fixture.Input)).CanSubmit.ShouldBeTrue();
		var transition = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); transition.Status.ShouldBe("Submitted");
		var certificates = GetRequiredService<IStudentCertificateAppService>();
		var request = new CreateStudentCertificateDto { StudentId = fixture.Student.Id, CertificateType = StudentCertificateType.Leaving, TransitionId = transition.Id, IdempotencyKey = "graduation-leaving" };
		(await Should.ThrowAsync<BusinessException>(() => certificates.GenerateAsync(request))).Code.ShouldBe("Students:Records:LeavingExitNotReady");
		var reviewerId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(reviewerId, $"graduation-review-{reviewerId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new global::ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewerId), true);
			var users = GetRequiredService<IdentityUserManager>(); (await users.AddToRoleAsync((await users.FindByIdAsync(reviewerId.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(ProfilePrincipal(reviewerId)))
			transition = await fixture.Service.ApproveAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Completed graduation verified" });
		transition.Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(e => e.TenantId == TestTenantId && e.StudentId == fixture.Student.Id))).IsActive.ShouldBeFalse();
		var certificate = await certificates.GenerateAsync(request); certificate.TransitionId.ShouldBe(transition.Id);
		var frozen = (await certificates.GetAsync(certificate.CertificateId!.Value)).CertificateData["_ExitTransition"];
		frozen.ShouldContain("Graduate"); frozen.ShouldContain("Applied");
		var blocked = await fixture.Service.CorrectAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, IdempotencyKey = "graduation-correction-held", Reason = "Graduation entry requires a reviewed correction", PreviewOnly = false });
		blocked.Ready.ShouldBeFalse(); blocked.TransitionId.ShouldBeNull(); blocked.Issues.ShouldContain(x => x.Code == "Students:Records:DownstreamDependency");
		(await certificates.VoidAsync(certificate.CertificateId.Value, new() { Reason = "Leaving Certificate voided before correcting graduation" })).Status.ShouldBe("Cancelled");
		(await certificates.GetHistoryAsync(fixture.Student.Id)).Single(x => x.CertificateId == certificate.CertificateId.Value).Status.ShouldBe("Cancelled");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordDocumentEvent, Guid>>().GetListAsync(e => e.TenantId == TestTenantId && e.DocumentId == certificate.CertificateId.Value && e.Action == "Void"))).Count.ShouldBe(1);
		var correction = await fixture.Service.CorrectAsync(transition.Id, new() { ConcurrencyStamp = transition.ConcurrencyStamp, IdempotencyKey = "graduation-correction", Reason = "Graduation entry corrected after records review", PreviewOnly = false });
		correction.Ready.ShouldBeTrue(); (await fixture.Service.GetAsync(correction.TransitionId!.Value)).Status.ShouldBe("Applied");
		(await certificates.GetAsync(certificate.CertificateId.Value)).CertificateData["_ExitTransition"].ShouldBe(frozen);
		request.IdempotencyKey = "corrected-graduation-leaving";
		(await Should.ThrowAsync<BusinessException>(() => certificates.GenerateAsync(request))).Code.ShouldBe("Students:Records:LeavingExitInvalid");
	});

	[Fact]
	public Task Record_certificate_future_approved_graduation_is_not_completed_for_leaving_issuance() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Documents.LeavingCertificateStage = "ApprovedExit"; policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy); fixture.Input.Items[0].Outcome = "Graduate";
		fixture.Input.Items[0].EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow).AddDays(2);
		var transition = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); transition.Status.ShouldBe("Scheduled");
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentCertificateAppService>().GenerateAsync(new()
		{
			StudentId = fixture.Student.Id, CertificateType = StudentCertificateType.Leaving, TransitionId = transition.Id, IdempotencyKey = "future-graduation-leaving"
		}))).Code.ShouldBe("Students:Records:LeavingExitNotReady");
	});

	[Theory]
	[InlineData("SuspendUntilReview")]
	[InlineData("KeepUntilReview")]
	public Task Record_guardian_review_uses_request_policy_and_preserves_sibling_identity_access(string mode) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "GuardianFirst", "Student");
		var sibling = await CreateExistingStudentAsync(offering, "GuardianSibling", "Student");
		await GrantCompletedRecordAsync(student); await GrantCompletedRecordAsync(sibling);
		var guardianId = Guid.NewGuid(); var email = $"guardian-{guardianId:N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var roles = GetRequiredService<IdentityRoleManager>(); if (!await roles.RoleExistsAsync("Parent")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>(); var user = new IdentityUser(guardianId, email, email, TestTenantId); user.SetEmailConfirmed(true);
			(await users.CreateAsync(user)).Succeeded.ShouldBeTrue(); (await users.AddToRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
			foreach (var id in new[] { student.Id, sibling.Id })
			{
				var familyRepo = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await familyRepo.FindAsync(id);
				family ??= await familyRepo.InsertAsync(new StudentFamilyDetail(id, TestTenantId), true);
				family.UpdateFatherDetails(new ParentGuardianDetails("Verified Father", null, null, email, "9000000001", null, null, null, null, null, null, null, null)); await familyRepo.UpdateAsync(family, true);
				await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, id, guardianId, "guardian", "father", TestUserId), true);
			}
		});
		var policy = StudentRecordsPolicy.Starter(); policy.GuardianAccess.OnApprovedChange = mode;
		var review = await WithUnitOfWorkAsync(() => GetRequiredService<StudentGuardianAccessReviewService>().CaptureAsync(student.Id, Guid.NewGuid(), policy.ToJson()));
		var service = GetRequiredService<IStudentGuardianAccessAppService>();
		(await service.GetLinksAsync(student.Id)).Count.ShouldBe(mode == "SuspendUntilReview" ? 0 : 1);
		(await service.GetLinksAsync(sibling.Id)).Single().UserId.ShouldBe(guardianId);
		var dto = (await service.GetReviewsAsync(student.Id)).Single();
		await service.ResolveAsync(review.Id, new() { ConcurrencyStamp = dto.ConcurrencyStamp, Reason = "Previous guardian access revoked after review", RevokeLinkIds = dto.PreviousLinks.Select(l => l.LinkId).ToList() });
		(await service.GetLinksAsync(student.Id)).ShouldBeEmpty(); (await service.GetLinksAsync(sibling.Id)).Single().UserId.ShouldBe(guardianId);
		(await GetRequiredService<IdentityUserManager>().FindByIdAsync(guardianId.ToString())).ShouldNotBeNull();
	});
}
