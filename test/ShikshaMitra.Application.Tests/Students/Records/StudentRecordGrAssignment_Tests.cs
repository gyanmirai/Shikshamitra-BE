using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Record_GR_manual_requires_reference_and_independent_approval_even_under_direct_correction_policy() =>
		RunCertificationScenarioAsync(async () =>
		{
			await EnsureTestIdentityUserAsync(TestUserId, "gr-requester");
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var student = await CreateExistingStudentAsync(offering, "Legacy", "Register");
			await GrantCompletedRecordAsync(student);
			var policy = StudentRecordsPolicy.Starter();
			policy.Changes["Correction"].Mode = "Direct";
			await PublishDirectoryPolicyAsync(policy);
			var service = GetRequiredService<IStudentRecordChangeAppService>();
			var outsideScope = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(outsideScope, $"gr-outside-scope-{outsideScope:N}");
			using (_principalAccessor.Change(ProfilePrincipal(outsideScope)))
				(await Should.ThrowAsync<BusinessException>(() => service.GetSnapshotAsync(student.Id, "gr-number")))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			var snapshot = await service.GetSnapshotAsync(student.Id, "gr-number");
			var input = GrRequest(snapshot, student.Id, "manual", "00071", null);
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(input))).Code
				.ShouldBe("Students:Records:GrRegisterReferenceRequired");
			var invalidReference = GrRequest(snapshot, student.Id, "manual", "00071", "Page\0entry 71");
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(invalidReference))).Code
				.ShouldBe("Students:Records:GrRegisterReferenceRequired");

			input = GrRequest(snapshot, student.Id, "manual", "00071", "General Register, page 12, entry 71");
			var request = await service.CreateAsync(input);
			request.Status.ShouldBe("Pending");
			request.RequiredApprovals.ShouldBe(1);
			(await service.CreateAsync(input)).Id.ShouldBe(request.Id);
			(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id))).GrNumber.ShouldBeNull();
			(await Should.ThrowAsync<BusinessException>(() => service.ApproveAsync(request.Id, GrApproval(request))))
				.Code.ShouldBe("Students:Records:ApproverRequired");
			var parallel = GrRequest(snapshot, student.Id, "manual", "72", "General Register, page 12");
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(parallel))).Code
				.ShouldBe("Students:Records:GrAssignmentPending");

			var reviewer = await CreateGrReviewerAsync();
			using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
				request = await service.ApproveAsync(request.Id, GrApproval(request));
			request.Status.ShouldBe("Applied");
			var saved = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id));
			saved.GrNumber.ShouldBe("00071");
			saved.GrNo.ShouldBe(71);
			saved.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
			(await service.CreateAsync(input)).Id.ShouldBe(request.Id);
			(await Should.ThrowAsync<BusinessException>(() => service.GetSnapshotAsync(student.Id, "gr-number")))
				.Code.ShouldBe("Students:Records:GrAlreadyAssigned");
		});

	[Fact]
	public Task Record_GR_auto_uses_published_preview_and_rejects_duplicate_legacy_numbers_and_stale_approval() =>
		RunCertificationScenarioAsync(async () =>
		{
			await EnsureTestIdentityUserAsync(TestUserId, "auto-gr-requester");
			var fixture = await CreateAutomaticRegressionFixtureAsync("RecordsGR");
			var student = await CreateExistingStudentAsync(fixture.Offering, "Legacy", "Automatic");
			await GrantCompletedRecordAsync(student);
			var other = await CreateExistingStudentAsync(fixture.Offering, "Different", "Student");
			var service = GetRequiredService<IStudentRecordChangeAppService>();
			var snapshot = await service.GetSnapshotAsync(student.Id, "gr-number");
			var preview = snapshot.Data.GetProperty("autoGrNumber").GetString();
			preview.ShouldNotBeNullOrWhiteSpace();
			(await service.GetSnapshotAsync(student.Id, "gr-number")).Data.GetProperty("autoGrNumber").GetString().ShouldBe(preview);
			await WithUnitOfWorkAsync(async () =>
			{
				var holder = await _studentRepository.GetAsync(other.Id);
				holder.AssignGrNo(24);
				await _studentRepository.UpdateAsync(holder, true);
			});
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(
				GrRequest(snapshot, student.Id, "manual", "00024", "General Register, page 6"))))
				.Code.ShouldBe(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);

			var request = await service.CreateAsync(GrRequest(snapshot, student.Id, "auto", preview!, null));
			request.Status.ShouldBe("Pending");
			var reviewer = await CreateGrReviewerAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var holder = await _studentRepository.GetAsync(other.Id);
				holder.AssignGrNumber(preview!, reason: "Competing allocation before approval");
				await _studentRepository.UpdateAsync(holder, true);
			});
			using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
				request = await service.ApproveAsync(request.Id, GrApproval(request));
			request.Status.ShouldBe("Conflict");
			request.ExceptionCode.ShouldBe(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
			(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id))).GrNumber.ShouldBeNull();

			snapshot = await service.GetSnapshotAsync(student.Id, "gr-number");
			var next = snapshot.Data.GetProperty("autoGrNumber").GetString();
			next.ShouldNotBeNullOrWhiteSpace();
			next.ShouldNotBe(preview);
			// The older combined onboarding command shares the same series. A newly occupied
			// cursor must not stall it or let it reserve the same GR number.
			var onboarding = await PrepareAutomaticRegressionCaseAsync(fixture, true, "gr-combined-skip", confirmFees: false);
			onboarding.GrNumber.ShouldBe(next);
			snapshot = await service.GetSnapshotAsync(student.Id, "gr-number");
			next = snapshot.Data.GetProperty("autoGrNumber").GetString();
			next.ShouldNotBeNullOrWhiteSpace();
			next.ShouldNotBe(onboarding.GrNumber);
			request = await service.CreateAsync(GrRequest(snapshot, student.Id, "auto", next!, null));
			using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
				request = await service.ApproveAsync(request.Id, GrApproval(request));
			request.Status.ShouldBe("Applied");
			(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id))).GrNumber.ShouldBe(next);
		});

	private static CreateStudentRecordChangeDto GrRequest(StudentRecordSectionSnapshotDto snapshot, Guid student, string mode, string number, string? reference) => new()
	{
		Section = "gr-number", StudentId = student, ExpectedSnapshot = snapshot.Snapshot,
		ClientKey = Guid.NewGuid().ToString("N"), Reason = "Verified missing GR on the active school record",
		Payload = JsonSerializer.SerializeToElement(new { mode, grNumber = number, registerReference = reference })
	};

	private static StudentRecordDecisionDto GrApproval(StudentRecordChangeDto request) => new()
	{
		ConcurrencyStamp = request.ConcurrencyStamp, Reason = "Reviewed the permanent register assignment"
	};

	private async Task<Guid> CreateGrReviewerAsync()
	{
		var reviewer = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(reviewer, $"gr-reviewer-{reviewer:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("admin"))
				(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		return reviewer;
	}
}
