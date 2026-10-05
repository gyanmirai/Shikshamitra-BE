using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionEntryMode.Inquiry)]
	public Task Record_return_rejection_closes_once_preserves_history_and_allows_new_review(AdmissionEntryMode mode) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(mode);
		if (mode == AdmissionEntryMode.Inquiry)
		{
			var workflow = await CreateAndPublishManagedInquiryWorkflowAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var offering = await _offeringRepository.GetAsync(fixture.Offering.Id);
				offering.ConfigureReferences(workflow.Id, offering.InquiryFormDefinitionId, offering.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(offering, true);
			});
		}
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		var returned = await fixture.Service.ReturnAsync(input);
		Guid applicationId;
		if (returned.AdmissionInquiryId.HasValue)
		{
			var inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(returned.AdmissionInquiryId.Value);
			var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await management.GetAsync(inquiry.Id);
			state = await management.RecordContactAsync(inquiry.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp,
				Channel = "PHONE", Direction = "OUTBOUND", Outcome = "CONNECTED", Note = "Returning family contacted" });
			await management.QualifyAsync(inquiry.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp,
				Outcome = AdmissionInquiryQualificationOutcome.Qualified, Reason = "Eligibility reviewed", Answers = [new() { Code = "AGE", Passed = true }] });
			applicationId = (await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Offering.Id, inquiry.Id)).Id;
		}
		else applicationId = returned.AdmissionApplicationId!.Value;
		var preserved = await AdmissionReturnHistoryAsync(fixture.Student.Id);
		var submitted = await _applicationAppService.SubmitAsync(applicationId);
		submitted.ValidationErrors.ShouldBeEmpty();
		const string reason = "School rejected this reviewed return application";
		var rejected = await _applicationAppService.RejectAsync(applicationId, Decision(submitted.Application, reason));
		rejected.Status.ShouldBe(AdmissionApplicationStatus.Rejected);
		(await fixture.Service.GetAsync(returned.TransitionId!.Value)).Status.ShouldBe("Cancelled");
		(await fixture.Service.ReturnAsync(input)).Status.ShouldBe("Cancelled");
		await WithUnitOfWorkAsync(async () => await GetRequiredService<StudentRecordAdmissionReturnClosureService>()
			.CloseRejectedApplicationAsync(await _applicationRepository.GetAsync(applicationId), reason));
		(await AdmissionReturnHistoryAsync(fixture.Student.Id)).ShouldBe(preserved);
		await AssertAdmissionReturnClosureEventAsync(returned.TransitionId.Value, "AdmissionRejected", reason);
		var fresh = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		fresh.Status.ShouldBe("AwaitingAdmission"); fresh.TransitionId.ShouldNotBe(returned.TransitionId);
		(await fixture.Service.GetAsync(returned.TransitionId.Value)).Status.ShouldBe("Cancelled");
		(await _applicationAppService.GetAsync(applicationId)).Status.ShouldBe(AdmissionApplicationStatus.Rejected);
	});

	[Fact]
	public Task Record_return_onboarding_cancel_closes_once_preserves_original_case_and_allows_new_review() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var input = FreshAdmissionReturn(fixture.Student, fixture.Offering);
		var returned = await fixture.Service.ReturnAsync(input);
		var onboarding = GetRequiredService<IStudentOnboardingAppService>();
		await onboarding.CreatePolicyAsync(new() { AcademicYearId = fixture.Student.AcademicYearId!.Value, Name = "Return cancellation",
			RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false, RequireStudentAccount = false, RequireGuardianAccount = false });
		var finalized = await FinalizeAsync(await ReadyAdmissionReturnApplicationAsync(returned.AdmissionApplicationId!.Value), false, "return-cancel-finalize");
		var item = await onboarding.GetAsync(finalized.OnboardingCaseId);
		var preserved = await AdmissionReturnHistoryAsync(fixture.Student.Id);
		const string reason = "Family cancelled before activation";
		var cancelled = await onboarding.CancelAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, Reason = reason });
		cancelled.Status.ShouldBe(StudentOnboardingStatus.Cancelled); cancelled.StudentEnrollmentId.ShouldBeNull();
		(await fixture.Service.GetAsync(returned.TransitionId!.Value)).Status.ShouldBe("Cancelled");
		await WithUnitOfWorkAsync(async () => await GetRequiredService<StudentRecordAdmissionReturnClosureService>()
			.CloseCancelledOnboardingAsync(await _onboardingCaseRepository.GetAsync(item.Id)));
		(await fixture.Service.ReturnAsync(input)).Status.ShouldBe("Cancelled");
		(await AdmissionReturnHistoryAsync(fixture.Student.Id)).ShouldBe(preserved);
		await AssertAdmissionReturnClosureEventAsync(returned.TransitionId.Value, "OnboardingCancelled", reason);
		var fresh = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		fresh.Status.ShouldBe("AwaitingAdmission"); fresh.TransitionId.ShouldNotBe(returned.TransitionId);
		(await onboarding.GetAsync(item.Id)).Status.ShouldBe(StudentOnboardingStatus.Cancelled);
		(await _applicationAppService.GetAsync(returned.AdmissionApplicationId.Value)).Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
	});

	private Task<string> AdmissionReturnHistoryAsync(Guid studentId) => WithUnitOfWorkAsync(async () =>
	{
		var student = await _studentRepository.GetAsync(studentId);
		var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == studentId);
		var original = await _onboardingCaseRepository.GetAsync(x => x.StudentId == studentId && x.Status == StudentOnboardingStatus.Completed);
		return JsonSerializer.Serialize(new { Placement = StudentRecordTransitionReadinessService.Capture(student, enrollment),
			student.FirstName, student.MiddleName, student.LastName, student.GrNumber, student.ExtraProperties,
			EnrollmentCount = await _studentEnrollmentRepository.CountAsync(x => x.StudentId == studentId),
			OriginalCase = JsonSerializer.Serialize(original), enrollment.OnboardingCaseId });
	});

	private Task AssertAdmissionReturnClosureEventAsync(Guid transitionId, string action, string reason) => WithUnitOfWorkAsync(async () =>
	{
		var events = GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>();
		(await events.CountAsync(x => x.TransitionId == transitionId && x.Action == action)).ShouldBe(1);
		var closure = await events.GetAsync(x => x.TransitionId == transitionId && x.Action == action);
		closure.ActorId.ShouldBe(TestUserId); closure.Remarks.ShouldBe(reason);
		(await events.CountAsync(x => x.TransitionId == transitionId && x.Action == "Submitted")).ShouldBe(1);
	});
}
