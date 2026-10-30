using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Finalization_main_contact_preview_and_handoff_preserve_legacy_guardian_or_block_explicit_missing_parent(bool explicitSelection) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiryInput = ManagedInquiryInput(offering);
			inquiryInput.ApplicantLastName = "Patel";
			var inquiry = await inquiries.CreateAsync(inquiryInput);
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
			application = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.ApproveAsync(application.Id, Decision(application, "QA contact review"));
			await CompleteCertificationWorkflowAsync(application.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
				applicant.ExtraProperties.Remove("ParentAccountFlowVersion");
				applicant.ExtraProperties.Remove("ParentAccountRelationship");
				applicant.ExtraProperties.Remove("AdmissionFamilyContactsRequest");
				foreach (var key in applicant.ExtraProperties.Keys.Where(key => key.StartsWith("AdmissionStudentField.Father.") || key.StartsWith("AdmissionStudentField.Mother.")).ToList())
					applicant.ExtraProperties.Remove(key);
				applicant.SetProperty(StudentFamilyContactSelection.SelectionKey, "father");
				applicant.SetProperty("AdmissionStudentField.Applicant.Nationality", "Indian");
				if (explicitSelection) applicant.SetProperty("AdmissionFamilyContactsRequest", "{\"MainContactRelationship\":\"father\"}");
				await _applicantRepository.UpdateAsync(applicant, true);
			});
			var beforeProfile = await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(application.ApplicantId)).ExtraProperties));
			var beforeStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp;
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(application.Id);
			preview.PossibleMatches.ShouldBeEmpty();
			await _applicationAppService.GetFinalizationPreviewAsync(application.Id);
			(await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(application.ApplicantId)).ExtraProperties))).ShouldBe(beforeProfile);
			(await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp.ShouldBe(beforeStamp);
			application = await _applicationAppService.GetAsync(application.Id);
			var input = new FinalizeAdmissionApplicationDto
			{
				ConcurrencyStamp = application.ConcurrencyStamp, ConfirmCreateNew = true,
				ConfirmGuardianContactVerified = true, IdempotencyKey = "qa-main-contact-" + explicitSelection
			};
			if (explicitSelection)
			{
				preview.IsReady.ShouldBeFalse();
				preview.Items.Where(item => !item.Passed).Select(item => item.Key).ShouldBe(["family_contact"]);
				(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.FinalizeAsync(application.Id, input)))
					.Code.ShouldBe(StudentOnboardingErrorCodes.FinalizationNotReady);
				(await _applicationAppService.GetAsync(application.Id)).StudentId.ShouldBeNull();
				return;
			}
			preview.IsReady.ShouldBeTrue();
			var result = await _applicationAppService.FinalizeAsync(application.Id, input);
			var replay = await _applicationAppService.FinalizeAsync(application.Id, input);
			replay.StudentId.ShouldBe(result.StudentId);
			replay.OnboardingCaseId.ShouldBe(result.OnboardingCaseId);
			await WithUnitOfWorkAsync(async () =>
			{
				var student = (await _studentRepository.GetWithDetailsAsync(result.StudentId)).ShouldNotBeNull();
				StudentFamilyContactSelection.Resolve(student).ShouldBe("guardian");
				student.FamilyDetail!.GuardianDetails.ShouldNotBeNull();
				student.FamilyDetail.FatherDetails.ShouldBeNull();
				student.FamilyDetail.MotherDetails.ShouldBeNull();
				student.UserId.ShouldBeNull();
				(await _onboardingCaseRepository.GetListAsync(item => item.ApplicationId == application.Id)).Count.ShouldBe(1);
				var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
				applicant.GetProperty<string>(StudentFamilyContactSelection.SelectionKey).ShouldBe("father");
				AdmissionSiblingParentSelection.IsParentFlow(applicant).ShouldBeFalse();
			});
		});
}
