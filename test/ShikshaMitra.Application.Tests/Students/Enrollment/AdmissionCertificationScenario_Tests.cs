using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static readonly DateTime CertificationBirthDate = new(2016, 4, 12);

	[Fact]
	public Task Certification_01_new_student_completes_the_realistic_journey_and_stops_at_the_admissions_boundary() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Aarav", realisticWorkflow: true);
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.IsReady.ShouldBeTrue();
			preview.Items.ShouldAllBe(item => item.Passed);

			var result = await FinalizeAsync(fixture.Application, confirmCreateNew: true, "cert-new-student");
			result.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
			result.Application.StudentId.ShouldBe(result.StudentId);
			result.Application.OnboardingCaseId.ShouldBe(result.OnboardingCaseId);
			result.Application.EnrolledStudentId.ShouldBeNull();
			result.Application.StudentEnrollmentId.ShouldBeNull();
			result.ReusedExistingStudent.ShouldBeFalse();

			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(result.StudentId));
			student.Status.ShouldBe(StudentLifeCycleStatusEnum.Admission);
			student.StandardId.ShouldBeNull();
			student.DivisionId.ShouldBeNull();
			student.GrNo.ShouldBeNull();
			student.GrNumber.ShouldBeNull();
			(await WithUnitOfWorkAsync(() =>
				_studentEnrollmentRepository.CountAsync(row => row.StudentId == student.Id))).ShouldBe(0);

			var onboardingCase = await WithUnitOfWorkAsync(() =>
				_onboardingCaseRepository.GetAsync(result.OnboardingCaseId));
			onboardingCase.ApplicationId.ShouldBe(fixture.Application.Id);
			onboardingCase.IntendedStandardId.ShouldBe(fixture.Offering.StandardId);
			(await WithUnitOfWorkAsync(() =>
				_onboardingStepRepository.CountAsync(step => step.OnboardingCaseId == onboardingCase.Id)))
				.ShouldBe(StudentOnboardingStepKeys.Defaults.Length);

			var legacy = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.EnrollAsync(fixture.Application.Id, new EnrollAdmissionApplicationDto()));
			legacy.Code.ShouldBe(StudentOnboardingErrorCodes.LegacyEnrollDisabled);
		});

	[Fact]
	public Task Certification_02_returning_student_is_reused_and_an_existing_target_year_enrollment_is_blocked() =>
		RunCertificationScenarioAsync(async () =>
		{
			var reusable = await CreateCertificationFixtureAsync("Diya", linkExistingStudent: true);
			var original = await WithUnitOfWorkAsync(() =>
				_studentRepository.GetAsync(reusable.ExistingStudentId!.Value));
			var originalStatus = original.Status;
			var originalStandardId = original.StandardId;
			var studentCount = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());

			var reused = await FinalizeAsync(reusable.Application, false, "cert-returning-student");
			reused.ReusedExistingStudent.ShouldBeTrue();
			reused.StudentId.ShouldBe(reusable.ExistingStudentId.Value);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(studentCount);
			var unchanged = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(reused.StudentId));
			unchanged.Status.ShouldBe(originalStatus);
			unchanged.StandardId.ShouldBe(originalStandardId);

			var duplicateCase = await CreateCertificationFixtureAsync(
				"Diya", existingStudentId: reused.StudentId);
			var duplicateCaseError = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(duplicateCase.Application, false, "cert-duplicate-active-case"));
			duplicateCaseError.Code.ShouldBe(StudentOnboardingErrorCodes.OnboardingAlreadyExists);

			var alreadyEnrolled = await CreateCertificationFixtureAsync("Ishaan", linkExistingStudent: true);
			await WithUnitOfWorkAsync(async () =>
			{
				var cycle = await _cycleRepository.GetAsync(alreadyEnrolled.Offering.AdmissionCycleId);
				await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(
					Guid.NewGuid(), TestTenantId, alreadyEnrolled.ExistingStudentId!.Value,
					cycle.AcademicYearId, alreadyEnrolled.Offering.StandardId, null), autoSave: true);
			});
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(alreadyEnrolled.Application, false, "cert-existing-enrollment"));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.EnrollmentAlreadyExists);
		});

	[Fact]
	public Task Certification_03_exact_duplicate_requires_an_explicit_staff_create_new_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Kabir");
			var existing = await CreateExistingStudentAsync(fixture.Offering, "Kabir", "Patel");
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.IsReady.ShouldBeFalse();
			preview.PossibleMatches.Select(match => match.StudentId).ShouldContain(existing.Id);

			var unresolved = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(fixture.Application, false, "cert-duplicate-unresolved"));
			unresolved.Code.ShouldBe(StudentOnboardingErrorCodes.DuplicateResolutionRequired);

			var result = await FinalizeAsync(fixture.Application, true, "cert-duplicate-new");
			result.StudentId.ShouldNotBe(existing.Id);
			result.ReusedExistingStudent.ShouldBeFalse();
		});

	[Fact]
	public Task Certification_04_unrelated_existing_student_cannot_be_selected_as_a_duplicate_match() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Myra");
			var unrelated = await CreateExistingStudentAsync(fixture.Offering, "Vivaan", "Shah");
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.FinalizeAsync(fixture.Application.Id, new FinalizeAdmissionApplicationDto
				{
					ConcurrencyStamp = fixture.Application.ConcurrencyStamp,
					IdempotencyKey = "cert-invalid-existing",
					ExistingStudentId = unrelated.Id,
					ConfirmGuardianContactVerified = true
				}));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.ExistingStudentInvalid);
		});

	[Fact]
	public Task Certification_05_incomplete_core_identity_blocks_finalization_without_partial_records() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Anaya", completeIdentity: false);
			var beforeStudents = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.Items.Single(item => item.Key == "identity").Passed.ShouldBeFalse();

			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(fixture.Application, true, "cert-missing-identity"));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(beforeStudents);
			(await WithUnitOfWorkAsync(() =>
				_onboardingCaseRepository.CountAsync(row => row.ApplicationId == fixture.Application.Id))).ShouldBe(0);
		});

	[Fact]
	public Task Certification_06_missing_primary_guardian_blocks_finalization_without_partial_records() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Riya", createGuardian: false);
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.Items.Single(item => item.Key == "guardian").Passed.ShouldBeFalse();

			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(fixture.Application, true, "cert-missing-guardian"));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
			(await WithUnitOfWorkAsync(() =>
				_onboardingCaseRepository.CountAsync(row => row.ApplicationId == fixture.Application.Id))).ShouldBe(0);
		});

	[Fact]
	public Task Certification_07_staff_must_explicitly_confirm_guardian_contact_verification() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Sai");
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.FinalizeAsync(fixture.Application.Id, new FinalizeAdmissionApplicationDto
				{
					ConcurrencyStamp = fixture.Application.ConcurrencyStamp,
					IdempotencyKey = "cert-unverified-guardian",
					ConfirmCreateNew = true,
					ConfirmGuardianContactVerified = false
				}));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.GuardianContactUnverified);
			(await WithUnitOfWorkAsync(() =>
				_onboardingCaseRepository.CountAsync(row => row.ApplicationId == fixture.Application.Id))).ShouldBe(0);
		});

	[Fact]
	public Task Certification_08_unaccepted_and_expired_offers_both_block_finalization() =>
		RunCertificationScenarioAsync(async () =>
		{
			var unaccepted = await CreateCertificationFixtureAsync("Zara", acceptOffer: false);
			var unacceptedPreview = await _applicationAppService.GetFinalizationPreviewAsync(unaccepted.Application.Id);
			unacceptedPreview.Items.Single(item => item.Key == "offer").Passed.ShouldBeFalse();
			var unacceptedError = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(unaccepted.Application, true, "cert-unaccepted-offer"));
			unacceptedError.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);

			var expired = await CreateCertificationFixtureAsync("Arjun");
			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(expired.Application.Id);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.OfferExpiresAt))!
					.SetValue(application, DateTime.UtcNow.AddMinutes(-1));
				await _applicationRepository.UpdateAsync(application, autoSave: true);
			});
			expired = expired with { Application = await _applicationAppService.GetAsync(expired.Application.Id) };
			var expiredPreview = await _applicationAppService.GetFinalizationPreviewAsync(expired.Application.Id);
			expiredPreview.Items.Single(item => item.Key == "offer").Passed.ShouldBeFalse();
			var expiredError = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(expired.Application, true, "cert-expired-offer"));
			expiredError.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
		});

	[Fact]
	public Task Certification_09_incomplete_workflow_blocks_finalization_even_after_offer_acceptance() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Navya", completeWorkflow: false);
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.Items.Single(item => item.Key == "workflow").Passed.ShouldBeFalse();
			var blocked = await Should.ThrowAsync<BusinessException>(() =>
				FinalizeAsync(fixture.Application, true, "cert-workflow-incomplete"));
			blocked.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
		});

	[Fact]
	public Task Certification_10_stale_writes_are_rejected_and_an_idempotent_retry_returns_the_same_result() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Reyansh");
			var stale = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.FinalizeAsync(fixture.Application.Id, new FinalizeAdmissionApplicationDto
				{
					ConcurrencyStamp = Guid.NewGuid().ToString("N"),
					IdempotencyKey = "cert-stale-finalization",
					ConfirmCreateNew = true,
					ConfirmGuardianContactVerified = true
				}));
			stale.Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);

			var first = await FinalizeAsync(fixture.Application, true, "cert-idempotent-finalization");
			var second = await _applicationAppService.FinalizeAsync(fixture.Application.Id,
				new FinalizeAdmissionApplicationDto
				{
					ConcurrencyStamp = fixture.Application.ConcurrencyStamp,
					IdempotencyKey = "cert-idempotent-finalization",
					ConfirmCreateNew = true,
					ConfirmGuardianContactVerified = true
				});
			second.StudentId.ShouldBe(first.StudentId);
			second.OnboardingCaseId.ShouldBe(first.OnboardingCaseId);
			(await WithUnitOfWorkAsync(() =>
				_onboardingCaseRepository.CountAsync(row => row.ApplicationId == fixture.Application.Id))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() =>
				_studentEnrollmentRepository.CountAsync(row => row.StudentId == first.StudentId))).ShouldBe(0);
		});

	private async Task RunCertificationScenarioAsync(Func<Task> scenario)
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), TestTenantId, userId: TestUserId,
				description: $"Admission certification {Guid.NewGuid():N}"), autoSave: true);
			await scenario();
		}
	}

	private async Task<AdmissionCertificationFixture> CreateCertificationFixtureAsync(
		string firstName,
		bool realisticWorkflow = false,
		bool completeWorkflow = true,
		bool completeIdentity = true,
		bool createGuardian = true,
		bool linkExistingStudent = false,
		bool acceptOffer = true,
		Guid? existingStudentId = null)
	{
		var workflow = realisticWorkflow
			? await CreateAndPublishRealisticWorkflowAsync()
			: await CreateAndPublishWorkflowAsync();
		var offering = await CreateAvailableOfferingAsync(workflow.Id);
		if (linkExistingStudent)
			existingStudentId = (await CreateExistingStudentAsync(offering, firstName, "Patel")).Id;
		var applicant = await CreateCertificationApplicantAsync(
			firstName, true, createGuardian, existingStudentId);
		var application = await _applicationAppService.StartAsync(applicant.Id, offering.Id);
		var submitResult = await _applicationAppService.SubmitAsync(application.Id);
		submitResult.ValidationErrors.ShouldBeEmpty();
		if (!completeIdentity)
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var storedApplicant = await _applicantRepository.GetAsync(applicant.Id);
				storedApplicant.UpdateProfile(firstName, null, null, null, null,
					storedApplicant.AddressLine1, storedApplicant.AddressLine2, storedApplicant.City,
					storedApplicant.State, storedApplicant.Country, storedApplicant.PostalCode);
				await _applicantRepository.UpdateAsync(storedApplicant, autoSave: true);
			});
		}
		if (completeWorkflow)
			await CompleteCertificationWorkflowAsync(application.Id);

		application = await _applicationAppService.GetAsync(application.Id);
		application = await _applicationAppService.MarkInReviewAsync(application.Id,
			Decision(application, "Application review started"));
		application = await _applicationAppService.ApproveAsync(application.Id,
			Decision(application, "Eligibility and submitted application approved"));
		application = await _applicationAppService.IssueOfferAsync(application.Id,
			Decision(application, "Admission offer issued", DateTime.UtcNow.AddDays(30)));
		if (acceptOffer)
			application = await _applicationAppService.AcceptOfferAsync(application.Id,
				Decision(application, "Guardian accepted the admission offer"));
		return new AdmissionCertificationFixture(applicant, offering, application, existingStudentId);
	}

	private async Task<AdmissionApplicant> CreateCertificationApplicantAsync(
		string firstName,
		bool completeIdentity,
		bool createGuardian,
		Guid? existingStudentId)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var family = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"CERT-{Guid.NewGuid():N}"[..20]);
			await _familyRepository.InsertAsync(family, autoSave: true);
			if (createGuardian)
			{
				var guardian = new AdmissionGuardian(
					Guid.NewGuid(), TestTenantId, family.Id, "Meera", "Patel",
					$"9{Random.Shared.NextInt64(100000000, 999999999)}", "meera.patel@example.test",
					AdmissionGuardianRelationship.Parent, true);
				await _guardianRepository.InsertAsync(guardian, autoSave: true);
				family.SetPrimaryGuardian(guardian.Id);
				await _familyRepository.UpdateAsync(family, autoSave: true);
			}

			var applicant = new AdmissionApplicant(
				Guid.NewGuid(), TestTenantId, family.Id, firstName, null,
				completeIdentity ? "Patel" : null,
				completeIdentity ? CertificationBirthDate : null,
				completeIdentity ? "Male" : null,
				existingStudentId,
				"14 School Road", null, "Ahmedabad", "Gujarat", "India", "380001");
			await _applicantRepository.InsertAsync(applicant, autoSave: true);
			return applicant;
		});
	}

	private async Task CompleteCertificationWorkflowAsync(Guid applicationId)
	{
		for (var index = 0; index < 20; index++)
		{
			var runtime = await _applicationAppService.GetWorkflowAsync(applicationId);
			if (runtime.Status == AdmissionWorkflowInstanceStatus.Completed)
				return;
			var stageId = runtime.CurrentStageInstanceId.ShouldNotBeNull();
			var application = await _applicationAppService.GetAsync(applicationId);
			await _applicationAppService.AdvanceWorkflowAsync(applicationId, new AdvanceAdmissionWorkflowDto
			{
				ExpectedCurrentStageInstanceId = stageId,
				IdempotencyKey = $"cert-stage-{stageId:N}",
				ConcurrencyStamp = application.ConcurrencyStamp
			});
		}

		throw new InvalidOperationException("The admission workflow did not complete within 20 transitions.");
	}

	private async Task<Student> CreateExistingStudentAsync(
		AdmissionOffering offering,
		string firstName,
		string lastName)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var student = Student.CreateDirectRegistration(
				Guid.NewGuid(), TestTenantId, firstName, null, lastName,
				cycle.AcademicYearId, offering.BoardId, offering.MediumId, offering.StageId,
				offering.StandardId, null,
				new DateTimeOffset(CertificationBirthDate, TimeSpan.Zero), GenderEnum.Male,
				$"8{Random.Shared.NextInt64(100000000, 999999999)}",
				email: $"{firstName.ToLowerInvariant()}.{Guid.NewGuid():N}@student.example.test");
			student.MoveToAdmission();
			student.ApproveAdmission("Existing Student used by admission certification.");
			await _studentRepository.InsertAsync(student, autoSave: true);
			return student;
		});
	}

	private Task<AdmissionFinalizationResultDto> FinalizeAsync(
		AdmissionApplicationDto application,
		bool confirmCreateNew,
		string idempotencyKey) =>
		_applicationAppService.FinalizeAsync(application.Id, new FinalizeAdmissionApplicationDto
		{
			ConcurrencyStamp = application.ConcurrencyStamp,
			IdempotencyKey = idempotencyKey,
			ConfirmCreateNew = confirmCreateNew,
			ConfirmGuardianContactVerified = true
		});

	private static AdmissionApplicationDecisionDto Decision(
		AdmissionApplicationDto application,
		string reason,
		DateTime? offerExpiresAt = null) => new()
	{
		ConcurrencyStamp = application.ConcurrencyStamp,
		Reason = reason,
		OfferExpiresAt = offerExpiresAt
	};

	private sealed record AdmissionCertificationFixture(
		AdmissionApplicant Applicant,
		AdmissionOffering Offering,
		AdmissionApplicationDto Application,
		Guid? ExistingStudentId);
}
