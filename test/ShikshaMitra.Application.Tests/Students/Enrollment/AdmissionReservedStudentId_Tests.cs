using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Reserved_student_identity_is_created_only_at_finalization_after_the_admission_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var studentCountBeforeAdmission = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var fixture = await CreateCertificationFixtureAsync("ReservedOffer", acceptOffer: false);
			fixture.Application.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			await AssertReservationUnusedAsync(fixture);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCountBeforeAdmission);

			var accepted = fixture.Application;
			await AssertReservationUnusedAsync(fixture with { Application = accepted });
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCountBeforeAdmission);

			var finalized = await FinalizeAsync(accepted, true, "reserved-offer-finalized");
			finalized.StudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCountBeforeAdmission + 1);
		});

	[Fact]
	public Task Retired_offer_decline_cannot_change_an_approved_reserved_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedDecline", acceptOffer: false);
			var error = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.DeclineOfferAsync(fixture.Application.Id,
				Decision(fixture.Application, "Guardian chose another school")));
			error.Code.ShouldBe(AdmissionOfferStageErrorCodes.InvalidState);
			(await _applicationAppService.GetAsync(fixture.Application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Approved);
			await AssertReservationUnusedAsync(fixture);
		});

	[Fact]
	public Task Rejected_application_keeps_its_unused_reserved_student_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedRejected", completeWorkflow: false);
			await AssertReservationUnusedAsync(fixture);
			await CompleteCertificationWorkflowAsync(fixture.Application.Id, stopBeforeApproval: true);
			var current = await _applicationAppService.GetAsync(fixture.Application.Id);
			var rejected = await _applicationAppService.RejectAsync(current.Id,
				Decision(current, "Applicant does not meet admission criteria"));
			rejected.Status.ShouldBe(AdmissionApplicationStatus.Rejected);
			var error = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(rejected, true, "reserved-rejected"));
			error.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			await AssertReservationUnusedAsync(fixture with { Application = rejected });
		});



	[Fact]
	public Task Explicit_existing_match_takes_precedence_and_does_not_consume_reserved_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedMatch");
			var existing = await CreateExistingStudentAsync(fixture.Offering, "ReservedMatch", "Patel");
			var studentsBefore = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var result = await _applicationAppService.FinalizeAsync(fixture.Application.Id,
				new FinalizeAdmissionApplicationDto
				{
					ConcurrencyStamp = fixture.Application.ConcurrencyStamp,
					IdempotencyKey = "reserved-explicit-match",
					ExistingStudentId = existing.Id,
					ConfirmGuardianContactVerified = true
				});

			result.StudentId.ShouldBe(existing.Id);
			result.ReusedExistingStudent.ShouldBeTrue();
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentsBefore);
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(fixture.Applicant.ReservedStudentId)))
				.ShouldBeNull();
			var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(fixture.Applicant.Id));
			applicant.ExistingStudentId.ShouldBe(existing.Id);
			applicant.ReservedStudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
		});

	[Fact]
	public Task Reserved_student_identity_collision_in_another_tenant_is_rejected_without_exposing_or_linking_student() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedOtherTenant");
			var otherTenantId = Guid.NewGuid();
			using (_currentTenant.Change(otherTenantId))
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
					var stage = await _stageRepository.GetAsync(stageId);
					var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
					var board = await _boardRepository.GetAsync(medium.EducationBoardId);
					var collision = Student.CreateDirectRegistration(
						fixture.Applicant.ReservedStudentId, otherTenantId, "Private", null, "OtherTenant",
						board.AcademicYearId, board.Id, medium.Id, stage.Id, null, null,
						new DateTimeOffset(CertificationBirthDate, TimeSpan.Zero), GenderEnum.Male, "9876543210");
					await _studentRepository.InsertAsync(collision, autoSave: true);
				});
			}

			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.PossibleMatches.ShouldBeEmpty();
			var error = await FinalizeCollisionInTransactionAsync(fixture.Application, "reserved-other-tenant-collision");
			error.Code.ShouldBe(AdmissionApplicantErrorCodes.ReservedStudentIdConflict);
			error.Data.Contains("TenantId").ShouldBeFalse();
			error.Data.Contains("StudentId").ShouldBeFalse();
			await AssertReservationUnusedAsync(fixture);
			using (_currentTenant.Change(otherTenantId))
			{
				var untouched = await WithUnitOfWorkAsync(() =>
					_studentRepository.GetAsync(fixture.Applicant.ReservedStudentId));
				untouched.FirstName.ShouldBe("Private");
				untouched.TenantId.ShouldBe(otherTenantId);
			}
		});

	[Fact]
	public Task Stale_applicant_loaded_before_another_finalization_cannot_create_a_second_student() =>
		RunCertificationScenarioAsync(async () =>
		{
			var first = await CreateCertificationFixtureAsync("ReservedInterleaved");
			var second = await CreateCertificationFixtureAsync("ReservedInterleaved", existingApplicant: first.Applicant);
			var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
			{
				var staleApplicant = await _applicantRepository.GetAsync(first.Applicant.Id);
				staleApplicant.ExistingStudentId.ShouldBeNull();
				using (var competing = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
				{
					var finalized = await FinalizeAsync(first.Application, true, "reserved-competing-winner");
					finalized.StudentId.ShouldBe(first.Applicant.ReservedStudentId);
					await competing.CompleteAsync();
				}

				// The first unit of work still tracks the applicant snapshot from before the other commit.
				staleApplicant.ExistingStudentId.ShouldBeNull();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await using var transaction = await db.Database.BeginTransactionAsync();
				try { await FinalizeAsync(second.Application, true, "reserved-competing-stale"); }
				finally { await transaction.RollbackAsync(); }
			}));
			error.Code.ShouldBe(AdmissionApplicantErrorCodes.ReservedStudentIdConflict);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync(
				item => item.Id == first.Applicant.ReservedStudentId))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(
				item => item.ApplicantId == first.Applicant.Id))).ShouldBe(1);
			var unchanged = await _applicationAppService.GetAsync(second.Application.Id);
			unchanged.StudentId.ShouldBeNull();
			unchanged.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Reserved_student_identity_collision_is_rejected_without_implicit_linking(bool softDeleted) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedCollision");
			var collision = await CreateExistingStudentAsync(fixture.Offering, "Different", "Identity",
				fixture.Applicant.ReservedStudentId);
			if (softDeleted)
				await WithUnitOfWorkAsync(() => _studentRepository.DeleteAsync(collision.Id, autoSave: true));
			var studentsBefore = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());

			var error = await FinalizeCollisionInTransactionAsync(fixture.Application, "reserved-collision");
			error.Code.ShouldBe(AdmissionApplicantErrorCodes.ReservedStudentIdConflict);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentsBefore);
			var storedApplication = await _applicationAppService.GetAsync(fixture.Application.Id);
			storedApplication.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			storedApplication.StudentId.ShouldBeNull();
			storedApplication.OnboardingCaseId.ShouldBeNull();
			var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(fixture.Applicant.Id));
			applicant.ExistingStudentId.ShouldBeNull();
			applicant.ReservedStudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(
				item => item.ApplicationId == fixture.Application.Id))).ShouldBe(0);
		});

	[Fact]
	public Task Later_application_for_the_same_applicant_reuses_the_finalized_student() =>
		RunCertificationScenarioAsync(async () =>
		{
			var first = await CreateCertificationFixtureAsync("ReservedReapply");
			var firstResult = await FinalizeAsync(first.Application, true, "reserved-first-application");
			await WithUnitOfWorkAsync(async () =>
			{
				var onboarding = await _onboardingCaseRepository.GetAsync(firstResult.OnboardingCaseId);
				onboarding.Cancel("Family will reapply for a different offering", DateTime.UtcNow);
				await _onboardingCaseRepository.UpdateAsync(onboarding, autoSave: true);
			});
			var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(first.Applicant.Id));
			var later = await CreateCertificationFixtureAsync("ReservedReapply", existingApplicant: applicant);
			later.Applicant.Id.ShouldBe(first.Applicant.Id);
			later.Applicant.ReservedStudentId.ShouldBe(first.Applicant.ReservedStudentId);
			later.Application.StudentId.ShouldBeNull();
			var studentsBefore = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());

			var laterResult = await FinalizeAsync(later.Application, false, "reserved-later-application");
			laterResult.ReusedExistingStudent.ShouldBeTrue();
			laterResult.StudentId.ShouldBe(firstResult.StudentId);
			laterResult.OnboardingCaseId.ShouldNotBe(firstResult.OnboardingCaseId);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentsBefore);
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(first.Applicant.Id)))
				.ReservedStudentId.ShouldBe(first.Applicant.ReservedStudentId);
		});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Finalization_without_tenant_or_user_scope_access_does_not_consume_reservation(bool otherTenant) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ReservedAccess");
			var callerId = Guid.NewGuid();
			if (!otherTenant)
				await WithUnitOfWorkAsync(async () =>
				{
					await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(
						callerId, $"no-scope-{callerId:N}", $"no-scope-{callerId:N}@example.test", TestTenantId),
						autoSave: true);
					(await _scopeRepository.CountAsync(item => item.UserId == callerId)).ShouldBe(0);
				});
			using (_currentTenant.Change(otherTenant ? Guid.NewGuid() : TestTenantId))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, callerId.ToString()),
				new Claim(AbpClaimTypes.UserName, "admissions-without-scope")
			], "Test"))))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() =>
					FinalizeAsync(fixture.Application, true, "reserved-inaccessible"));
			}
			await AssertReservationUnusedAsync(fixture);
		});

	private async Task<BusinessException> FinalizeCollisionInTransactionAsync(
		AdmissionApplicationDto application, string idempotencyKey)
	{
		var decisions = GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>();
		var activities = GetRequiredService<IRepository<AdmissionActivity, Guid>>();
		var events = GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>();
		var before = await WithUnitOfWorkAsync(async () => new
		{
			Decisions = await decisions.CountAsync(item => item.ApplicationId == application.Id),
			Activities = await activities.CountAsync(item => item.ApplicationId == application.Id),
			Steps = await _onboardingStepRepository.CountAsync(),
			Events = await events.CountAsync()
		});
		var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
		{
			// The shared test module disables automatic transactions; restore the real finalization boundary here.
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			await using var transaction = await db.Database.BeginTransactionAsync();
			try { await FinalizeAsync(application, true, idempotencyKey); }
			finally { await transaction.RollbackAsync(); }
		}));
		await WithUnitOfWorkAsync(async () =>
		{
			(await decisions.CountAsync(item => item.ApplicationId == application.Id)).ShouldBe(before.Decisions);
			(await activities.CountAsync(item => item.ApplicationId == application.Id)).ShouldBe(before.Activities);
			(await _onboardingStepRepository.CountAsync()).ShouldBe(before.Steps);
			(await events.CountAsync()).ShouldBe(before.Events);
		});
		return exception;
	}

	private async Task AssertReservationUnusedAsync(AdmissionCertificationFixture fixture)
	{
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		application.StudentId.ShouldBeNull();
		application.OnboardingCaseId.ShouldBeNull();
		var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(fixture.Applicant.Id));
		applicant.ReservedStudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
		applicant.ExistingStudentId.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(applicant.ReservedStudentId))).ShouldBeNull();
		(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(
			item => item.ApplicationId == fixture.Application.Id))).ShouldBe(0);
	}
}
