using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static readonly DateTime CertificationBirthDate = new(2016, 4, 12);

	[Fact]
	public Task Certification_onboarding_settings_accept_a_GSEB_school_level_and_reject_invalid_coverage() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("GsebSettings");
			var existingMedium = await WithUnitOfWorkAsync(() => _mediumRepository.GetAsync(fixture.Offering.MediumId));
			var existingBoard = await WithUnitOfWorkAsync(() => _boardRepository.GetAsync(fixture.Offering.BoardId));
			existingBoard.Type.ShouldBe(EducationBoardType.CBSE);
			var path = await WithUnitOfWorkAsync(async () =>
			{
				var board = (await _boardRepository.GetListAsync(item =>
					item.AcademicYearId == existingBoard.AcademicYearId && item.Type == EducationBoardType.GSEB))
					.FirstOrDefault();
				board ??= await _boardRepository.InsertAsync(
					await GetRequiredService<EducationBoardManager>().CreateAsync(
						existingBoard.AcademicYearId, EducationBoardType.GSEB), true);
				var suffix = Guid.NewGuid().ToString("N")[..8];
				var mediumName = $"Gujarati {suffix}";
				var medium = await _mediumRepository.InsertAsync(
					await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, mediumName), true);
				var level = await _stageRepository.InsertAsync(
					await GetRequiredService<EducationMediumStageManager>().CreateAsync(
						medium.Id, $"GSEB - {mediumName} - Higher Secondary"), true);
				await _stageRepository.InsertAsync(
					await GetRequiredService<EducationMediumStageManager>().CreateAsync(
						medium.Id, $"GSEB - {mediumName} - Primary"), true);
				var standard = await _standardRepository.InsertAsync(
					await _standardManager.CreateAsync(level.Id, $"Class 11 {suffix}", 11, null), true);
				return (BoardId: board.Id, MediumId: medium.Id, LevelId: level.Id, StandardId: standard.Id);
			});
			var configurationJson = System.Text.Json.JsonSerializer.Serialize(new
			{
				academicAvailability = new { schoolLevelNames = new[] { "Higher Secondary" } },
				readinessAccess = new { studentAccount = true, guardianAccount = true },
				onboardingProfiles = new[]
				{
					new
					{
						id = "gseb-higher-secondary",
						admissionRoutes = new[] { "Fresh admission" },
						applicability = new
						{
							boardMode = "selected", boardIds = new[] { path.BoardId },
							mediumMode = "selected", academicMediumIds = new[] { path.MediumId },
							stageMode = "selected", academicMediumStageIds = new[] { path.LevelId },
							classMode = "selected", standardIds = new[] { path.StandardId },
							divisionMode = "all"
						},
						rules = new { studentAccount = true, guardianAccount = true }
					}
				},
				grNumber = new { configurations = Array.Empty<object>() },
				rollNumber = new { configurations = Array.Empty<object>() },
				divisionChange = new { configurations = new[] { new {
					id = "active-gseb-coverage", status = "draft", version = 1, isActive = false,
					boardMode = "selected", boardIds = new[] { path.BoardId },
					mediumMode = "selected", academicMediumIds = new[] { path.MediumId },
					stageMode = "selected", academicMediumStageIds = new[] { path.LevelId },
					classMode = "selected", standardIds = new[] { path.StandardId }, divisionMode = "all"
				} } }
			});
			var onboarding = GetRequiredService<IStudentOnboardingAppService>();
			var saved = await onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = existingBoard.AcademicYearId,
				ConfigurationJson = configurationJson
			});
			(await onboarding.GetSettingsAsync(existingBoard.AcademicYearId))
				.Single(item => item.AcademicYearId == existingBoard.AcademicYearId)
				.ConfigurationJson.ShouldBe(configurationJson);

			var invalidJson = configurationJson.Replace(path.MediumId.ToString(),
				existingMedium.Id.ToString(), StringComparison.OrdinalIgnoreCase);
			invalidJson.ShouldNotBe(configurationJson);
			(await Should.ThrowAsync<BusinessException>(() => onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = existingBoard.AcademicYearId,
				ConfigurationJson = invalidJson,
				ConcurrencyStamp = saved.ConcurrencyStamp
			}))).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
			var hiddenLevelJson = configurationJson.Replace(
				"\"schoolLevelNames\":[\"Higher Secondary\"]",
				"\"schoolLevelNames\":[\"Primary\"]", StringComparison.Ordinal);
			hiddenLevelJson.ShouldNotBe(configurationJson);
			var hiddenLevelError = await Should.ThrowAsync<BusinessException>(() => onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = existingBoard.AcademicYearId,
				ConfigurationJson = hiddenLevelJson,
				ConcurrencyStamp = saved.ConcurrencyStamp
			}));
			hiddenLevelError.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
			hiddenLevelError.Data["Reason"].ShouldBe("Existing rules target a hidden onboarding school level.");
			var malformedReadinessJson = configurationJson.Replace(
				"\"studentAccount\":true", "\"studentAccount\":\"true\"", StringComparison.Ordinal);
			malformedReadinessJson.ShouldNotBe(configurationJson);
			(await Should.ThrowAsync<BusinessException>(() => onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = existingBoard.AcademicYearId,
				ConfigurationJson = malformedReadinessJson,
				ConcurrencyStamp = saved.ConcurrencyStamp
			}))).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
			(await onboarding.GetSettingsAsync(existingBoard.AcademicYearId))
				.Single(item => item.AcademicYearId == existingBoard.AcademicYearId)
				.ConfigurationJson.ShouldBe(configurationJson);
		});

	[Fact]
	public Task Certification_01_new_student_completes_the_realistic_journey_and_stops_at_the_admissions_boundary() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Aarav", campusScoped: true, currentWorkflow: true);
			fixture.Offering.CampusId.ShouldNotBeNull();
			var campusId = fixture.Offering.CampusId.Value;
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var onboarding = GetRequiredService<IStudentOnboardingAppService>();
			var allPath = new
			{
				boardMode = "all",
				mediumMode = "all",
				stageMode = "all",
				classMode = "all",
				divisionMode = "all"
			};
			var settingsJson = System.Text.Json.JsonSerializer.Serialize(new
			{
				readinessAccess = new { studentAccount = false, guardianAccount = false },
				onboardingProfiles = new object[]
				{
					new { id = "all-campus", admissionRoutes = new[] { "Fresh admission" },
						applicability = allPath, rules = new { studentAccount = false } },
					new { id = "certification-campus", admissionRoutes = new[] { "Fresh admission" },
						applicability = new { campusMode = "selected", campusIds = new[] { campusId },
							allPath.boardMode, allPath.mediumMode, allPath.stageMode,
							allPath.classMode, allPath.divisionMode },
						rules = new { studentAccount = true } },
					new { id = "scholarship-campus", admissionRoutes = new[] { "Scholarship intake" },
						applicability = new { campusMode = "selected", campusIds = new[] { campusId },
							allPath.boardMode, allPath.mediumMode, allPath.stageMode,
							allPath.classMode, allPath.divisionMode },
						rules = new { guardianAccount = true } }
				},
				grNumber = new { configurations = Array.Empty<object>() },
				rollNumber = new { configurations = Array.Empty<object>() },
				divisionChange = new { configurations = Array.Empty<object>() }
			});
			var savedSettings = await onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = settingsJson
			});
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
			result.StudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			var finalizedApplicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(fixture.Applicant.Id));
			finalizedApplicant.ReservedStudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			finalizedApplicant.ExistingStudentId.ShouldBe(result.StudentId);

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
			onboardingCase.GetCampusId().ShouldBe(fixture.Offering.CampusId);
			onboardingCase.IntendedStandardId.ShouldBe(fixture.Offering.StandardId);
			var onboardingView = await onboarding.GetAsync(onboardingCase.Id);
			onboardingView.CampusId.ShouldBe(campusId);
			// Historical named profiles cannot override the current global readiness rules.
			onboardingView.StudentAccountRequired.ShouldBeFalse();
			await WithUnitOfWorkAsync(async () =>
			{
				var legacyCase = await _onboardingCaseRepository.GetAsync(onboardingCase.Id);
				legacyCase.ExtraProperties.Remove("AdmissionCampusId");
				await _onboardingCaseRepository.UpdateAsync(legacyCase, autoSave: true);
			});
			var legacyView = await onboarding.GetAsync(onboardingCase.Id);
			legacyView.CampusId.ShouldBe(campusId);
			legacyView.StudentAccountRequired.ShouldBeFalse();
			var scholarshipView = await onboarding.SetAdmissionRouteAsync(onboardingCase.Id, new()
			{
				AdmissionRoute = "SCHOLARSHIP INTAKE",
				ConcurrencyStamp = legacyView.ConcurrencyStamp
			});
			scholarshipView.AdmissionRoute.ShouldBe("Scholarship intake");
			scholarshipView.GuardianAccountRequired.ShouldBeFalse();
			scholarshipView.StudentAccountRequired.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => onboarding.SetAdmissionRouteAsync(onboardingCase.Id, new()
			{
				AdmissionRoute = "Unconfigured intake",
				ConcurrencyStamp = scholarshipView.ConcurrencyStamp
			}))).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
			var withoutCustomRoute = System.Text.Json.Nodes.JsonNode.Parse(settingsJson)!;
			withoutCustomRoute["onboardingProfiles"]!.AsArray().RemoveAt(2);
			await onboarding.SaveSettingsAsync(new()
			{
				AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = withoutCustomRoute.ToJsonString(),
				ConcurrencyStamp = savedSettings.ConcurrencyStamp
			});
			var refreshed = await onboarding.RefreshReadinessAsync(onboardingCase.Id, scholarshipView.ConcurrencyStamp);
			refreshed.AdmissionRoute.ShouldBe(scholarshipView.AdmissionRoute);
			refreshed.StudentAccountRequired.ShouldBeFalse();
			refreshed.GuardianAccountRequired.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() =>
				_onboardingStepRepository.CountAsync(step => step.OnboardingCaseId == onboardingCase.Id)))
				.ShouldBe(StudentOnboardingStepKeys.Defaults.Length);

