using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Record_resume_return_uses_existing_unfinished_application_and_public_activation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var original = await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.Student.Id); student.AssignGrNumber("RESUME-PERMANENT-GR", 54); await _studentRepository.UpdateAsync(student, true);
			var completed = await _onboardingCaseRepository.GetAsync(x => x.StudentId == student.Id && x.Status == StudentOnboardingStatus.Completed);
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == student.Id); enrollment.LinkOnboardingCase(completed.Id); await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
			return (Case: completed, Enrollment: enrollment);
		});
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering); input.Route = "ResumeStage"; input.ResumeApplicationId = original.Case.ApplicationId;
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ReturnAsync(input))).Code.ShouldBe("Students:Records:InvalidReturnApplication");
		var applicant = await CreateCertificationApplicantAsync("Transition", true, true, fixture.Student.Id);
		var existingApplication = await StartFixtureApplicationAsync(applicant.Id, fixture.Offering.Id);
		var options = await fixture.Service.GetOptionsAsync(fixture.Student.AcademicYearId, fixture.Student.Id);
		options.ResumeApplications.ShouldNotContain(x => x.Id == original.Case.ApplicationId);
		options.ResumeApplications.ShouldContain(x => x.Id == existingApplication.Id);
		input.ResumeApplicationId = existingApplication.Id;
		var count = await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync());
		var returned = await fixture.Service.ReturnAsync(input); returned.Status.ShouldBe("AwaitingAdmission"); returned.AdmissionApplicationId.ShouldBe(existingApplication.Id);
		(await fixture.Service.ReturnAsync(input)).AdmissionApplicationId.ShouldBe(existingApplication.Id);
		(await WithUnitOfWorkAsync(() => _applicationRepository.CountAsync())).ShouldBe(count);
		var onboarding = GetRequiredService<IStudentOnboardingAppService>();
		await onboarding.CreatePolicyAsync(new() { AcademicYearId = fixture.Student.AcademicYearId!.Value, Name = "Reviewed resumed return",
			RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false, RequireStudentAccount = false, RequireGuardianAccount = false });
		var finalized = await FinalizeAsync(await ReadyAdmissionReturnApplicationAsync(existingApplication.Id), false, "resume-reviewed-finalize");
		finalized.StudentId.ShouldBe(fixture.Student.Id); finalized.OnboardingCaseId.ShouldNotBe(original.Case.Id);
		var item = await onboarding.GetAsync(finalized.OnboardingCaseId); item.AdmissionRoute.ShouldBe("Returning student");
		var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "ResumedReturn", 40), true));
		item = await onboarding.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = Guid.NewGuid(), RollNumber = 19, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await onboarding.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
		var activation = new ActivateStudentOnboardingDto { IdempotencyKey = "resume-reviewed-activate", ConcurrencyStamp = item.ConcurrencyStamp };
		item = await onboarding.ActivateAsync(item.Id, activation); item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
		(await onboarding.ActivateAsync(item.Id, activation)).StudentEnrollmentId.ShouldBe(original.Enrollment.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await _studentEnrollmentRepository.GetAsync(original.Enrollment.Id); enrollment.IsActive.ShouldBeTrue(); enrollment.OnboardingCaseId.ShouldBe(original.Case.Id);
			(await _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.Student.Id)).ShouldBe(1);
			(await _onboardingCaseRepository.GetAsync(original.Case.Id)).ConcurrencyStamp.ShouldBe(original.Case.ConcurrencyStamp);
			(await _studentRepository.GetAsync(fixture.Student.Id)).GrNumber.ShouldBe("RESUME-PERMANENT-GR");
		});
	});

	[Fact]
	public Task Record_inquiry_return_preserves_contact_qualification_and_links_real_application_to_finalization() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.Inquiry);
		var managedWorkflow = await CreateAndPublishManagedInquiryWorkflowAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var offering = await _offeringRepository.GetAsync(fixture.Offering.Id);
			offering.ConfigureReferences(managedWorkflow.Id, offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId); await _offeringRepository.UpdateAsync(offering, true);
		});
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		input.Inquiry!.FormValues = [new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Unauthorized permanent name\"" },
			new() { FieldCode = "GUARDIAN_MOBILE", ValueJson = "\"9999999999\"" }, new() { FieldCode = "LEARNING_NOTES", ValueJson = "\"Returning admission evidence\"" }];
		var returned = await fixture.Service.ReturnAsync(input); returned.AdmissionInquiryId.ShouldNotBeNull(); returned.AdmissionApplicationId.ShouldBeNull();
		var inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(returned.AdmissionInquiryId!.Value);
		var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
		var state = await management.GetAsync(inquiry.Id); state.Managed.ShouldBeTrue(); state.ContactCount.ShouldBe(0); state.CanStartApplication.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Offering.Id, inquiry.Id))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ContactRequired);
		state = await management.RecordContactAsync(inquiry.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Channel = "PHONE", Direction = "OUTBOUND", Outcome = "CONNECTED", Note = "Reviewed returning contact" });
		state = await management.QualifyAsync(inquiry.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, Outcome = AdmissionInquiryQualificationOutcome.Qualified,
			Reason = "Returning eligibility checked", Answers = [new() { Code = "AGE", Passed = true }] });
		state.CanStartApplication.ShouldBeTrue();
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Offering.Id, inquiry.Id);
		(await fixture.Service.ReturnAsync(input)).AdmissionApplicationId.ShouldBe(application.Id);
		(await fixture.Service.GetAsync(returned.TransitionId!.Value)).AdmissionApplicationId.ShouldBe(application.Id);
		(await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, fixture.Offering.Id, inquiry.Id)).Id.ShouldBe(application.Id);
		var captured = await _applicationAppService.GetFormAsync(application.Id);
		captured.Values.ShouldContain(x => x.FieldCode == "LEARNING_NOTES" && x.ValueJson == "\"Returning admission evidence\"");
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(application.ApplicantId))).FirstName.ShouldBe(fixture.Student.FirstName);
		var finalized = await FinalizeAsync(await ReadyAdmissionReturnApplicationAsync(application.Id), false, "inquiry-reviewed-return-finalize");
		finalized.StudentId.ShouldBe(fixture.Student.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			(await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId)).ExtraProperties["RecordReturnTransitionId"].ToString().ShouldBe(returned.TransitionId.Value.ToString("D"));
			(await _onboardingCaseRepository.CountAsync(x => x.StudentId == fixture.Student.Id && x.Status == StudentOnboardingStatus.Completed)).ShouldBe(1);
			(await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id)).IsActive.ShouldBeFalse();
			(await _studentRepository.GetAsync(fixture.Student.Id)).FirstName.ShouldBe(fixture.Student.FirstName);
		});
	});
}
