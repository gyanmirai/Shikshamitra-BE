using System.Linq;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionInquirySubmissionIdempotency_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ShikshaMitraDbContext _dbContext;

	public AdmissionInquirySubmissionIdempotency_Tests()
	{
		_dbContext = GetRequiredService<ShikshaMitraDbContext>();
	}

	[Fact]
	public void Public_submission_key_must_have_the_expected_unique_tenant_template_index()
	{
		var entityType = _dbContext.Model.FindEntityType(typeof(AdmissionInquiryFormSubmission));
		var submissionKey = entityType?.FindProperty(nameof(AdmissionInquiryFormSubmission.SubmissionKey));
		var idempotencyIndex = entityType?.GetIndexes().SingleOrDefault(index =>
			index.Properties.Select(property => property.Name).SequenceEqual(
				new[]
				{
					nameof(AdmissionInquiryFormSubmission.TenantId),
					nameof(AdmissionInquiryFormSubmission.FormTemplateId),
					nameof(AdmissionInquiryFormSubmission.SubmissionKey)
				}));

		entityType.ShouldNotBeNull();
		submissionKey.ShouldNotBeNull();
		submissionKey.GetMaxLength().ShouldBe(AdmissionCrmConsts.MaxSubmissionKeyLength);
		idempotencyIndex.ShouldNotBeNull();
		idempotencyIndex.IsUnique.ShouldBeTrue();
	}
}
