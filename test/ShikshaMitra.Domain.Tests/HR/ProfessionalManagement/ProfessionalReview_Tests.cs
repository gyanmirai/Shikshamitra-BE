using System;
using ShikshaMitra.HR;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReview_Tests
{
	[Fact]
	public void SaveDraft_Should_Update_Draft_Review()
	{
		var review = NewReview();

		review.SaveDraft(4, 3, "Good", "Keep improving", Guid.NewGuid());

		review.Status.ShouldBe(ProfessionalReviewStatus.Draft);
		review.CommunicationScore.ShouldBe(4);
		review.ProblemSolvingScore.ShouldBe(3);
		review.SubmittedAt.ShouldBeNull();
	}

	[Fact]
	public void Submit_Should_Submit_Draft_Review()
	{
		var review = NewReview();
		var submittedAt = DateTime.UtcNow;

		review.Submit(5, 4, "Strong", "Ready", Guid.NewGuid(), submittedAt);

		review.Status.ShouldBe(ProfessionalReviewStatus.Submitted);
		review.SubmittedAt.ShouldBe(submittedAt);
	}

	[Fact]
	public void SaveDraft_Should_Reject_Submitted_Review()
	{
		var review = NewReview();
		review.Submit(5, 4, "Strong", "Ready", Guid.NewGuid(), DateTime.UtcNow);

		var ex = Should.Throw<BusinessException>(() =>
			review.SaveDraft(1, 1, "Changed", "Changed", Guid.NewGuid()));

		ex.Code.ShouldBe(HRErrorCodes.ProfessionalReviewAlreadySubmitted);
	}

	[Fact]
	public void Submit_Should_Reject_Submitted_Review()
	{
		var review = NewReview();
		review.Submit(5, 4, "Strong", "Ready", Guid.NewGuid(), DateTime.UtcNow);

		var ex = Should.Throw<BusinessException>(() =>
			review.Submit(1, 1, "Changed", "Changed", Guid.NewGuid(), DateTime.UtcNow));

		ex.Code.ShouldBe(HRErrorCodes.ProfessionalReviewAlreadySubmitted);
	}

	private static ProfessionalReview NewReview()
	{
		return new ProfessionalReview(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	}
}
