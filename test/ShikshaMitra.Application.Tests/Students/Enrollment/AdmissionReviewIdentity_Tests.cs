using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_canonical_middle_name_correction_invalidates_review_without_a_form_or_application_write() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync();
			var submitted = await SubmitReviewAsync(fixture);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId);
				applicant.UpdateProfile(applicant.FirstName, "Corrected middle name", applicant.LastName,
					applicant.DateOfBirth, applicant.Gender, applicant.AddressLine1, applicant.AddressLine2,
					applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
				await _applicantRepository.UpdateAsync(applicant, autoSave: true);
			});
			var review = await GetRequiredService<IAdmissionReviewAppService>().GetAsync(fixture.ApplicationId, fixture.StageId);
			review.ConcurrencyStamp.ShouldBe(submitted.ConcurrencyStamp);
			review.IsStale.ShouldBeTrue();
			review.Decision.CanApprove.ShouldBeFalse();
			review.LatestRecord!.Id.ShouldBe(submitted.LatestRecord!.Id);
		});
}
