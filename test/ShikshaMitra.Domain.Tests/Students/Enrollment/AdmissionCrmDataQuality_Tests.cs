using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmDataQuality_Tests
{
	[Fact]
	public void Redetection_Should_Reopen_A_Resolved_Issue_Without_Losing_First_Detection()
	{
		var firstDetectedAt = DateTime.UtcNow.AddDays(-2);
		var issue = CreateIssue(firstDetectedAt);
		issue.Resolve(false, "The duplicate records were merged.", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));

		issue.Redetect(AdmissionCrmDataQualityIssueSeverity.Critical, "{\"leadIds\":\"1,2\"}", DateTime.UtcNow);

		issue.Status.ShouldBe(AdmissionCrmDataQualityIssueStatus.Open);
		issue.FirstDetectedAt.ShouldBe(firstDetectedAt);
		issue.ResolutionReason.ShouldBeNull();
		issue.Severity.ShouldBe(AdmissionCrmDataQualityIssueSeverity.Critical);
	}

	[Fact]
	public void Reconciliation_Should_Not_Overwrite_Accepted_Risk()
	{
		var issue = CreateIssue(DateTime.UtcNow.AddDays(-1));
		issue.Resolve(true, "Shared household contact was verified.", Guid.NewGuid(), DateTime.UtcNow);

		issue.ResolveByReconciliation(DateTime.UtcNow.AddHours(1));

		issue.Status.ShouldBe(AdmissionCrmDataQualityIssueStatus.Ignored);
		issue.ResolutionReason.ShouldBe("Shared household contact was verified.");
	}

	private static AdmissionCrmDataQualityIssue CreateIssue(DateTime at) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FINGERPRINT",
		AdmissionCrmDataQualityIssueType.DuplicateApplicant,
		AdmissionCrmDataQualityIssueSeverity.Warning, Guid.NewGuid(), null,
		"Possible duplicate applicants", "{}", "[]", at);
}
