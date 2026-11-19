using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Full_name_and_split_name_representations_still_require_duplicate_resolution(bool existingFullName)
		=> RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingFlowFixtureAsync("NameMatch", null);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
				applicant.UpdateProfile(existingFullName ? "Aarav" : "Aarav Sharma", null,
					existingFullName ? "Sharma" : null, applicant.DateOfBirth, applicant.Gender);
				await _applicantRepository.UpdateAsync(applicant, true);
			});
			var existing = await CreateExistingStudentAsync(fixture.Offering, existingFullName ? "Aarav Sharma" : "Aarav",
				existingFullName ? "" : "Sharma");
			var count = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
			preview.PossibleMatches.ShouldContain(match => match.StudentId == existing.Id);
			preview.Items.ShouldContain(item => item.Key == "duplicate_resolution" && !item.Passed);
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			(await Should.ThrowAsync<Volo.Abp.BusinessException>(() => FinalizeAsync(
				application, false, "full-name-unresolved")))
				.Code.ShouldBe(StudentOnboardingErrorCodes.DuplicateResolutionRequired);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(count);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(row => row.ApplicationId == fixture.Application.Id))).ShouldBe(0);
		});

	[Theory]
	[InlineData("Applicant.Gender", "APPLICANT_GENDER")]
	[InlineData("Applicant.DateOfBirth", "APPLICANT_DOB")]
	[InlineData("Applicant.FirstName", "APPLICANT_FIRST_NAME")]
	public Task Submission_rejects_missing_core_identity_even_when_the_question_is_optional(string binding, string code) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var field = (await _formFieldRepository.GetListAsync(row => row.FormDefinitionId == offering.ApplicationFormDefinitionId && row.BindingKey == binding)).Single();
				await _formFieldRepository.DeleteAsync(field, true);
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), field.FormDefinitionId,
					field.SectionId, field.Code, field.Label, field.FieldType, field.BindingType, field.BindingKey,
					false, false, field.DisplayOrder, field.Placeholder, field.HelpText, field.DefaultValue,
					field.ValidationJson, field.VisibilityRuleJson, field.OptionsJson), true);
			});
			var applicant = await CreateCertificationApplicantAsync("IdentitySubmission", true, true, null);
			await WithUnitOfWorkAsync(async () =>
			{
				var saved = await _applicantRepository.GetAsync(applicant.Id);
				saved.UpdateProfile(saved.FirstName, null, null,
					binding == "Applicant.DateOfBirth" ? null : saved.DateOfBirth,
					binding == "Applicant.Gender" ? null : saved.Gender);
				await _applicantRepository.UpdateAsync(saved, true);
			});
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
			// Draft saves remain possible and cannot consume a submission attempt.
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values = [new() { FieldCode = code, ValueJson = "\"\"" }]
			});
			var result = await _applicationAppService.SubmitAsync(application.Id);
			result.ValidationErrors.ShouldNotBeEmpty();
			result.FieldErrors.ShouldContainKey(code);
			(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Draft);
			(await _applicationAppService.GetFormAsync(application.Id)).Status.ShouldBe(AdmissionFormInstanceStatus.Draft);
			var submissions = GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>();
			(await WithUnitOfWorkAsync(() => submissions.CountAsync(row => row.ApplicationId == application.Id))).ShouldBe(0);
			var corrected = binding == "Applicant.Gender" ? "Female" : binding == "Applicant.DateOfBirth" ? "2016-01-01" : "Cher";
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
				Values = [new() { FieldCode = code, ValueJson = JsonSerializer.Serialize(corrected) }]
			});
			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			(await WithUnitOfWorkAsync(() => submissions.CountAsync(row => row.ApplicationId == application.Id))).ShouldBe(1);
		});

	[Fact]
	public Task Single_name_guardian_finalization_and_activation_replays_preserve_one_student_case_and_enrollment() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingFlowFixtureAsync("SingleNameGuardian", null);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
				applicant.UpdateProfile("Cher", null, null, applicant.DateOfBirth, applicant.Gender);
				AdmissionFamilyContactCapture.Capture(applicant, new()
				{
					OtherGuardian = new() { Relationship = "Guardian", FullName = "Verified Guardian", Mobile = "9876543210" },
					MainContactRelationship = "guardian"
				}, parentRelationship: null);
				await _applicantRepository.UpdateAsync(applicant, true);
			});
			var studentsBefore = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeTrue();
			var finalized = await FinalizeAsync(application, true, "single-name-finalize");
			var replay = await FinalizeAsync(application, true, "single-name-finalize");
			replay.StudentId.ShouldBe(finalized.StudentId);
			replay.OnboardingCaseId.ShouldBe(finalized.OnboardingCaseId);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var division = await WithUnitOfWorkAsync(() => GetRequiredService<ShikshaMitra.TenantSettings.AcademicSetups.DivisionManager>()
				.CreateAsync(fixture.Offering.StandardId, "SingleNameQA", 40));
			await WithUnitOfWorkAsync(() => GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>()
				.InsertAsync(division, true));
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.AssignRollNumberAsync(item.Id, new() { RollNumber = 1, RollAssignmentMode = "automatic", ConcurrencyStamp = item.ConcurrencyStamp });
			var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
			item = await service.AssignGrNumberAsync(item.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			(await service.PreviewActivationAsync(item.Id)).LastError.ShouldBeNullOrEmpty();
			var activation = new ActivateStudentOnboardingDto { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "single-name-activate" };
			var activated = await service.ActivateAsync(item.Id, activation);
			var activatedReplay = await service.ActivateAsync(item.Id, activation);
			activated.StudentEnrollmentId.ShouldNotBeNull();
			activatedReplay.StudentEnrollmentId.ShouldBe(activated.StudentEnrollmentId);
			await WithUnitOfWorkAsync(async () =>
			{
				(await _studentRepository.CountAsync()).ShouldBe(studentsBefore + 1);
				(await _onboardingCaseRepository.CountAsync(row => row.ApplicationId == application.Id)).ShouldBe(1);
				(await _studentEnrollmentRepository.CountAsync(row => row.StudentId == finalized.StudentId)).ShouldBe(1);
				var student = (await _studentRepository.GetWithDetailsAsync(finalized.StudentId)).ShouldNotBeNull();
				student.FirstName.ShouldBe("Cher");
				student.LastName.ShouldBeNullOrEmpty();
				StudentFamilyContactSelection.Resolve(student).ShouldBe("guardian");
				student.FamilyDetail!.GuardianDetails.ShouldNotBeNull();
				student.FamilyDetail.FatherDetails.ShouldBeNull();
				student.FamilyDetail.MotherDetails.ShouldBeNull();
			});
		});
}
