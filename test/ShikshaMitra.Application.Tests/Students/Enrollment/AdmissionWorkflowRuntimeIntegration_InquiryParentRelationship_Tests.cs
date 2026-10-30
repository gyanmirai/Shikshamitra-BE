using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("father", "mother")]
	[InlineData("mother", "father")]
	public Task Inquiry_detail_preserves_captured_parent_relationship_when_other_parent_is_main_contact(
		string capturedRelationship, string mainContactRelationship) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering);
		input.ParentRelationship = capturedRelationship;
		input.FamilyContacts = new()
		{
			MainContactRelationship = mainContactRelationship,
			OtherParent = new()
			{
				FullName = "Other Parent", Relationship = mainContactRelationship,
				// A shared phone must not be treated as evidence of which parent was captured.
				Mobile = input.GuardianPhone
			}
		};
		var inquiry = await service.CreateAsync(input);

		var detail = await service.GetAsync(inquiry.Id);

		detail.ParentRelationship.ShouldBe(capturedRelationship);
		detail.MainSchoolContact.ShouldNotBeNull().Relationship.ShouldBe(mainContactRelationship);
		detail.MainSchoolContact!.Mobile.ShouldBe(input.GuardianPhone);
	});

	[Fact]
	public Task Inquiry_detail_without_captured_parent_relationship_does_not_infer_it_from_main_contact() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var service = GetRequiredService<IAdmissionInquiryAppService>();
			var input = ManagedInquiryInput(offering);
			input.ParentRelationship = "father";
			var inquiry = await service.CreateAsync(input);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value);
				applicant.ExtraProperties.Remove("ParentAccountRelationship");
				await _applicantRepository.UpdateAsync(applicant, true);
			});

			var detail = await service.GetAsync(inquiry.Id);

			detail.ParentRelationship.ShouldBeNull();
			detail.MainSchoolContact.ShouldNotBeNull().Relationship.ShouldBe("father");
		});
}