#pragma warning disable CS0618 // The compatibility endpoint is intentionally exercised here.
			var legacy = await Should.ThrowAsync<BusinessException>(() =>
				_applicationAppService.EnrollAsync(fixture.Application.Id, new EnrollAdmissionApplicationDto()));
#pragma warning restore CS0618
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
			reused.StudentId.ShouldBe(original.Id);
			(await WithUnitOfWorkAsync(() => _studentRepository.FindAsync(reusable.Applicant.ReservedStudentId)))
				.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(reusable.Applicant.Id)))
				.ReservedStudentId.ShouldBe(reusable.Applicant.ReservedStudentId);
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
			result.StudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
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
	public Task Certification_current_approval_finalizes_without_a_retired_offer() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("NoOffer");
		await WithUnitOfWorkAsync(async () =>
		{
			var application = await _applicationRepository.GetAsync(fixture.Application.Id);
			typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.OfferExpiresAt))!.SetValue(application, DateTime.UtcNow.AddDays(-1));
			await _applicationRepository.UpdateAsync(application, true);
		});
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		application.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeTrue();
		(await FinalizeAsync(application, true, "current-no-retired-offer")).Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
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
			second.StudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(fixture.Applicant.Id)))
				.ReservedStudentId.ShouldBe(fixture.Applicant.ReservedStudentId);
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
			await EnsureTestIdentityUserAsync(TestUserId, "records-certification-actor");
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
		bool campusScoped = false,
		Guid? existingStudentId = null,
		AdmissionApplicant? existingApplicant = null,
		bool currentWorkflow = true)
	{
		var workflow = currentWorkflow
			? await CreateCurrentCertificationWorkflowAsync()
			: realisticWorkflow
			? await CreateAndPublishRealisticWorkflowAsync()
			: await CreateAndPublishWorkflowAsync();
		Guid? registrationFormId = null;
		if (currentWorkflow)
		{
			using var configuration = System.Text.Json.JsonDocument.Parse(
				workflow.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.Form).ConfigurationJson!);
			registrationFormId = configuration.RootElement.GetProperty("formDefinitionId").GetGuid();
		}
		var offering = await CreateAvailableOfferingAsync(workflow.Id, campusScoped: campusScoped, registrationFormId: registrationFormId);
		if (linkExistingStudent)
			existingStudentId = (await CreateExistingStudentAsync(offering, firstName, "Patel")).Id;
		var applicant = existingApplicant ?? await CreateCertificationApplicantAsync(
			firstName, true, createGuardian, existingStudentId);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
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
			await CompleteCertificationWorkflowAsync(application.Id, stopBeforeApproval: true);

		application = await _applicationAppService.GetAsync(application.Id);
		if (currentWorkflow)
		{
			if (!completeWorkflow)
				return new AdmissionCertificationFixture(applicant, offering, application, existingStudentId);
			application = await _applicationAppService.ApproveAsync(application.Id,
				Decision(application, "Eligibility and submitted application approved"));
			if (completeWorkflow)
				await CompleteCertificationWorkflowAsync(application.Id);
			return new AdmissionCertificationFixture(applicant, offering,
				await _applicationAppService.GetAsync(application.Id), existingStudentId);
		}

		application = await _applicationAppService.MarkInReviewAsync(application.Id,
			Decision(application, "Application review started"));
		application = await _applicationAppService.ApproveAsync(application.Id,
			Decision(application, "Eligibility and submitted application approved"));
		if (completeWorkflow)
			await CompleteCertificationWorkflowAsync(application.Id);
		application = await _applicationAppService.GetAsync(application.Id);
		application = await _applicationAppService.IssueOfferAsync(application.Id,
			Decision(application, "Admission offer issued", DateTime.UtcNow.AddDays(30)));
		if (acceptOffer)
			application = await _applicationAppService.AcceptOfferAsync(application.Id,
				Decision(application, "Guardian accepted the admission offer"));
		return new AdmissionCertificationFixture(applicant, offering, application, existingStudentId);
	}

	private async Task<AdmissionWorkflowDto> CreateCurrentCertificationWorkflowAsync()
	{
		var registrationForm = await WithUnitOfWorkAsync(async () =>
		{
			var definition = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Certification registration form",
				$"CERT_FORM_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage);
			definition.Publish(TestUserId);
			return await _formRepository.InsertAsync(definition, true);
		});
		var form = Guid.NewGuid();
		var decision = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Current certification journey", Code = $"CURRENT_{Guid.NewGuid():N}"[..24],
			Stages = [new() { Id = form, Code = "FORM", Name = "Admission form",
				StageType = AdmissionWorkflowStageType.Form, IsRequired = true, IsEntryStage = true,
				ConfigurationJson = System.Text.Json.JsonSerializer.Serialize(new
				{
					formDefinitionId = registrationForm.Id,
					form = new AdmissionFormStagePolicy { Version = 1, Enabled = true, AutoAdvanceOnSubmit = true }
				}) },
				new() { Id = decision, Code = "DECISION", Name = "Admission decision", DisplayOrder = 1,
					StageType = AdmissionWorkflowStageType.Approval, IsRequired = true, IsTerminalStage = true }],
			Transitions = [new() { FromStageId = form, ToStageId = decision, IsDefault = true, Priority = 1 }]
		});
		var validation = await _workflowAppService.ValidateAsync(workflow.Id);
		validation.Issues.Where(issue => issue.Severity == AdmissionWorkflowValidationSeverity.Error)
			.Select(issue => issue.Message).ShouldBeEmpty();
		workflow = await _workflowAppService.GetAsync(workflow.Id);
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
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
				Guid.NewGuid(),
				"14 School Road", null, "Ahmedabad", "Gujarat", "India", "380001");
			await _applicantRepository.InsertAsync(applicant, autoSave: true);
			return applicant;
		});
	}

	private async Task CompleteCertificationWorkflowAsync(Guid applicationId, bool stopBeforeApproval = false)
	{
		for (var index = 0; index < 20; index++)
		{
			var runtime = await _applicationAppService.GetWorkflowAsync(applicationId);
			if (runtime.Status == AdmissionWorkflowInstanceStatus.Completed)
				return;
			var stageId = runtime.CurrentStageInstanceId.ShouldNotBeNull();
			if (stopBeforeApproval && runtime.Stages.Single(stage => stage.Id == stageId).StageType == AdmissionWorkflowStageType.Approval)
				return;
			var application = await _applicationAppService.GetAsync(applicationId);
			await _applicationAppService.AdvanceWorkflowAsync(applicationId, new AdvanceAdmissionWorkflowDto
			{
				ExpectedCurrentStageInstanceId = stageId,
				DeferFeeCollection = runtime.Stages.Single(stage => stage.Id == stageId).StageType == AdmissionWorkflowStageType.Payment,
				Reason = "Fixture family requested collection later in Onboarding",
				IdempotencyKey = $"cert-stage-{stageId:N}",
				ConcurrencyStamp = application.ConcurrencyStamp
			});
		}

		throw new InvalidOperationException("The admission workflow did not complete within 20 transitions.");
	}

	private async Task<Student> CreateExistingStudentAsync(
		AdmissionOffering offering,
		string firstName,
		string lastName,
		Guid? studentId = null)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var student = Student.CreateDirectRegistration(
				studentId ?? Guid.NewGuid(), TestTenantId, firstName, null, lastName,
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
